using System;
using System.Collections.Generic;
using System.Threading;
using BlackIceGPUTemp.Monitoring;
using BlackIceGPUTemp.Monitoring.Abstractions;
using Xunit;

namespace BlackIceGPUTemp.Tests
{
    public class DeviceSelectorTests
    {
        private class TestGpuDevice : IGpuDevice
        {
            public string Id { get; }
            public string ProviderId => "test-provider";
            public string Name => "Test Device";
            public GpuVendor Vendor => GpuVendor.Unknown;
            public GpuKind Kind => GpuKind.Discrete;

            public TestGpuDevice(string id)
            {
                Id = id;
            }

            public bool TryReadTemperature(out TemperatureReading? reading, out string? error, CancellationToken cancellationToken = default)
            {
                reading = null;
                error = "Not supported";
                return false;
            }
        }

        private class TestGpuProvider : IGpuDeviceEnumerator
        {
            public string Id { get; }
            public string DisplayName => Id;
            public int TryEnumerateDevicesCallCount { get; private set; }

            private readonly List<IGpuDevice> _devices;
            private readonly bool _shouldSucceed;
            private readonly string? _errorMessage;

            public TestGpuProvider(string id, IEnumerable<IGpuDevice>? devices = null, bool shouldSucceed = true, string? errorMessage = null)
            {
                Id = id;
                _devices = devices != null ? new List<IGpuDevice>(devices) : new List<IGpuDevice>();
                _shouldSucceed = shouldSucceed;
                _errorMessage = errorMessage;
            }

            public bool TryEnumerateDevices(out IReadOnlyList<IGpuDevice> devices, out string? error, CancellationToken cancellationToken = default)
            {
                TryEnumerateDevicesCallCount++;
                if (!_shouldSucceed)
                {
                    devices = Array.Empty<IGpuDevice>();
                    error = _errorMessage ?? "Enumeration failed";
                    return false;
                }

                devices = _devices.AsReadOnly();
                error = null;
                return true;
            }
        }

        [Fact]
        public void Constructor_NullRegistry_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new DeviceSelector(null!));
        }

        [Theory]
        [InlineData(null, "dev1")]
        [InlineData("", "dev1")]
        [InlineData("   ", "dev1")]
        [InlineData("prov1", null)]
        [InlineData("prov1", "")]
        [InlineData("prov1", "   ")]
        public void FindDevice_InvalidArguments_ThrowsArgumentException(string? providerId, string? deviceId)
        {
            var registry = new GpuProviderRegistry(Array.Empty<IGpuDeviceEnumerator>());
            var selector = new DeviceSelector(registry);

            Assert.Throws<ArgumentException>(() => selector.FindDevice(providerId!, deviceId!));
        }

        [Fact]
        public void FindDevice_CaseInsensitiveMatch_ReturnsDevice()
        {
            var device = new TestGpuDevice("GPU-001");
            var provider = new TestGpuProvider("Nvidia", new[] { device });
            var registry = new GpuProviderRegistry(new[] { provider });
            var selector = new DeviceSelector(registry);

            var found1 = selector.FindDevice("nvidia", "gpu-001", CancellationToken.None);
            var found2 = selector.FindDevice("NVIDIA", "GPU-001", CancellationToken.None);

            Assert.Same(device, found1);
            Assert.Same(device, found2);
        }

        [Fact]
        public void FindDevice_UnknownProvider_ReturnsNullAndDoesNotEnumerate()
        {
            var provider = new TestGpuProvider("amd");
            var registry = new GpuProviderRegistry(new[] { provider });
            var selector = new DeviceSelector(registry);

            var result = selector.FindDevice("nvidia", "gpu-001", CancellationToken.None);

            Assert.Null(result);
            Assert.Equal(0, provider.TryEnumerateDevicesCallCount);
        }

        [Fact]
        public void FindDevice_UnknownDevice_ReturnsNull()
        {
            var device = new TestGpuDevice("gpu-001");
            var provider = new TestGpuProvider("nvidia", new[] { device });
            var registry = new GpuProviderRegistry(new[] { provider });
            var selector = new DeviceSelector(registry);

            var result = selector.FindDevice("nvidia", "gpu-999", CancellationToken.None);

            Assert.Null(result);
            Assert.Equal(1, provider.TryEnumerateDevicesCallCount);
        }

        [Fact]
        public void FindDevice_EnumerationReturnsEmptyList_ReturnsNull()
        {
            var provider = new TestGpuProvider("nvidia", Array.Empty<IGpuDevice>());
            var registry = new GpuProviderRegistry(new[] { provider });
            var selector = new DeviceSelector(registry);

            var result = selector.FindDevice("nvidia", "gpu-001", CancellationToken.None);

            Assert.Null(result);
            Assert.Equal(1, provider.TryEnumerateDevicesCallCount);
        }

        [Fact]
        public void FindDevice_EnumerationFails_ReturnsNull()
        {
            var provider = new TestGpuProvider("nvidia", new[] { new TestGpuDevice("gpu-001") }, shouldSucceed: false, errorMessage: "ADLX init failed");
            var registry = new GpuProviderRegistry(new[] { provider });
            var selector = new DeviceSelector(registry);

            var result = selector.FindDevice("nvidia", "gpu-001", CancellationToken.None);

            Assert.Null(result);
            Assert.Equal(1, provider.TryEnumerateDevicesCallCount);
        }

        [Fact]
        public void FindDevice_CancellationRequested_ThrowsOperationCanceledException()
        {
            var cts = new CancellationTokenSource();
            var provider = new TestGpuProvider("nvidia", new[] { new TestGpuDevice("gpu-001") });
            var registry = new GpuProviderRegistry(new[] { provider });
            var selector = new DeviceSelector(registry);

            cts.Cancel();

            Assert.Throws<OperationCanceledException>(() => selector.FindDevice("nvidia", "gpu-001", cts.Token));
        }
    }
}