using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;
using TinyTemp.Monitoring;
using TinyTemp.Monitoring.Providers.Amd;

namespace TinyTemp.Tray
{
    internal sealed class TinyTempApplicationContext : ApplicationContext
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly Timer _timer;
        private readonly ContextMenuStrip _contextMenuStrip;
        private HardwareMonitorService? _monitorService;
        private string? _providerId;
        private string? _deviceId;
        private string _deviceName = "Starting...";
        private Icon? _currentIcon;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly Control _uiDispatcher = new();
        private readonly object _disposeLock = new();
        private bool _disposing;
        private bool _readInProgress;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        public TinyTempApplicationContext()
        {
            _notifyIcon = new NotifyIcon
            {
                Icon = CreateTemperatureIcon("--"),
                Visible = true,
                Text = "TinyTemp: Starting..."
            };
            _currentIcon = _notifyIcon.Icon;

            _contextMenuStrip = new ContextMenuStrip();
            _contextMenuStrip.Items.Add("Exit", null, (_, _) => ExitThread());
            _notifyIcon.MouseClick += OnNotifyIconMouseClick;

            _timer = new Timer { Interval = 1000 };
            _timer.Tick += OnTimerTick;

            _ = _uiDispatcher.Handle;

            Task.Run(() => Initialize(_cancellationTokenSource.Token));
        }

        private void OnNotifyIconMouseClick(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                _contextMenuStrip.Show(Cursor.Position, ToolStripDropDownDirection.AboveLeft);
            }
        }

        private void Initialize(CancellationToken cancellationToken)
        {
            try
            {
                var provider = new AmdAdlxProvider();
                var probeResult = provider.Probe();

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                if (probeResult.IsAvailable)
                {
                    var devices = provider.EnumerateDevices();
                    if (devices != null && devices.Count > 0)
                    {
                        var firstDevice = devices[0];
                        _providerId = provider.Id;
                        _deviceId = firstDevice.Id;
                        _deviceName = firstDevice.Name;

                        var registry = new GpuProviderRegistry(new[] { provider });
                        var selector = new DeviceSelector(registry);
                        _monitorService = new HardwareMonitorService(selector);

                        if (cancellationToken.IsCancellationRequested)
                        {
                            return;
                        }

                        InvokeOnUiThread(() =>
                        {
                            _timer.Start();
                            UpdateTooltip("TinyTemp: Reading temperature...");
                        });
                    }
                    else
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            return;
                        }
                        _deviceName = "No supported GPU detected";
                        InvokeOnUiThread(() => _notifyIcon.Text = "TinyTemp: No supported GPU");
                    }
                }
                else
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                    _deviceName = "ADLX not available";
                    string reason = string.IsNullOrWhiteSpace(probeResult.Message)
                        ? "Unknown error"
                        : probeResult.Message!;
                    InvokeOnUiThread(() => UpdateTooltip($"TinyTemp: {reason}"));
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    InvokeOnUiThread(() => UpdateTooltip("TinyTemp: Initialization error"));
                }
            }
        }

        private void InvokeOnUiThread(Action action)
        {
            lock (_disposeLock)
            {
                if (_disposing)
                {
                    return;
                }

                try
                {
                    _uiDispatcher.BeginInvoke(() =>
                    {
                        lock (_disposeLock)
                        {
                            if (_disposing)
                            {
                                return;
                            }
                            action();
                        }
                    });
                }
                catch (ObjectDisposedException)
                {
                }
                catch (InvalidOperationException)
                {
                }
            }
        }

        private void OnTimerTick(object? sender, EventArgs e)
        {
            if (_monitorService == null)
            {
                return;
            }

            string providerId = _providerId ?? string.Empty;
            string deviceId = _deviceId ?? string.Empty;

            if (string.IsNullOrEmpty(providerId) || string.IsNullOrEmpty(deviceId))
            {
                return;
            }

            lock (_disposeLock)
            {
                if (_disposing || _readInProgress)
                {
                    return;
                }
                _readInProgress = true;
            }

            var capturedProviderId = providerId;
            var capturedDeviceId = deviceId;
            var token = _cancellationTokenSource.Token;

            Task.Run(() =>
            {
                try
                {
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }

                    bool success = _monitorService!.TryReadTemperature(capturedProviderId, capturedDeviceId, out var reading, out var error);

                    lock (_disposeLock)
                    {
                        if (_disposing)
                        {
                            return;
                        }
                    }

                    if (success && reading != null)
                    {
                        InvokeOnUiThread(() =>
                        {
                            UpdateTooltip($"{_deviceName}: {reading.Celsius:F1}°C");
                            UpdateIcon(((int)Math.Round(reading.Celsius)).ToString());
                        });
                    }
                    else
                    {
                        InvokeOnUiThread(() =>
                        {
                            UpdateTooltip($"{_deviceName}: Temp unavailable");
                            UpdateIcon("--");
                        });
                    }
                }
                catch
                {
                    lock (_disposeLock)
                    {
                        if (_disposing)
                        {
                            return;
                        }
                    }

                    InvokeOnUiThread(() =>
                    {
                        UpdateTooltip($"{_deviceName}: Temp unavailable");
                        UpdateIcon("--");
                    });
                }
                finally
                {
                    lock (_disposeLock)
                    {
                        _readInProgress = false;
                    }
                }
            });
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
            const int size = 16;
            var bitmap = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            IntPtr hIcon = IntPtr.Zero;

            try
            {
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(Color.Transparent);
                    graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                    const float fontSize = 11f;
                    using (var font = new Font("Segoe UI", fontSize, FontStyle.Regular, GraphicsUnit.Pixel))
                    using (var brush = new SolidBrush(Color.White))
                    using (var format = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    })
                    {
                        graphics.DrawString(text, font, brush, new RectangleF(0, 0, size, size), format);
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
            catch
            {
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
                lock (_disposeLock)
                {
                    _disposing = true;
                }

                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();

                _timer?.Stop();
                _timer?.Dispose();

                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                }

                _currentIcon?.Dispose();
                _contextMenuStrip?.Dispose();
                _uiDispatcher.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}