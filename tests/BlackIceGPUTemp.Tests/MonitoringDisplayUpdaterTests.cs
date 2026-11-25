using System;
using System.Globalization;
using System.Threading;
using BlackIceGPUTemp.Monitoring.Abstractions;
using BlackIceGPUTemp.Tray;
using Xunit;

namespace BlackIceGPUTemp.Tests
{
    public class MonitoringDisplayUpdaterTests
    {
        private readonly Action<Action> _dispatcher;
        private readonly Action<string> _updateTooltip;
        private readonly Action<string> _updateIcon;
        private readonly Action<Exception> _reportError;

        private Action? _capturedAction;
        private string? _tooltipResult;
        private string? _iconResult;
        private Exception? _reportedError;

        public MonitoringDisplayUpdaterTests()
        {
            _dispatcher = action => _capturedAction = action;
            _updateTooltip = text => _tooltipResult = text;
            _updateIcon = text => _iconResult = text;
            _reportError = ex => _reportedError = ex;
        }

        [Fact]
        public void Constructor_ThrowsWhenDispatcherIsNull()
        {
            Assert.Throws<ArgumentNullException>(() => new MonitoringDisplayUpdater(
                null!,
                _updateTooltip,
                _updateIcon,
                _reportError));
        }

        [Fact]
        public void Constructor_ThrowsWhenUpdateTooltipIsNull()
        {
            Assert.Throws<ArgumentNullException>(() => new MonitoringDisplayUpdater(
                _dispatcher,
                null!,
                _updateIcon,
                _reportError));
        }

        [Fact]
        public void Constructor_ThrowsWhenUpdateIconIsNull()
        {
            Assert.Throws<ArgumentNullException>(() => new MonitoringDisplayUpdater(
                _dispatcher,
                _updateTooltip,
                null!,
                _reportError));
        }

        [Fact]
        public void Constructor_ThrowsWhenReportErrorIsNull()
        {
            Assert.Throws<ArgumentNullException>(() => new MonitoringDisplayUpdater(
                _dispatcher,
                _updateTooltip,
                _updateIcon,
                null!));
        }

        [Fact]
        public void Update_DoesNotExecuteBeforeDispatcherActionInvoked()
        {
            var updater = new MonitoringDisplayUpdater(_dispatcher, _updateTooltip, _updateIcon, _reportError);
            var reading = new TemperatureReading(65.5, "GPU Core", DateTimeOffset.UtcNow);

            updater.Update("Test GPU", true, reading);

            Assert.Null(_tooltipResult);
            Assert.Null(_iconResult);
        }

        [Fact]
        public void Update_SuccessfulReading_PassesCorrectTooltipAndIconText()
        {
            var updater = new MonitoringDisplayUpdater(_dispatcher, _updateTooltip, _updateIcon, _reportError);
            var reading = new TemperatureReading(65.5, "GPU Core", DateTimeOffset.UtcNow);

            updater.Update("Test GPU", true, reading);
            _capturedAction!();

            Assert.Equal("Test GPU: 65.5°C", _tooltipResult);
            Assert.Equal("66", _iconResult);
        }

        [Fact]
        public void Update_FailedReading_PassesUnavailableTooltipAndDashIcon()
        {
            var updater = new MonitoringDisplayUpdater(_dispatcher, _updateTooltip, _updateIcon, _reportError);

            updater.Update("Test GPU", false, null);
            _capturedAction!();

            Assert.Equal("Test GPU: Temp unavailable", _tooltipResult);
            Assert.Equal("--", _iconResult);
        }

        [Fact]
        public void Update_TooltipThrows_ReportsErrorAndIconStillUpdates()
        {
            var tooltipException = new InvalidOperationException("Tooltip failed");
            var updateTooltip = new Action<string>(_ => throw tooltipException);

            var updater = new MonitoringDisplayUpdater(_dispatcher, updateTooltip, _updateIcon, _reportError);
            var reading = new TemperatureReading(65.5, "GPU Core", DateTimeOffset.UtcNow);

            updater.Update("Test GPU", true, reading);
            _capturedAction!();

            Assert.Equal(tooltipException, _reportedError);
            Assert.Equal("66", _iconResult);
        }

        [Fact]
        public void Update_IconThrows_ReportsError()
        {
            var iconException = new InvalidOperationException("Icon failed");
            var updateIcon = new Action<string>(_ => throw iconException);

            var updater = new MonitoringDisplayUpdater(_dispatcher, _updateTooltip, updateIcon, _reportError);
            var reading = new TemperatureReading(65.5, "GPU Core", DateTimeOffset.UtcNow);

            updater.Update("Test GPU", true, reading);
            _capturedAction!();

            Assert.Equal(iconException, _reportedError);
            Assert.Equal("Test GPU: 65.5°C", _tooltipResult);
        }

        [Fact]
        public void Update_BothThrow_BothErrorsReportedIndependently()
        {
            var tooltipException = new InvalidOperationException("Tooltip failed");
            var iconException = new InvalidOperationException("Icon failed");
            var updateTooltip = new Action<string>(_ => throw tooltipException);
            var updateIcon = new Action<string>(_ => throw iconException);

            var reportedErrors = new System.Collections.Generic.List<Exception>();
            var reportError = new Action<Exception>(ex => reportedErrors.Add(ex));

            var updater = new MonitoringDisplayUpdater(_dispatcher, updateTooltip, updateIcon, reportError);
            var reading = new TemperatureReading(65.5, "GPU Core", DateTimeOffset.UtcNow);

            updater.Update("Test GPU", true, reading);
            _capturedAction!();

            Assert.Equal(2, reportedErrors.Count);
            Assert.Contains(tooltipException, reportedErrors);
            Assert.Contains(iconException, reportedErrors);
        }

        [Fact]
        public void Update_ReportErrorThrows_DoesNotEscapeCallback()
        {
            var tooltipException = new InvalidOperationException("Tooltip failed");
            var updateTooltip = new Action<string>(_ => throw tooltipException);
            var reportError = new Action<Exception>(_ => throw new InvalidOperationException("Report failed"));

            var updater = new MonitoringDisplayUpdater(_dispatcher, updateTooltip, _updateIcon, reportError);
            var reading = new TemperatureReading(65.5, "GPU Core", DateTimeOffset.UtcNow);

            var exception = Record.Exception(() =>
            {
                updater.Update("Test GPU", true, reading);
                _capturedAction!();
            });

            Assert.Null(exception);
            Assert.Equal("66", _iconResult);
        }

        [Fact]
        public void Update_UsesDispatcherThreadCultureForFormatting()
        {
            var updater = new MonitoringDisplayUpdater(_dispatcher, _updateTooltip, _updateIcon, _reportError);
            var reading = new TemperatureReading(65.5, "GPU Core", DateTimeOffset.UtcNow);

            var originalCulture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = new CultureInfo("pl-PL");

            try
            {
                updater.Update("Test GPU", true, reading);

                Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
                _capturedAction!();

                Assert.Contains("65,5", _tooltipResult);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = originalCulture;
            }
        }
    }
}