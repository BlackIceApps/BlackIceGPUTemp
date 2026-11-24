using System;
using System.Collections.Generic;
using System.Threading;
using BlackIceGPUTemp.Monitoring;
using BlackIceGPUTemp.Monitoring.Abstractions;
using Xunit;

namespace BlackIceGPUTemp.Tests
{
    public class HardwareMonitorServiceTests
    {
        private class TestGpuDevice : IGpuDevice
        {
            public string Id { get; }
            public string ProviderId => "test-provider";
            public string Name => "Test Device";
            public GpuVendor Vendor => GpuVendor.Unknown;
            public GpuKind Kind => GpuKind.Discrete;

            private readonly bool _success;
            private readonly TemperatureReading? _reading;
            private readonly string? _error;

            public TestGpuDevice(string id, bool success, TemperatureReading? reading, string? error)
            {
                Id = id;
                _success = success;
                _reading = reading;
                _error = error;
            }

            public bool TryReadTemperature(out TemperatureReading? reading, out string? error, CancellationToken cancellationToken = default)
            {
                reading = _reading;
                error = _error;
                return _success;
            }
        }

        private class TrackingGpuDevice : IGpuDevice
        {
            public string Id { get; }
            public string ProviderId => "test-provider";
            public string Name => "Tracking Device";
            public GpuVendor Vendor => GpuVendor.Unknown;
            public GpuKind Kind => GpuKind.Discrete;

            public int ReadCallCount { get; private set; }

            public TrackingGpuDevice(string id)
            {
                Id = id;
            }

            public bool TryReadTemperature(out TemperatureReading? reading, out string? error, CancellationToken cancellationToken = default)
            {
                ReadCallCount++;
                reading = new TemperatureReading(50.0, "GPU Core", DateTimeOffset.UtcNow);
                error = null;
                return true;
            }
        }

        private class TestGpuProvider : IGpuDeviceEnumerator
        {
            public string Id { get; }
            public string DisplayName => Id;
            public int EnumerateDevicesCallCount { get; private set; }

            private readonly List<IGpuDevice> _devices;

            public TestGpuProvider(string id, IEnumerable<IGpuDevice>? devices = null)
            {
                Id = id;
                _devices = devices != null ? new List<IGpuDevice>(devices) : new List<IGpuDevice>();
            }

            public IReadOnlyList<IGpuDevice> EnumerateDevices(CancellationToken cancellationToken = default)
            {
                EnumerateDevicesCallCount++;
                return _devices.AsReadOnly();
            }
        }

        [Fact]
        public void Constructor_NullDeviceSelector_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new HardwareMonitorService((DeviceSelector)null!));
        }

        [Fact]
        public void Constructor_NullDirectDevice_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new HardwareMonitorService((IGpuDevice)null!));
        }

        [Fact]
        public void TryReadTemperature_DeviceNotFound_ReturnsFalseAndError()
        {
            var registry = new GpuProviderRegistry(Array.Empty<IGpuDeviceEnumerator>());
            var selector = new DeviceSelector(registry);
            var service = new HardwareMonitorService(selector);

            bool result = service.TryReadTemperature("nvidia", "gpu-1", out var reading, out var error, CancellationToken.None);

            Assert.False(result);
            Assert.Null(reading);
            Assert.False(string.IsNullOrWhiteSpace(error));
        }

        [Fact]
        public void TryReadTemperature_SuccessfulRead_ReturnsTrueAndReading()
        {
            var expectedReading = new TemperatureReading(75.5, "GPU Core", DateTimeOffset.UtcNow);
            var device = new TestGpuDevice("gpu-1", true, expectedReading, null);
            var provider = new TestGpuProvider("nvidia", new[] { device });
            var registry = new GpuProviderRegistry(new[] { provider });
            var selector = new DeviceSelector(registry);
            var service = new HardwareMonitorService(selector);

            bool result = service.TryReadTemperature("nvidia", "gpu-1", out var reading, out var error, CancellationToken.None);

            Assert.True(result);
            Assert.Same(expectedReading, reading);
            Assert.Null(error);
        }

        [Fact]
        public void TryReadTemperature_FailedReadWithCustomError_PreservesError()
        {
            string customError = "Hardware failure";
            var device = new TestGpuDevice("gpu-1", false, null, customError);
            var provider = new TestGpuProvider("nvidia", new[] { device });
            var registry = new GpuProviderRegistry(new[] { provider });
            var selector = new DeviceSelector(registry);
            var service = new HardwareMonitorService(selector);

            bool result = service.TryReadTemperature("nvidia", "gpu-1", out var reading, out var error, CancellationToken.None);

            Assert.False(result);
            Assert.Null(reading);
            Assert.Equal(customError, error);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void TryReadTemperature_FailedReadWithNullOrWhitespaceError_UsesFallbackError(string? deviceError)
        {
            var device = new TestGpuDevice("gpu-1", false, null, deviceError);
            var provider = new TestGpuProvider("nvidia", new[] { device });
            var registry = new GpuProviderRegistry(new[] { provider });
            var selector = new DeviceSelector(registry);
            var service = new HardwareMonitorService(selector);

            bool result = service.TryReadTemperature("nvidia", "gpu-1", out var reading, out var error, CancellationToken.None);

            Assert.False(result);
            Assert.Null(reading);
            Assert.False(string.IsNullOrWhiteSpace(error));
        }

        [Fact]
        public void TryReadTemperature_TrueWithNullReading_ReturnsFalseAndError()
        {
            var device = new TestGpuDevice("gpu-1", true, null, null);
            var provider = new TestGpuProvider("nvidia", new[] { device });
            var registry = new GpuProviderRegistry(new[] { provider });
            var selector = new DeviceSelector(registry);
            var service = new HardwareMonitorService(selector);

            bool result = service.TryReadTemperature("nvidia", "gpu-1", out var reading, out var error, CancellationToken.None);

            Assert.False(result);
            Assert.Null(reading);
            Assert.False(string.IsNullOrWhiteSpace(error));
        }

        [Fact]
        public void TryReadTemperature_DoesNotInvokeEnumerateDevicesWhenProviderNotFound()
        {
            var device = new TestGpuDevice("gpu-1", true, new TemperatureReading(50.0, "GPU Core", DateTimeOffset.UtcNow), null);
            var provider = new TestGpuProvider("nvidia", new[] { device });
            var registry = new GpuProviderRegistry(new[] { provider });
            var selector = new DeviceSelector(registry);
            var service = new HardwareMonitorService(selector);

            service.TryReadTemperature("amd", "gpu-1", out _, out _, CancellationToken.None);

            Assert.Equal(0, provider.EnumerateDevicesCallCount);
        }

        // Direct device mode tests

        [Fact]
        public void TryReadTemperature_DirectDeviceMode_SuccessfulRead_ReturnsTrueAndReading()
        {
            var expectedReading = new TemperatureReading(75.5, "GPU Core", DateTimeOffset.UtcNow);
            var device = new TestGpuDevice("gpu-1", true, expectedReading, null);
            var service = new HardwareMonitorService(device);

            bool result = service.TryReadTemperature("test-provider", "gpu-1", out var reading, out var error, CancellationToken.None);

            Assert.True(result);
            Assert.Same(expectedReading, reading);
            Assert.Null(error);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void TryReadTemperature_DirectDeviceMode_NullOrWhitespaceProviderId_ThrowsArgumentException(string? providerId)
        {
            var device = new TestGpuDevice("gpu-1", true, new TemperatureReading(50.0, "GPU Core", DateTimeOffset.UtcNow), null);
            var service = new HardwareMonitorService(device);

            Assert.Throws<ArgumentException>(() => service.TryReadTemperature(providerId!, "gpu-1", out _, out _, CancellationToken.None));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void TryReadTemperature_DirectDeviceMode_NullOrWhitespaceDeviceId_ThrowsArgumentException(string? deviceId)
        {
            var device = new TestGpuDevice("gpu-1", true, new TemperatureReading(50.0, "GPU Core", DateTimeOffset.UtcNow), null);
            var service = new HardwareMonitorService(device);

            Assert.Throws<ArgumentException>(() => service.TryReadTemperature("test-provider", deviceId!, out _, out _, CancellationToken.None));
        }

        [Fact]
        public void TryReadTemperature_DirectDeviceMode_MismatchedProviderId_ReturnsFalseAndError()
        {
            var device = new TestGpuDevice("gpu-1", true, new TemperatureReading(50.0, "GPU Core", DateTimeOffset.UtcNow), null);
            var service = new HardwareMonitorService(device);

            bool result = service.TryReadTemperature("wrong-provider", "gpu-1", out var reading, out var error, CancellationToken.None);

            Assert.False(result);
            Assert.Null(reading);
            Assert.Contains("Provider ID or Device ID does not match", error);
        }

        [Fact]
        public void TryReadTemperature_DirectDeviceMode_MismatchedDeviceId_ReturnsFalseAndError()
        {
            var device = new TestGpuDevice("gpu-1", true, new TemperatureReading(50.0, "GPU Core", DateTimeOffset.UtcNow), null);
            var service = new HardwareMonitorService(device);

            bool result = service.TryReadTemperature("test-provider", "wrong-device", out var reading, out var error, CancellationToken.None);

            Assert.False(result);
            Assert.Null(reading);
            Assert.Contains("Provider ID or Device ID does not match", error);
        }

        [Fact]
        public void TryReadTemperature_DirectDeviceMode_CancellationToken_Respected()
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();
            var device = new TestGpuDevice("gpu-1", true, new TemperatureReading(50.0, "GPU Core", DateTimeOffset.UtcNow), null);
            var service = new HardwareMonitorService(device);

            // Service should throw OperationCanceledException before calling device
            Assert.Throws<OperationCanceledException>(() => service.TryReadTemperature("test-provider", "gpu-1", out _, out _, cts.Token));
            // Device should not have been called since service throws first
        }
    }
}