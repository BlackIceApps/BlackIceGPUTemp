using System;
using System.Collections.Generic;
using System.Threading;
using BlackIceGPUTemp.Monitoring;
using BlackIceGPUTemp.Monitoring.Abstractions;
using BlackIceGPUTemp.Monitoring.Providers.Amd;
using BlackIceGPUTemp.Monitoring.Providers.Amd.Native;
using Xunit;

namespace BlackIceGPUTemp.Tests
{
    public class GpuMonitoringSessionFactoryTests
    {
        private class UnavailableProvider : IGpuProvider, IGpuProviderProbe, IGpuDeviceEnumerator
        {
            public string Id => "unavailable";
            public string DisplayName => "Unavailable Provider";

            public ProviderProbeResult Probe(CancellationToken cancellationToken = default)
            {
                return new ProviderProbeResult(false, "Provider not available");
            }

            public IReadOnlyList<IGpuDevice> EnumerateDevices(CancellationToken cancellationToken = default)
            {
                return Array.Empty<IGpuDevice>();
            }
        }

        private class AvailableButNoDevicesProvider : IGpuProvider, IGpuProviderProbe, IGpuDeviceEnumerator
        {
            public string Id => "available-no-devices";
            public string DisplayName => "Available No Devices Provider";

            public ProviderProbeResult Probe(CancellationToken cancellationToken = default)
            {
                return new ProviderProbeResult(true, null);
            }

            public IReadOnlyList<IGpuDevice> EnumerateDevices(CancellationToken cancellationToken = default)
            {
                return Array.Empty<IGpuDevice>();
            }
        }

        private class AvailableWithDevicesProvider : IGpuProvider, IGpuProviderProbe, IGpuDeviceEnumerator
        {
            public string Id => "available-with-devices";
            public string DisplayName => "Available With Devices Provider";

            public ProviderProbeResult Probe(CancellationToken cancellationToken = default)
            {
                return new ProviderProbeResult(true, null);
            }

            public IReadOnlyList<IGpuDevice> EnumerateDevices(CancellationToken cancellationToken = default)
            {
                return new List<IGpuDevice>
                {
                    new FakeDevice("device-1", "Fake GPU")
                }.AsReadOnly();
            }
        }

        private class FakeDevice : IGpuDevice
        {
            public string Id { get; }
            public string Name { get; }
            public GpuVendor Vendor => GpuVendor.Amd;
            public GpuKind Kind => GpuKind.Discrete;

            public FakeDevice(string id, string name)
            {
                Id = id;
                Name = name;
            }

            public bool TryReadTemperature(out TemperatureReading? reading, out string? error, CancellationToken cancellationToken = default)
            {
                reading = new TemperatureReading(50.0, "GPU", DateTimeOffset.UtcNow);
                error = null;
                return true;
            }
        }

        private class TestProviderFactory : IGpuProviderFactory
        {
            private readonly IEnumerable<IGpuProvider> _providers;

            public TestProviderFactory(IEnumerable<IGpuProvider> providers)
            {
                _providers = providers;
            }

            public IReadOnlyList<IGpuProvider> CreateProviders()
            {
                return new List<IGpuProvider>(_providers).AsReadOnly();
            }
        }

        [Fact]
        public void Create_AllProvidersUnavailable_ReturnsFailureWithProvidersUnavailableMessage()
        {
            var factory = new GpuMonitoringSessionFactory(
                new TestProviderFactory(new List<IGpuProvider>
                {
                    new UnavailableProvider(),
                    new UnavailableProvider()
                }));

            var result = factory.Create(CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal("No GPU providers are available.", result.Message);
            Assert.Null(result.HardwareMonitorService);
            Assert.Null(result.ProviderId);
            Assert.Null(result.DeviceId);
            Assert.Null(result.DeviceName);
        }

        [Fact]
        public void Create_AtLeastOneProviderAvailableButNoDevices_ReturnsFailureWithNoDevicesMessage()
        {
            var factory = new GpuMonitoringSessionFactory(
                new TestProviderFactory(new List<IGpuProvider>
                {
                    new UnavailableProvider(),
                    new AvailableButNoDevicesProvider()
                }));

            var result = factory.Create(CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal("No supported GPU devices found.", result.Message);
            Assert.Null(result.HardwareMonitorService);
            Assert.Null(result.ProviderId);
            Assert.Null(result.DeviceId);
            Assert.Null(result.DeviceName);
        }

        [Fact]
        public void Create_AvailableProviderWithDevices_ReturnsSuccess()
        {
            var factory = new GpuMonitoringSessionFactory(
                new TestProviderFactory(new List<IGpuProvider>
                {
                    new AvailableWithDevicesProvider()
                }));

            var result = factory.Create(CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.HardwareMonitorService);
            Assert.Equal("available-with-devices", result.ProviderId);
            Assert.Equal("device-1", result.DeviceId);
            Assert.Equal("Fake GPU", result.DeviceName);
            Assert.Null(result.Message);
        }
    }
}