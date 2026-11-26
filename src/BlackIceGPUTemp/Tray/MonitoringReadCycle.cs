using System;
using System.Threading;
using BlackIceGPUTemp.Monitoring;
using BlackIceGPUTemp.Monitoring.Abstractions;

namespace BlackIceGPUTemp.Tray
{
    internal sealed class MonitoringReadCycle
    {
        private readonly HardwareMonitorService _monitorService;
        private readonly MonitoringDisplayUpdater _displayUpdater;
        private readonly Action<Exception> _reportError;
        private readonly string _providerId;
        private readonly string _deviceId;
        private readonly string _deviceName;

        public MonitoringReadCycle(
            HardwareMonitorService monitorService,
            MonitoringDisplayUpdater displayUpdater,
            Action<Exception> reportError,
            string providerId,
            string deviceId,
            string deviceName)
        {
            _monitorService = monitorService ?? throw new ArgumentNullException(nameof(monitorService));
            _displayUpdater = displayUpdater ?? throw new ArgumentNullException(nameof(displayUpdater));
            _reportError = reportError ?? throw new ArgumentNullException(nameof(reportError));
            _providerId = providerId ?? throw new ArgumentNullException(nameof(providerId));
            _deviceId = deviceId ?? throw new ArgumentNullException(nameof(deviceId));
            _deviceName = deviceName ?? throw new ArgumentNullException(nameof(deviceName));
        }

        public void Execute(CancellationToken cancellationToken, Func<bool> isDisposing)
        {
            if (cancellationToken.IsCancellationRequested || isDisposing())
            {
                return;
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                bool success = _monitorService.TryReadTemperature(
                    _providerId,
                    _deviceId,
                    out var reading,
                    out var error,
                    cancellationToken);

                if (cancellationToken.IsCancellationRequested || isDisposing())
                {
                    return;
                }

                _displayUpdater.Update(_deviceName, success, reading, error);
            }
            catch (OperationCanceledException ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    ReportAndUpdateUnavailable(ex, cancellationToken, isDisposing);
                }
            }
            catch (Exception ex)
            {
                ReportAndUpdateUnavailable(ex, cancellationToken, isDisposing);
            }
        }

        private void ReportAndUpdateUnavailable(Exception ex, CancellationToken cancellationToken, Func<bool> isDisposing)
        {
            if (cancellationToken.IsCancellationRequested || isDisposing())
            {
                return;
            }

            try
            {
                _reportError(ex);
            }
            catch
            {
            }

            if (cancellationToken.IsCancellationRequested || isDisposing())
            {
                return;
            }

            _displayUpdater.Update(_deviceName, false, null);
        }
    }
}