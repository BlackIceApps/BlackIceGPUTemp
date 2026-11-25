using System;
using System.Collections.Generic;
using System.Threading;
using BlackIceGPUTemp.Logging;
using BlackIceGPUTemp.Monitoring;
using BlackIceGPUTemp.Monitoring.Abstractions;
using BlackIceGPUTemp.Tray;
using Xunit;

namespace BlackIceGPUTemp.Tests
{
    public class MonitoringReadCycleTests
    {
        private readonly List<string> _tooltipResults = new();
        private readonly List<string> _iconResults = new();
        private readonly List<Exception> _reportedErrors = new();
        private readonly List<Exception> _loggedErrors = new();

        private Action<Action> _dispatcher = action => action();
        private Action<string> _updateTooltip = text => _tooltipResults.Add(text);
        private Action<string> _updateIcon = text => _iconResults.Add(text);
        private Action<Exception> _reportError = ex => _reportedErrors.Add(ex);

        private class FakeGpuDevice : IGpuDevice
        {
            public string Id { get; } = "test-device";
            public string ProviderId => "test-provider";
            public string Name => "Test GPU";
            public GpuVendor Vendor => GpuVendor.Unknown;
            public GpuKind Kind => GpuKind.Discrete;

            private readonly Func<CancellationToken, (bool success, TemperatureReading? reading, string? error)> _readFunc;

            public FakeGpuDevice(Func<CancellationToken, (bool success, TemperatureReading? reading, string? error)> readFunc)
            {
                _readFunc = readFunc;
            }

            public bool TryReadTemperature(out TemperatureReading? reading, out string? error, CancellationToken cancellationToken = default)
            {
                var result = _readFunc(cancellationToken);
                reading = result.reading;
                error = result.error;
                return result.success;
            }
        }

        private MonitoringReadCycle CreateCycle(
            HardwareMonitorService monitorService,
            string providerId = "test-provider",
            string deviceId = "test-device",
            string deviceName = "Test GPU")
        {
            var displayUpdater = new MonitoringDisplayUpdater(_dispatcher, _updateTooltip, _updateIcon, _reportError);
            return new MonitoringReadCycle(monitorService, displayUpdater, _reportError, providerId, deviceId, deviceName);
        }

        private HardwareMonitorService CreateMonitorService(Func<CancellationToken, (bool success, TemperatureReading? reading, string? error)> readFunc)
        {
            var device = new FakeGpuDevice(readFunc);
            return new HardwareMonitorService(device);
        }

        private TemperatureReading CreateReading(double celsius = 65.5)
        {
            return new TemperatureReading(celsius, "GPU Core", DateTimeOffset.UtcNow);
        }

        [Fact]
        public void Execute_SuccessfulRead_UpdatesTooltipAndIcon()
        {
            var reading = CreateReading(65.5);
            var monitorService = CreateMonitorService(_ => (true, reading, null));
            var cycle = CreateCycle(monitorService);

            cycle.Execute(CancellationToken.None, false);

            Assert.Single(_tooltipResults);
            Assert.Single(_iconResults);
            Assert.Equal("Test GPU: 65.5°C", _tooltipResults[0]);
            Assert.Equal("66", _iconResults[0]);
            Assert.Empty(_reportedErrors);
        }

        [Fact]
        public void Execute_FailedReadWithErrorMessage_UpdatesTooltipWithErrorAndDashIcon()
        {
            var monitorService = CreateMonitorService(_ => (false, null, "Device not found"));
            var cycle = CreateCycle(monitorService);

            cycle.Execute(CancellationToken.None, false);

            Assert.Single(_tooltipResults);
            Assert.Single(_iconResults);
            Assert.Equal("Test GPU: Device not found", _tooltipResults[0]);
            Assert.Equal("--", _iconResults[0]);
            Assert.Empty(_reportedErrors);
        }

        [Fact]
        public void Execute_FailedReadWithoutErrorMessage_UpdatesTooltipUnavailableAndDashIcon()
        {
            var monitorService = CreateMonitorService(_ => (false, null, ""));
            var cycle = CreateCycle(monitorService);

            cycle.Execute(CancellationToken.None, false);

            Assert.Single(_tooltipResults);
            Assert.Single(_iconResults);
            Assert.Equal("Test GPU: Temp unavailable", _tooltipResults[0]);
            Assert.Equal("--", _iconResults[0]);
            Assert.Empty(_reportedErrors);
        }

        [Fact]
        public void Execute_FailedReadWithNullError_UpdatesTooltipUnavailableAndDashIcon()
        {
            var monitorService = CreateMonitorService(_ => (false, null, null));
            var cycle = CreateCycle(monitorService);

            cycle.Execute(CancellationToken.None, false);

            Assert.Single(_tooltipResults);
            Assert.Single(_iconResults);
            Assert.Equal("Test GPU: Temp unavailable", _tooltipResults[0]);
            Assert.Equal("--", _iconResults[0]);
            Assert.Empty(_reportedErrors);
        }

        [Fact]
        public void Execute_FailedReadWithWhitespaceError_UpdatesTooltipUnavailableAndDashIcon()
        {
            var monitorService = CreateMonitorService(_ => (false, null, "   "));
            var cycle = CreateCycle(monitorService);

            cycle.Execute(CancellationToken.None, false);

            Assert.Single(_tooltipResults);
            Assert.Single(_iconResults);
            Assert.Equal("Test GPU: Temp unavailable", _tooltipResults[0]);
            Assert.Equal("--", _iconResults[0]);
            Assert.Empty(_reportedErrors);
        }

        [Fact]
        public void Execute_ExceptionDuringRead_ReportsErrorAndUpdatesUnavailable()
        {
            var exception = new InvalidOperationException("Hardware failure");
            var monitorService = CreateMonitorService(_ => throw exception);
            var cycle = CreateCycle(monitorService);

            cycle.Execute(CancellationToken.None, false);

            Assert.Single(_tooltipResults);
            Assert.Single(_iconResults);
            Assert.Equal("Test GPU: Temp unavailable", _tooltipResults[0]);
            Assert.Equal("--", _iconResults[0]);
            Assert.Single(_reportedErrors);
            Assert.Same(exception, _reportedErrors[0]);
        }

        [Fact]
        public void Execute_CancellationBeforeRead_NoReadNoUpdate()
        {
            var readCalled = false;
            var monitorService = CreateMonitorService(ct =>
            {
                readCalled = true;
                ct.ThrowIfCancellationRequested();
                return (true, CreateReading(), null);
            });
            var cycle = CreateCycle(monitorService);
            var cts = new CancellationTokenSource();
            cts.Cancel();

            cycle.Execute(cts.Token, false);

            Assert.False(readCalled);
            Assert.Empty(_tooltipResults);
            Assert.Empty(_iconResults);
            Assert.Empty(_reportedErrors);
        }

        [Fact]
        public void Execute_CancellationDuringRead_NoUpdate()
        {
            var monitorService = CreateMonitorService(ct =>
            {
                ct.ThrowIfCancellationRequested();
                return (true, CreateReading(), null);
            });
            var cycle = CreateCycle(monitorService);
            var cts = new CancellationTokenSource();
            cts.Cancel();

            cycle.Execute(cts.Token, false);

            Assert.Empty(_tooltipResults);
            Assert.Empty(_iconResults);
            Assert.Empty(_reportedErrors);
        }

        [Fact]
        public void Execute_IsDisposingTrue_NoReadNoUpdate()
        {
            var readCalled = false;
            var monitorService = CreateMonitorService(_ =>
            {
                readCalled = true;
                return (true, CreateReading(), null);
            });
            var cycle = CreateCycle(monitorService);

            cycle.Execute(CancellationToken.None, true);

            Assert.False(readCalled);
            Assert.Empty(_tooltipResults);
            Assert.Empty(_iconResults);
            Assert.Empty(_reportedErrors);
        }

        [Fact]
        public void Execute_IsDisposingBecomesTrueAfterRead_NoUpdate()
        {
            var readCompleted = false;
            var monitorService = CreateMonitorService(_ =>
            {
                readCompleted = true;
                return (true, CreateReading(), null);
            });
            var cycle = CreateCycle(monitorService);

            cycle.Execute(CancellationToken.None, true);

            Assert.False(readCompleted);
            Assert.Empty(_tooltipResults);
            Assert.Empty(_iconResults);
            Assert.Empty(_reportedErrors);
        }

        [Fact]
        public void Execute_ExceptionWhenDisposing_DoesNotReportErrorOrUpdate()
        {
            var exception = new InvalidOperationException("Hardware failure");
            var monitorService = CreateMonitorService(_ => throw exception);
            var cycle = CreateCycle(monitorService);

            cycle.Execute(CancellationToken.None, true);

            Assert.Empty(_tooltipResults);
            Assert.Empty(_iconResults);
            Assert.Empty(_reportedErrors);
        }

        [Fact]
        public void Execute_CancellationWhenDisposing_DoesNotReportErrorOrUpdate()
        {
            var monitorService = CreateMonitorService(ct => { ct.ThrowIfCancellationRequested(); return (true, CreateReading(), null); });
            var cycle = CreateCycle(monitorService);
            var cts = new CancellationTokenSource();
            cts.Cancel();

            cycle.Execute(cts.Token, true);

            Assert.Empty(_tooltipResults);
            Assert.Empty(_iconResults);
            Assert.Empty(_reportedErrors);
        }

        [Fact]
        public void Constructor_NullMonitorService_ThrowsArgumentNullException()
        {
            var displayUpdater = new MonitoringDisplayUpdater(_dispatcher, _updateTooltip, _updateIcon, _reportError);
            Assert.Throws<ArgumentNullException>(() => new MonitoringReadCycle(
                null!,
                displayUpdater,
                _reportError,
                "provider",
                "device",
                "Test GPU"));
        }

        [Fact]
        public void Constructor_NullDisplayUpdater_ThrowsArgumentNullException()
        {
            var monitorService = CreateMonitorService(_ => (true, CreateReading(), null));
            Assert.Throws<ArgumentNullException>(() => new MonitoringReadCycle(
                monitorService,
                null!,
                _reportError,
                "provider",
                "device",
                "Test GPU"));
        }

        [Fact]
        public void Constructor_NullReportError_ThrowsArgumentNullException()
        {
            var monitorService = CreateMonitorService(_ => (true, CreateReading(), null));
            var displayUpdater = new MonitoringDisplayUpdater(_dispatcher, _updateTooltip, _updateIcon, _reportError);
            Assert.Throws<ArgumentNullException>(() => new MonitoringReadCycle(
                monitorService,
                displayUpdater,
                null!,
                "provider",
                "device",
                "Test GPU"));
        }

        [Fact]
        public void Constructor_NullProviderId_ThrowsArgumentNullException()
        {
            var monitorService = CreateMonitorService(_ => (true, CreateReading(), null));
            var displayUpdater = new MonitoringDisplayUpdater(_dispatcher, _updateTooltip, _updateIcon, _reportError);
            Assert.Throws<ArgumentNullException>(() => new MonitoringReadCycle(
                monitorService,
                displayUpdater,
                _reportError,
                null!,
                "device",
                "Test GPU"));
        }

        [Fact]
        public void Constructor_NullDeviceId_ThrowsArgumentNullException()
        {
            var monitorService = CreateMonitorService(_ => (true, CreateReading(), null));
            var displayUpdater = new MonitoringDisplayUpdater(_dispatcher, _updateTooltip, _updateIcon, _reportError);
            Assert.Throws<ArgumentNullException>(() => new MonitoringReadCycle(
                monitorService,
                displayUpdater,
                _reportError,
                "provider",
                null!,
                "Test GPU"));
        }

        [Fact]
        public void Constructor_NullDeviceName_ThrowsArgumentNullException()
        {
            var monitorService = CreateMonitorService(_ => (true, CreateReading(), null));
            var displayUpdater = new MonitoringDisplayUpdater(_dispatcher, _updateTooltip, _updateIcon, _reportError);
            Assert.Throws<ArgumentNullException>(() => new MonitoringReadCycle(
                monitorService,
                displayUpdater,
                _reportError,
                "provider",
                "device",
                null!));
        }
    }
}