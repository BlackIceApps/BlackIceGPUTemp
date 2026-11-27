using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;
using BlackIceGPUTemp.Logging;
using BlackIceGPUTemp.Monitoring;
using BlackIceGPUTemp.Monitoring.Abstractions;

namespace BlackIceGPUTemp.Tray
{
    internal sealed class BlackIceGPUTempApplicationContext : ApplicationContext
    {
        private const int TimerIntervalMs = 1000;

        private readonly NotifyIcon _notifyIcon;
        private readonly Timer _timer;
        private Icon? _currentIcon;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly MonitoringShutdownState _shutdownState;
        private readonly Control _uiDispatcher = new();
        private readonly MonitoringReadExecutor _readExecutor;
        private readonly MonitoringReadTrigger _readTrigger;
        private MonitoringSession? _session;
        private MonitoringReadCycle? _readCycle;
        private readonly IGpuMonitoringSessionFactory _monitoringSessionFactory;
        private readonly MonitoringLifecycleOwner _lifecycleOwner = new();
        private readonly MonitoringDisplayUpdater _displayUpdater;
        private readonly TrayMenu _trayMenu;
        private int _initializationInProgress = 0;
        private bool _initializationFailed = false;

        private sealed class MonitoringSession
        {
            public HardwareMonitorService MonitorService { get; }
            public string ProviderId { get; }
            public string DeviceId { get; }
            public string DeviceName { get; }

            public MonitoringSession(HardwareMonitorService monitorService, string providerId, string deviceId, string deviceName)
            {
                MonitorService = monitorService;
                ProviderId = providerId;
                DeviceId = deviceId;
                DeviceName = deviceName;
            }
        }

        public BlackIceGPUTempApplicationContext(IGpuMonitoringSessionFactory monitoringSessionFactory)
        {
            _monitoringSessionFactory = monitoringSessionFactory ?? throw new ArgumentNullException(nameof(monitoringSessionFactory));

            _notifyIcon = new NotifyIcon
            {
                Icon = TrayIconRenderer.CreateTemperatureIcon("--"),
                Visible = true,
                Text = "BlackIceGPUTemp: Starting..."
            };
            _currentIcon = _notifyIcon.Icon;

            _notifyIcon.MouseClick += OnNotifyIconMouseClick;

            _timer = new Timer { Interval = TimerIntervalMs };
            _timer.Tick += OnTimerTick;

            _ = _uiDispatcher.Handle;

            _shutdownState = new MonitoringShutdownState(_cancellationTokenSource);
            _readExecutor = new MonitoringReadExecutor(action => Task.Run(action));
            _readTrigger = new MonitoringReadTrigger(_readExecutor);
            _displayUpdater = new MonitoringDisplayUpdater(
                InvokeOnUiThread,
                UpdateTooltip,
                UpdateIcon,
                ex => Logger.LogError("Monitoring display update failed", ex));

            var initToken = _cancellationTokenSource.Token;
            _trayMenu = new TrayMenu(_uiDispatcher.Handle);
            Task.Run(() => Initialize(initToken));
        }

        private void OnNotifyIconMouseClick(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
            {
                return;
            }

            var cursorPos = Cursor.Position;

            TrayMenuResult result = _trayMenu.ShowContextMenu(cursorPos, _initializationFailed && !_shutdownState.IsDisposing);
            if (result == TrayMenuResult.Exit)
            {
                ExitThread();
            }
            else if (result == TrayMenuResult.Retry)
            {
                RetryInitialization();
            }
        }

        private void RetryInitialization()
        {
            if (!_shutdownState.TryGetCancellationToken(out var token))
            {
                return;
            }

            if (Interlocked.CompareExchange(ref _initializationInProgress, 1, 0) != 0)
            {
                return;
            }

            Task.Run(() => RetryInitializationAsync(token));
        }

        private async Task RetryInitializationAsync(CancellationToken token)
        {
            try
            {
                InvokeOnUiThreadSync(() => UpdateTooltip("BlackIceGPUTemp: Initializing..."));

                var initResult = await Task.Run(() =>
                {
                    try
                    {
                        return _monitoringSessionFactory.Create(token);
                    }
                    catch (OperationCanceledException)
                    {
                        return new GpuMonitoringInitializationResult("Operation canceled");
                    }
                    catch (Exception ex)
                    {
                        if (!token.IsCancellationRequested)
                        {
                            Logger.LogError("Initialization error", ex);
                            return new GpuMonitoringInitializationResult("Initialization error");
                        }
                        return new GpuMonitoringInitializationResult("Operation canceled");
                    }
                });

                if (token.IsCancellationRequested)
                {
                    if (initResult.ProviderLifecycle != null)
                    {
                        try
                        {
                            initResult.ProviderLifecycle.Disconnect();
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError("Error disconnecting provider after cancellation", ex);
                        }
                    }
                    return;
                }

                if (!initResult.IsSuccess)
                {
                    HandleInitFailure(initResult.Message ?? "Initialization failed", token);
                }
                else
                {
                    HandleInitSuccess(initResult, token);
                }
            }
            finally
            {
                ReleaseInitializationLock();
            }
        }

        private void HandleInitFailure(string message, CancellationToken token)
        {
            Logger.LogWarn($"GPU monitoring initialization failed: {message}");
            InvokeOnUiThreadSync(() =>
            {
                if (token.IsCancellationRequested || _shutdownState.IsDisposing)
                {
                    return;
                }
                _initializationFailed = true;
                UpdateTooltip($"BlackIceGPUTemp: {message}");
            });
        }

        private void HandleInitSuccess(GpuMonitoringInitializationResult result, CancellationToken token)
        {
            var monitorService = result.HardwareMonitorService;
            var providerId = result.ProviderId;
            var deviceId = result.DeviceId;
            var deviceName = result.DeviceName;
            var providerLifecycle = result.ProviderLifecycle;

            if (monitorService == null ||
                providerId == null || string.IsNullOrWhiteSpace(providerId) ||
                deviceId == null || string.IsNullOrWhiteSpace(deviceId) ||
                deviceName == null || string.IsNullOrWhiteSpace(deviceName))
            {
                if (providerLifecycle != null)
                {
                    try
                    {
                        providerLifecycle.Disconnect();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Error disconnecting provider after invalid initialization data", ex);
                    }
                }

                if (token.IsCancellationRequested || _shutdownState.IsDisposing)
                {
                    return;
                }

                HandleInitFailure("Initialization error", token);
                return;
            }

            if (token.IsCancellationRequested || _shutdownState.IsDisposing)
            {
                if (providerLifecycle != null)
                {
                    try
                    {
                        providerLifecycle.Disconnect();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Error disconnecting provider after cancellation", ex);
                    }
                }
                return;
            }

            var session = new MonitoringSession(
                monitorService,
                providerId,
                deviceId,
                deviceName);

            bool registered = _lifecycleOwner.TryRegister(providerLifecycle);
            if (!registered)
            {
                if (providerLifecycle != null)
                {
                    try
                    {
                        providerLifecycle.Disconnect();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Error disconnecting provider during disposal race", ex);
                    }
                }
                return;
            }

            var readCycle = new MonitoringReadCycle(
                monitorService,
                _displayUpdater,
                ex => Logger.LogError("Error reading temperature", ex),
                providerId,
                deviceId,
                deviceName);

            InvokeOnUiThreadSync(() =>
            {
                if (token.IsCancellationRequested || _shutdownState.IsDisposing)
                {
                    return;
                }
                _session = session;
                _readCycle = readCycle;
                _initializationFailed = false;
                _timer.Start();
                UpdateTooltip("BlackIceGPUTemp: Reading temperature...");
            });
        }

        private void ReleaseInitializationLock()
        {
            Interlocked.Exchange(ref _initializationInProgress, 0);
        }

        private void Initialize(CancellationToken cancellationToken)
        {
            try
            {
                var initResult = _monitoringSessionFactory.Create(cancellationToken);

                if (!initResult.IsSuccess)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    HandleInitFailure(initResult.Message ?? "Initialization failed", cancellationToken);
                    return;
                }

                var monitorService = initResult.HardwareMonitorService;
                var providerId = initResult.ProviderId;
                var deviceId = initResult.DeviceId;
                var deviceName = initResult.DeviceName;
                var providerLifecycle = initResult.ProviderLifecycle;

                if (monitorService == null ||
                    providerId == null || string.IsNullOrWhiteSpace(providerId) ||
                    deviceId == null || string.IsNullOrWhiteSpace(deviceId) ||
                    deviceName == null || string.IsNullOrWhiteSpace(deviceName))
                {
                    if (providerLifecycle != null)
                    {
                        try
                        {
                            providerLifecycle.Disconnect();
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError("Error disconnecting provider after invalid initialization data", ex);
                        }
                    }

                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    HandleInitFailure("Initialization error", cancellationToken);
                    return;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    if (providerLifecycle != null)
                    {
                        try
                        {
                            providerLifecycle.Disconnect();
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError("Error disconnecting provider after cancellation", ex);
                        }
                    }
                    return;
                }

                var session = new MonitoringSession(
                    monitorService,
                    providerId,
                    deviceId,
                    deviceName);

                bool registered = _lifecycleOwner.TryRegister(providerLifecycle);
                if (!registered)
                {
                    if (providerLifecycle != null)
                    {
                        try
                        {
                            providerLifecycle.Disconnect();
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError("Error disconnecting provider during disposal race", ex);
                        }
                    }
                    return;
                }

                var readCycle = new MonitoringReadCycle(
                    monitorService,
                    _displayUpdater,
                    ex => Logger.LogError("Error reading temperature", ex),
                    providerId,
                    deviceId,
                    deviceName);

                InvokeOnUiThreadSync(() =>
                {
                    if (cancellationToken.IsCancellationRequested || _shutdownState.IsDisposing)
                    {
                        return;
                    }
                    _session = session;
                    _readCycle = readCycle;
                    _initializationFailed = false;
                    _timer.Start();
                    UpdateTooltip("BlackIceGPUTemp: Reading temperature...");
                });
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    Logger.LogError("Initialization error", ex);
                    InvokeOnUiThreadSync(() =>
                    {
                        if (cancellationToken.IsCancellationRequested || _shutdownState.IsDisposing)
                        {
                            return;
                        }
                        _initializationFailed = true;
                        UpdateTooltip("BlackIceGPUTemp: Initialization error");
                    });
                }
            }
        }

        private void InvokeOnUiThread(Action action)
        {
            if (_shutdownState.IsDisposing)
            {
                return;
            }

            try
            {
                _uiDispatcher.BeginInvoke(() =>
                {
                    if (_shutdownState.IsDisposing)
                    {
                        return;
                    }
                    action();
                });
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void InvokeOnUiThreadSync(Action action)
        {
            if (_shutdownState.IsDisposing)
            {
                return;
            }

            try
            {
                if (_uiDispatcher.InvokeRequired)
                {
                    _uiDispatcher.Invoke(action);
                }
                else
                {
                    action();
                }
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void OnTimerTick(object? sender, EventArgs e)
        {
            var session = _session;
            var readCycle = _readCycle;

            if (!_shutdownState.TryGetCancellationToken(out var token))
            {
                return;
            }

            bool isDisposing = _shutdownState.IsDisposing;

            _readTrigger.TryStart(
                hasSession: session != null,
                providerId: session?.ProviderId,
                deviceId: session?.DeviceId,
                isDisposing: isDisposing,
                operation: ct =>
                {
                    if (readCycle == null)
                    {
                        return;
                    }

                    readCycle.Execute(ct, () => _shutdownState.IsDisposing);
                },
                cancellationToken: token);
        }

        private void UpdateIcon(string text)
        {
            if (_notifyIcon == null)
            {
                return;
            }

            var newIcon = TrayIconRenderer.CreateTemperatureIcon(text);
            if (newIcon == null)
            {
                return;
            }

            var oldIcon = _currentIcon;
            _notifyIcon.Icon = newIcon;
            _currentIcon = newIcon;

            if (oldIcon != null)
            {
                oldIcon.Dispose();
            }
        }

        private void UpdateTooltip(string text)
        {
            if (_notifyIcon != null)
            {
                if (text.Length > 63)
                {
                    text = text.Substring(0, 60) + "...";
                }
                _notifyIcon.Text = text;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (!_shutdownState.TryBeginDispose())
                {
                    base.Dispose(disposing);
                    return;
                }

                var cleanup = new MonitoringShutdownCleanup(
                    stopTimer: () => _timer?.Stop(),
                    stopAndTakeLifecycle: () => _lifecycleOwner.StopAndTake(),
                    stopExecutor: () => _readExecutor.Stop(),
                    cancelTokenSource: () => _cancellationTokenSource.Cancel(),
                    waitForIdle: () => _readExecutor.WaitForIdle(),
                    disposeTokenSource: () => _cancellationTokenSource.Dispose(),
                    disposeTimer: () => _timer?.Dispose(),
                    hideNotifyIcon: () =>
                    {
                        if (_notifyIcon != null)
                        {
                            _notifyIcon.Visible = false;
                        }
                    },
                    disposeNotifyIcon: () =>
                    {
                        if (_notifyIcon != null)
                        {
                            _notifyIcon.Dispose();
                        }
                    },
                    disposeCurrentIcon: () => _currentIcon?.Dispose(),
                    disposeUiDispatcher: () => _uiDispatcher.Dispose());

                cleanup.Execute();
            }
            base.Dispose(disposing);
        }
    }
}