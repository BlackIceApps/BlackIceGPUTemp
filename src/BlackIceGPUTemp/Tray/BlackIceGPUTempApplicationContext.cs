using System;
using System.Drawing;
using System.Runtime.InteropServices;
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
        private const int IconBaseSize = 16;
        private const int BaseDpi = 96;
        private const float SegoeUiFontSize = 12f;
        private const float TahomaFontSize = 11f;
        private const float DefaultFontSize = 12f;
        private const float FontSizeMultiplier = 1f;
        private const FontStyle IconFontStyle = FontStyle.Bold;
        private const float VerticalOffset = -1f;
        private const string SegoeUiFontFamily = "Segoe UI";
        private const string TahomaFontFamily = "Tahoma";

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

        private const int MenuExitId = 1001;
        private const uint TPM_RIGHTALIGN = 0x0008;
        private const uint TPM_BOTTOMALIGN = 0x0020;
        private const uint TPM_RIGHTBUTTON = 0x0002;
        private const uint TPM_RETURNCMD = 0x0100;
        private const uint WM_NULL = 0x0000;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll")]
        private static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll")]
        private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, uint uIDNewItem, string lpNewItem);

        [DllImport("user32.dll")]
        private static extern int TrackPopupMenuEx(IntPtr hMenu, uint uFlags, int x, int y, IntPtr hwnd, IntPtr lptpm);

        [DllImport("user32.dll")]
        private static extern bool DestroyMenu(IntPtr hMenu);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        public BlackIceGPUTempApplicationContext(IGpuMonitoringSessionFactory monitoringSessionFactory)
        {
            _monitoringSessionFactory = monitoringSessionFactory ?? throw new ArgumentNullException(nameof(monitoringSessionFactory));

            _notifyIcon = new NotifyIcon
            {
                Icon = CreateTemperatureIcon("--"),
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
            Task.Run(() => Initialize(initToken));
        }

        private void OnNotifyIconMouseClick(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
            {
                return;
            }

            var cursorPos = Cursor.Position;

            IntPtr hMenu = CreatePopupMenu();
            if (hMenu == IntPtr.Zero)
            {
                return;
            }

            try
            {
                if (!AppendMenu(hMenu, 0x00000000, (uint)MenuExitId, "Exit"))
                {
                    return;
                }

                uint flags = TPM_RIGHTALIGN | TPM_BOTTOMALIGN | TPM_RIGHTBUTTON | TPM_RETURNCMD;
                int x = cursorPos.X;
                int y = cursorPos.Y;

                SetForegroundWindow(_uiDispatcher.Handle);
                int result = TrackPopupMenuEx(hMenu, flags, x, y, _uiDispatcher.Handle, IntPtr.Zero);
                PostMessage(_uiDispatcher.Handle, WM_NULL, IntPtr.Zero, IntPtr.Zero);

                if (result == MenuExitId)
                {
                    ExitThread();
                }
            }
            finally
            {
                DestroyMenu(hMenu);
            }
        }

        private void Initialize(CancellationToken cancellationToken)
        {
            try
            {
                var result = _monitoringSessionFactory.Create(cancellationToken);

                if (!result.IsSuccess)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    Logger.LogWarn($"GPU monitoring initialization failed: {result.Message}");
                    InvokeOnUiThread(() => UpdateTooltip($"BlackIceGPUTemp: {result.Message}"));
                    return;
                }

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
                    // Always disconnect provider lifecycle if present, regardless of cancellation
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

                    Logger.LogError("GPU monitoring initialization succeeded but returned invalid data.");
                    InvokeOnUiThread(() => UpdateTooltip("BlackIceGPUTemp: Initialization error"));
                    return;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    // If cancelled after successful creation but before publishing, disconnect the provider
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

                // Take ownership of the lifecycle provider before publishing the session
                bool registered = _lifecycleOwner.TryRegister(providerLifecycle);
                if (!registered)
                {
                    // Context is already disposing - disconnect after registration attempt
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

                InvokeOnUiThread(() =>
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                    _session = session;
                    _readCycle = readCycle;
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
                    InvokeOnUiThread(() => UpdateTooltip("BlackIceGPUTemp: Initialization error"));
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

            var newIcon = CreateTemperatureIcon(text);
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

        private static Icon? CreateTemperatureIcon(string text)
        {
            float dpiX, dpiY;
            using (var bmp = new Bitmap(1, 1, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                dpiX = bmp.HorizontalResolution;
                dpiY = bmp.VerticalResolution;
            }

            int width = (int)Math.Round(IconBaseSize * dpiX / BaseDpi);
            int height = (int)Math.Round(IconBaseSize * dpiY / BaseDpi);
            width = width < IconBaseSize ? IconBaseSize : width;
            height = height < IconBaseSize ? IconBaseSize : height;

            var fontFamily = SystemFonts.MessageBoxFont.FontFamily;

            float baseSize = fontFamily.Name switch
            {
                SegoeUiFontFamily => SegoeUiFontSize,
                TahomaFontFamily => TahomaFontSize,
                _ => DefaultFontSize
            };

            float fontSize = baseSize * width / IconBaseSize * FontSizeMultiplier;

            var bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            IntPtr hIcon = IntPtr.Zero;

            try
            {
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(Color.Transparent);

                    graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                    graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

                    using (var font = new Font(fontFamily, fontSize, IconFontStyle, GraphicsUnit.Pixel))
                    using (var brush = new SolidBrush(Color.White))
                    {
                        var bounds = new Rectangle(0, 0, width, height);
                        var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding;

                        if (VerticalOffset != 0)
                        {
                            bounds = new Rectangle(0, (int)Math.Round(VerticalOffset), width, height);
                        }

                        TextRenderer.DrawText(graphics, text, font, bounds, Color.White, Color.Transparent, flags);
                    }
                }

                hIcon = bitmap.GetHicon();
                if (hIcon == IntPtr.Zero)
                {
                    return null;
                }

                var icon = Icon.FromHandle(hIcon);
                var clonedIcon = (Icon)icon.Clone();
                return clonedIcon;
            }
            catch (Exception ex)
            {
                Logger.LogError("CreateTemperatureIcon: exception creating temperature icon", ex);
                return null;
            }
            finally
            {
                if (hIcon != IntPtr.Zero)
                {
                    DestroyIcon(hIcon);
                }
                bitmap.Dispose();
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