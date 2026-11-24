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
            public bool ProbeCalled { get; private set; }

            public ProviderProbeResult Probe(CancellationToken cancellationToken = default)
            {
                ProbeCalled = true;
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

        private class CancellingProbeProvider : IGpuProvider, IGpuProviderProbe, IGpuDeviceEnumerator
        {
            public string Id => "cancelling-probe";
            public string DisplayName => "Cancelling Probe Provider";
            public CancellationTokenSource? TokenSource { get; set; }
            public bool ProbeCalled { get; private set; }

            public ProviderProbeResult Probe(CancellationToken cancellationToken = default)
            {
                ProbeCalled = true;
                TokenSource?.Cancel();
                return new ProviderProbeResult(false, "Cancelled");
            }

            public IReadOnlyList<IGpuDevice> EnumerateDevices(CancellationToken cancellationToken = default)
            {
                return Array.Empty<IGpuDevice>();
            }
        }

        private class CancelDuringEnumerationProvider : IGpuProvider, IGpuProviderProbe, IGpuDeviceEnumerator
        {
            public string Id => "cancel-enumeration";
            public string DisplayName => "Cancel During Enumeration Provider";
            public CancellationTokenSource? TokenSource { get; set; }
            public bool EnumerateCalled { get; private set; }

            public ProviderProbeResult Probe(CancellationToken cancellationToken = default)
            {
                return new ProviderProbeResult(true, null);
            }

            public IReadOnlyList<IGpuDevice> EnumerateDevices(CancellationToken cancellationToken = default)
            {
                EnumerateCalled = true;
                TokenSource?.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
                return Array.Empty<IGpuDevice>();
            }
        }

        private class ThrowingEnumerateProvider : IGpuProvider, IGpuProviderProbe, IGpuDeviceEnumerator
        {
            public string Id => "throwing-enumerate";
            public string DisplayName => "Throwing Enumerate Provider";

            public ProviderProbeResult Probe(CancellationToken cancellationToken = default)
            {
                return new ProviderProbeResult(true, null);
            }

            public IReadOnlyList<IGpuDevice> EnumerateDevices(CancellationToken cancellationToken = default)
            {
                throw new InvalidOperationException("Enumerate failed");
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

        [Fact]
        public void Create_FirstProviderUnavailableSecondAvailable_ReturnsSuccessWithSecondProvider()
        {
            var factory = new GpuMonitoringSessionFactory(
                new TestProviderFactory(new List<IGpuProvider>
                {
                    new UnavailableProvider(),
                    new AvailableWithDevicesProvider()
                }));

            var result = factory.Create(CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.HardwareMonitorService);
            Assert.Equal("available-with-devices", result.ProviderId);
            Assert.Equal("device-1", result.DeviceId);
            Assert.Equal("Fake GPU", result.DeviceName);
        }

        [Fact]
        public void Create_FirstProviderAvailableNoDevicesSecondAvailableWithDevices_ReturnsSuccessWithSecondProvider()
        {
            var factory = new GpuMonitoringSessionFactory(
                new TestProviderFactory(new List<IGpuProvider>
                {
                    new AvailableButNoDevicesProvider(),
                    new AvailableWithDevicesProvider()
                }));

            var result = factory.Create(CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.HardwareMonitorService);
            Assert.Equal("available-with-devices", result.ProviderId);
            Assert.Equal("device-1", result.DeviceId);
            Assert.Equal("Fake GPU", result.DeviceName);
        }

        [Fact]
        public void Create_CancellationBeforeNextProvider_ThrowsOperationCanceledException()
        {
            var cts = new CancellationTokenSource();
            var firstProvider = new CancellingProbeProvider { TokenSource = cts };
            var secondProvider = new AvailableWithDevicesProvider();
            var factory = new GpuMonitoringSessionFactory(
                new TestProviderFactory(new List<IGpuProvider>
                {
                    firstProvider,
                    secondProvider
                }));

            // First provider cancels the token during its Probe
            Assert.Throws<OperationCanceledException>(() => factory.Create(cts.Token));

            // First provider's Probe was called
            Assert.True(firstProvider.ProbeCalled);
            // Second provider's Probe was NOT called
            Assert.False(secondProvider.ProbeCalled);
        }

        [Fact]
        public void Create_CancellationDuringEnumeration_ThrowsOperationCanceledException()
        {
            var cts = new CancellationTokenSource();
            var firstProvider = new CancelDuringEnumerationProvider { TokenSource = cts };
            var factory = new GpuMonitoringSessionFactory(
                new TestProviderFactory(new List<IGpuProvider>
                {
                    firstProvider,
                    new AvailableWithDevicesProvider()
                }));

            // First provider passes Probe, then cancels token during EnumerateDevices
            Assert.Throws<OperationCanceledException>(() => factory.Create(cts.Token));

            // EnumerateDevices was called and cancellation happened there
            Assert.True(firstProvider.EnumerateCalled);
        }

        [Fact]
        public void Create_ProviderProbeThrowsException_PropagatesException()
        {
            var probeThrowingProvider = new ThrowingProbeProvider();
            var factory = new GpuMonitoringSessionFactory(
                new TestProviderFactory(new List<IGpuProvider>
                {
                    probeThrowingProvider,
                    new AvailableWithDevicesProvider()
                }));

            Assert.Throws<InvalidOperationException>(() => factory.Create(CancellationToken.None));
        }

        [Fact]
        public void Create_ProviderEnumerateThrowsException_PropagatesException()
        {
            var factory = new GpuMonitoringSessionFactory(
                new TestProviderFactory(new List<IGpuProvider>
                {
                    new ThrowingEnumerateProvider(),
                    new AvailableWithDevicesProvider()
                }));

            Assert.Throws<InvalidOperationException>(() => factory.Create(CancellationToken.None));
        }

        private class ThrowingProbeProvider : IGpuProvider, IGpuProviderProbe, IGpuDeviceEnumerator
        {
            public string Id => "throwing-probe";
            public string DisplayName => "Throwing Probe Provider";

            public ProviderProbeResult Probe(CancellationToken cancellationToken = default)
            {
                throw new InvalidOperationException("Probe failed");
            }

            public IReadOnlyList<IGpuDevice> EnumerateDevices(CancellationToken cancellationToken = default)
            {
                return Array.Empty<IGpuDevice>();
            }
        }
    }
}