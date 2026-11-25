using System;

namespace BlackIceGPUTemp.Tray
{
    internal sealed class MonitoringDisplayUpdater
    {
        private readonly Action<Action> _dispatcher;
        private readonly Action<string> _updateTooltip;
        private readonly Action<string> _updateIcon;
        private readonly Action<Exception> _reportError;

        public MonitoringDisplayUpdater(
            Action<Action> dispatcher,
            Action<string> updateTooltip,
            Action<string> updateIcon,
            Action<Exception> reportError)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _updateTooltip = updateTooltip ?? throw new ArgumentNullException(nameof(updateTooltip));
            _updateIcon = updateIcon ?? throw new ArgumentNullException(nameof(updateIcon));
            _reportError = reportError ?? throw new ArgumentNullException(nameof(reportError));
        }

        public void Update(string deviceName, bool success, TemperatureReading? reading)
        {
            _dispatcher(() =>
            {
                var displayState = TemperatureDisplayState.Create(deviceName, success, reading);

                try
                {
                    _updateTooltip(displayState.TooltipText);
                }
                catch (Exception ex)
                {
                    SafeReportError(ex);
                }

                try
                {
                    _updateIcon(displayState.IconText);
                }
                catch (Exception ex)
                {
                    SafeReportError(ex);
                }
            });
        }

        private void SafeReportError(Exception ex)
        {
            try
            {
                _reportError(ex);
            }
            catch
            {
            }
        }
    }
}