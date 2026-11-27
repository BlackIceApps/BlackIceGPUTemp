using System;
using BlackIceGPUTemp.Monitoring.Abstractions;

namespace BlackIceGPUTemp.Tray
{
    internal sealed class MonitoringDisplayUpdater
    {
        private readonly Action<Action> _dispatcher;
        private readonly Action<string> _updateTooltip;
        private readonly Action<string> _updateIcon;
        private readonly Action<Exception> _reportError;
        private string? _lastTooltipText;
        private string? _lastIconText;

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

        public void Update(string deviceName, bool success, TemperatureReading? reading, string? error = null)
        {
            _dispatcher(() =>
            {
                var displayState = TemperatureDisplayState.Create(deviceName, success, reading, error);

                if (_lastTooltipText == null || !string.Equals(_lastTooltipText, displayState.TooltipText, StringComparison.Ordinal))
                {
                    try
                    {
                        _updateTooltip(displayState.TooltipText);
                        _lastTooltipText = displayState.TooltipText;
                    }
                    catch (Exception ex)
                    {
                        SafeReportError(ex);
                    }
                }

                if (_lastIconText == null || !string.Equals(_lastIconText, displayState.IconText, StringComparison.Ordinal))
                {
                    try
                    {
                        _updateIcon(displayState.IconText);
                        _lastIconText = displayState.IconText;
                    }
                    catch (Exception ex)
                    {
                        SafeReportError(ex);
                    }
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