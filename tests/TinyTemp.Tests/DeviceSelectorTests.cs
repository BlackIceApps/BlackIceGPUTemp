using System;
using System.Collections.Generic;
using TinyTemp.Monitoring;
using TinyTemp.Monitoring.Abstractions;
using Xunit;

namespace TinyTemp.Tests
{
    public class DeviceSelectorTests
    {
        private class TestGpuDevice : IGpuDevice
        {
            public string Id { get; }
            public string Name => "Test Device";
            public GpuVendor Vendor => GpuVendor.Unknown;
            public GpuKind Kind => GpuKind.Discrete;

            public TestGpuDevice(string id)
            {
                Id = id;
            }

            public bool TryReadTemperature(out TemperatureReading? reading, out string? error)
            {
                reading = null;
                error = "Not supported";
                return false;
            }
        }

        private class TestGpuProvider : IGpuProvider
        {
            public string Id { get; }
            public string DisplayName => Id;
            public int EnumerateDevicesCallCount { get; private set; }
            public int ProbeCallCount { get; private set; }

            private readonly List<IGpuDevice> _devices;

            public TestGpuProvider(string id, IEnumerable<IGpuDevice>? devices = null)
            {
                Id = id;
                _devices = devices != null ? new List<IGpuDevice>(devices) : new List<IGpuDevice>();
            }

            public ProviderProbeResult Probe()
            {
                ProbeCallCount++;
                return new ProviderProbeResult(true, null);
            }

            public IReadOnlyList<IGpuDevice> EnumerateDevices()
            {
                EnumerateDevicesCallCount++;
                return _devices.AsReadOnly();
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
            var registry = new GpuProviderRegistry(Array.Empty<IGpuProvider>());
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

            var found1 = selector.FindDevice("nvidia", "gpu-001");
            var found2 = selector.FindDevice("NVIDIA", "GPU-001");

            Assert.Same(device, found1);
            Assert.Same(device, found2);
        }

        [Fact]
        public void FindDevice_UnknownProvider_ReturnsNullAndDoesNotEnumerate()
        {
            var provider = new TestGpuProvider("amd");
            var registry = new GpuProviderRegistry(new[] { provider });
            var selector = new DeviceSelector(registry);

            var result = selector.FindDevice("nvidia", "gpu-001");

            Assert.Null(result);
            Assert.Equal(0, provider.EnumerateDevicesCallCount);
        }

        [Fact]
        public void FindDevice_UnknownDevice_ReturnsNull()
        {
            var device = new TestGpuDevice("gpu-001");
            var provider = new TestGpuProvider("nvidia", new[] { device });
            var registry = new GpuProviderRegistry(new[] { provider });
            var selector = new DeviceSelector(registry);

            var result = selector.FindDevice("nvidia", "gpu-999");

            Assert.Null(result);
            Assert.Equal(1, provider.EnumerateDevicesCallCount);
        }

        [Fact]
        public void FindDevice_ProbeIsNotCalled()
        {
            var provider = new TestGpuProvider("nvidia", new[] { new TestGpuDevice("gpu-001") });
            var registry = new GpuProviderRegistry(new[] { provider });
            var selector = new DeviceSelector(registry);

            selector.FindDevice("nvidia", "gpu-001");

            Assert.Equal(0, provider.ProbeCallCount);
        }
    }
}
