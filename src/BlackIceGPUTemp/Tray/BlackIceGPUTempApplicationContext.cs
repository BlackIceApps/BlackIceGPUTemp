using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;
using BlackIceGPUTemp.Monitoring;
using BlackIceGPUTemp.Monitoring.Providers.Amd;

namespace BlackIceGPUTemp.Tray
{
    internal sealed class BlackIceGPUTempApplicationContext : ApplicationContext
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly Timer _timer;
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

        public BlackIceGPUTempApplicationContext()
        {
            _notifyIcon = new NotifyIcon
            {
                Icon = CreateTemperatureIcon("--"),
                Visible = true,
                Text = "BlackIceGPUTemp: Starting..."
            };
            _currentIcon = _notifyIcon.Icon;

            _notifyIcon.MouseClick += OnNotifyIconMouseClick;

            _timer = new Timer { Interval = 1000 };
            _timer.Tick += OnTimerTick;

            _ = _uiDispatcher.Handle;

            Task.Run(() => Initialize(_cancellationTokenSource.Token));
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
                            UpdateTooltip("BlackIceGPUTemp: Reading temperature...");
                        });
                    }
                    else
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            return;
                        }
                        _deviceName = "No supported GPU detected";
                        InvokeOnUiThread(() => _notifyIcon.Text = "BlackIceGPUTemp: No supported GPU");
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
                    InvokeOnUiThread(() => UpdateTooltip($"BlackIceGPUTemp: {reason}"));
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    InvokeOnUiThread(() => UpdateTooltip("BlackIceGPUTemp: Initialization error"));
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
            // 1. DPI awareness - scale icon size with system DPI
            float dpiX, dpiY;
            using (var bmp = new Bitmap(1, 1, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                dpiX = bmp.HorizontalResolution;
                dpiY = bmp.VerticalResolution;
            }

            int width = (int)Math.Round(16 * dpiX / 96);
            int height = (int)Math.Round(16 * dpiY / 96);
            width = width < 16 ? 16 : width;
            height = height < 16 ? 16 : height;

            // 2. Use system font family (adapts to OS)
            var fontFamily = SystemFonts.MessageBoxFont.FontFamily;

            // 3. Per-font base size tuning (compensate metric differences)
            float baseSize = fontFamily.Name switch
            {
                "Segoe UI" => 12f,
                "Tahoma" => 11f,
                _ => 12f
            };

            // 4. Configurable overrides (adjust these to taste)
            const float fontSizeMultiplier = 1f;    // Multiply calculated size
            const int fontStyle = (int)FontStyle.Bold; // FontStyle.Regular for normal
            const float verticalOffset = -1f;       // Pixels up (-) or down (+)

            float fontSize = baseSize * width / 16f * fontSizeMultiplier;

            var bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            IntPtr hIcon = IntPtr.Zero;

            try
            {
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(Color.Transparent);

                    // 3. TextRenderer for better small-text rendering on Windows
                    graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                    graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

                    using (var font = new Font(fontFamily, fontSize, (FontStyle)fontStyle, GraphicsUnit.Pixel))
                    using (var brush = new SolidBrush(Color.White))
                    {
                        var bounds = new Rectangle(0, 0, width, height);
                        var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding;

                        // Apply vertical offset by adjusting bounds
                        if (verticalOffset != 0)
                        {
                            bounds = new Rectangle(0, (int)Math.Round(verticalOffset), width, height);
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
                _uiDispatcher.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}