using System;
using System.Drawing;
using System.Windows.Forms;
using TinyTemp.Monitoring;
using TinyTemp.Monitoring.Providers.Amd;

namespace TinyTemp.Tray
{
    internal sealed class TinyTempApplicationContext : ApplicationContext
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly Timer? _timer;
        private readonly HardwareMonitorService? _monitorService;
        private readonly string? _providerId;
        private readonly string? _deviceId;
        private readonly string _deviceName;

        public TinyTempApplicationContext()
        {
            _notifyIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Visible = true,
                Text = "TinyTemp: Starting..."
            };

            var exitMenuItem = new MenuItem("Exit", (_, _) => ExitThread());
            _notifyIcon.ContextMenu = new ContextMenu(new[] { exitMenuItem });

            var provider = new AmdAdlxProvider();
            var probeResult = provider.Probe();

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

                    _timer = new Timer { Interval = 5000 };
                    _timer.Tick += OnTimerTick;
                    _timer.Start();

                    UpdateTooltip("TinyTemp: Reading temperature...");
                }
                else
                {
                    _deviceName = "No supported GPU detected";
                    _notifyIcon.Text = "TinyTemp: No supported GPU";
                }
            }
            else
            {
                _deviceName = "ADLX not available";
                _notifyIcon.Text = "TinyTemp: ADLX unavailable";
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

            bool success = _monitorService.TryReadTemperature(providerId, deviceId, out var reading, out var error);

            if (success && reading != null)
            {
                UpdateTooltip($"{_deviceName}: {reading.Celsius:F1}°C");
            }
            else
            {
                UpdateTooltip($"{_deviceName}: Temp unavailable");
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
                _timer?.Stop();
                _timer?.Dispose();

                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}