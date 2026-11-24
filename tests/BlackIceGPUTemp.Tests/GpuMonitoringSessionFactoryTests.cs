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
            public string ProviderId => "available-with-devices";
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

        // Lifecycle-aware provider fakes
        private class LifecycleProvider : IGpuProvider, IGpuProviderProbe, IGpuDeviceEnumerator, IGpuProviderLifecycle
        {
            public string Id => "lifecycle-provider";
            public string DisplayName => "Lifecycle Provider";
            public bool ConnectCalled { get; private set; }
            public bool DisconnectCalled { get; private set; }
            public bool ProbeCalled { get; private set; }
            public bool EnumerateCalled { get; private set; }
            public string? ConnectError { get; set; }

            public bool Connect(out string? error)
            {
                ConnectCalled = true;
                error = ConnectError;
                return ConnectError == null;
            }

            public void Disconnect()
            {
                DisconnectCalled = true;
            }

            public ProviderProbeResult Probe(CancellationToken cancellationToken = default)
            {
                ProbeCalled = true;
                return new ProviderProbeResult(true, null);
            }

            public IReadOnlyList<IGpuDevice> EnumerateDevices(CancellationToken cancellationToken = default)
            {
                EnumerateCalled = true;
                return new List<IGpuDevice>
                {
                    new FakeDevice("device-1", "Fake GPU")
                }.AsReadOnly();
            }
        }

        private class LifecycleProviderNoDevices : IGpuProvider, IGpuProviderProbe, IGpuDeviceEnumerator, IGpuProviderLifecycle
        {
            public string Id => "lifecycle-provider-no-devices";
            public string DisplayName => "Lifecycle Provider No Devices";
            public bool ConnectCalled { get; private set; }
            public bool DisconnectCalled { get; private set; }
            public bool ProbeCalled { get; private set; }
            public bool EnumerateCalled { get; private set; }
            public string? ConnectError { get; set; }

            public bool Connect(out string? error)
            {
                ConnectCalled = true;
                error = ConnectError;
                return ConnectError == null;
            }

            public void Disconnect()
            {
                DisconnectCalled = true;
            }

            public ProviderProbeResult Probe(CancellationToken cancellationToken = default)
            {
                ProbeCalled = true;
                return new ProviderProbeResult(true, null);
            }

            public IReadOnlyList<IGpuDevice> EnumerateDevices(CancellationToken cancellationToken = default)
            {
                EnumerateCalled = true;
                return Array.Empty<IGpuDevice>();
            }
        }

        private class LifecycleProviderProbeFails : IGpuProvider, IGpuProviderProbe, IGpuDeviceEnumerator, IGpuProviderLifecycle
        {
            public string Id => "lifecycle-provider-probe-fails";
            public string DisplayName => "Lifecycle Provider Probe Fails";
            public bool ConnectCalled { get; private set; }
            public bool DisconnectCalled { get; private set; }
            public bool ProbeCalled { get; private set; }
            public string? ConnectError { get; set; }

            public bool Connect(out string? error)
            {
                ConnectCalled = true;
                error = ConnectError;
                return ConnectError == null;
            }

            public void Disconnect()
            {
                DisconnectCalled = true;
            }

            public ProviderProbeResult Probe(CancellationToken cancellationToken = default)
            {
                ProbeCalled = true;
                return new ProviderProbeResult(false, "Probe failed");
            }

            public IReadOnlyList<IGpuDevice> EnumerateDevices(CancellationToken cancellationToken = default)
            {
                return Array.Empty<IGpuDevice>();
            }
        }

        private class LifecycleProviderThrowsInProbe : IGpuProvider, IGpuProviderProbe, IGpuDeviceEnumerator, IGpuProviderLifecycle
        {
            public string Id => "lifecycle-throws-probe";
            public string DisplayName => "Lifecycle Throws Probe";
            public bool ConnectCalled { get; private set; }
            public bool DisconnectCalled { get; private set; }
            public bool ProbeCalled { get; private set; }
            public string? ConnectError { get; set; }

            public bool Connect(out string? error)
            {
                ConnectCalled = true;
                error = ConnectError;
                return ConnectError == null;
            }

            public void Disconnect()
            {
                DisconnectCalled = true;
            }

            public ProviderProbeResult Probe(CancellationToken cancellationToken = default)
            {
                ProbeCalled = true;
                throw new InvalidOperationException("Probe exception");
            }

            public IReadOnlyList<IGpuDevice> EnumerateDevices(CancellationToken cancellationToken = default)
            {
                return Array.Empty<IGpuDevice>();
            }
        }

        private class LifecycleProviderThrowsInEnumerate : IGpuProvider, IGpuProviderProbe, IGpuDeviceEnumerator, IGpuProviderLifecycle
        {
            public string Id => "lifecycle-throws-enumerate";
            public string DisplayName => "Lifecycle Throws Enumerate";
            public bool ConnectCalled { get; private set; }
            public bool DisconnectCalled { get; private set; }
            public bool ProbeCalled { get; private set; }
            public bool EnumerateCalled { get; private set; }
            public string? ConnectError { get; set; }

            public bool Connect(out string? error)
            {
                ConnectCalled = true;
                error = ConnectError;
                return ConnectError == null;
            }

            public void Disconnect()
            {
                DisconnectCalled = true;
            }

            public ProviderProbeResult Probe(CancellationToken cancellationToken = default)
            {
                ProbeCalled = true;
                return new ProviderProbeResult(true, null);
            }

            public IReadOnlyList<IGpuDevice> EnumerateDevices(CancellationToken cancellationToken = default)
            {
                EnumerateCalled = true;
                throw new InvalidOperationException("Enumerate exception");
            }
        }

        private class LifecycleProviderCancelsInProbe : IGpuProvider, IGpuProviderProbe, IGpuDeviceEnumerator, IGpuProviderLifecycle
        {
            public string Id => "lifecycle-cancels-probe";
            public string DisplayName => "Lifecycle Cancels Probe";
            public bool ConnectCalled { get; private set; }
            public bool DisconnectCalled { get; private set; }
            public bool ProbeCalled { get; private set; }
            public CancellationTokenSource? TokenSource { get; set; }
            public string? ConnectError { get; set; }

            public bool Connect(out string? error)
            {
                ConnectCalled = true;
                error = ConnectError;
                return ConnectError == null;
            }

            public void Disconnect()
            {
                DisconnectCalled = true;
            }

            public ProviderProbeResult Probe(CancellationToken cancellationToken = default)
            {
                ProbeCalled = true;
                TokenSource?.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
                return new ProviderProbeResult(false, "Cancelled");
            }

            public IReadOnlyList<IGpuDevice> EnumerateDevices(CancellationToken cancellationToken = default)
            {
                return Array.Empty<IGpuDevice>();
            }
        }

        private class LifecycleProviderCancelsInEnumerate : IGpuProvider, IGpuProviderProbe, IGpuDeviceEnumerator, IGpuProviderLifecycle
        {
            public string Id => "lifecycle-cancels-enumerate";
            public string DisplayName => "Lifecycle Cancels Enumerate";
            public bool ConnectCalled { get; private set; }
            public bool DisconnectCalled { get; private set; }
            public bool ProbeCalled { get; private set; }
            public bool EnumerateCalled { get; private set; }
            public CancellationTokenSource? TokenSource { get; set; }
            public string? ConnectError { get; set; }

            public bool Connect(out string? error)
            {
                ConnectCalled = true;
                error = ConnectError;
                return ConnectError == null;
            }

            public void Disconnect()
            {
                DisconnectCalled = true;
            }

            public ProviderProbeResult Probe(CancellationToken cancellationToken = default)
            {
                ProbeCalled = true;
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

        private class LifecycleProviderCancelsInEnumerate : IGpuProvider, IGpuProviderProbe, IGpuDeviceEnumerator, IGpuProviderLifecycle
        {
            public string Id => "lifecycle-cancels-enumerate";
            public string DisplayName => "Lifecycle Cancels Enumerate";
            public bool ConnectCalled { get; private set; }
            public bool DisconnectCalled { get; private set; }
            public bool ProbeCalled { get; private set; }
            public bool EnumerateCalled { get; private set; }
            public CancellationTokenSource? TokenSource { get; set; }
            public string? ConnectError { get; set; }

            public bool Connect(out string? error)
            {
                ConnectCalled = true;
                error = ConnectError;
                return ConnectError == null;
            }

            public void Disconnect()
            {
                DisconnectCalled = true;
            }

            public ProviderProbeResult Probe(CancellationToken cancellationToken = default)
            {
                ProbeCalled = true;
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

        // Provider with event recording for exact sequence verification
        private class LifecycleProviderWithEvents : IGpuProvider, IGpuProviderProbe, IGpuDeviceEnumerator, IGpuProviderLifecycle
        {
            public string Id => "lifecycle-with-events";
            public string DisplayName => "Lifecycle With Events";
            public List<string> Events { get; } = new List<string>();
            public string? ConnectError { get; set; }

            public bool Connect(out string? error)
            {
                Events.Add("Connect");
                error = ConnectError;
                return ConnectError == null;
            }

            public void Disconnect()
            {
                Events.Add("Disconnect");
            }

            public ProviderProbeResult Probe(CancellationToken cancellationToken = default)
            {
                Events.Add("Probe");
                return new ProviderProbeResult(true, null);
            }

            public IReadOnlyList<IGpuDevice> EnumerateDevices(CancellationToken cancellationToken = default)
            {
                Events.Add("Enumerate");
                return new List<IGpuDevice>
                {
                    new FakeDevice("device-1", "Fake GPU")
                }.AsReadOnly();
            }
        }

        private class LifecycleProviderDisconnectThrows : IGpuProvider, IGpuProviderProbe, IGpuDeviceEnumerator, IGpuProviderLifecycle
        {
            public string Id => "lifecycle-disconnect-throws";
            public string DisplayName => "Lifecycle Disconnect Throws";
            public bool ConnectCalled { get; private set; }
            public bool ProbeCalled { get; private set; }
            public bool EnumerateCalled { get; private set; }
            public string? ConnectError { get; set; }

            public bool Connect(out string? error)
            {
                ConnectCalled = true;
                error = ConnectError;
                return ConnectError == null;
            }

            public void Disconnect()
            {
                throw new InvalidOperationException("Disconnect exception");
            }

            public ProviderProbeResult Probe(CancellationToken cancellationToken = default)
            {
                ProbeCalled = true;
                return new ProviderProbeResult(true, null);
            }

            public IReadOnlyList<IGpuDevice> EnumerateDevices(CancellationToken cancellationToken = default)
            {
                EnumerateCalled = true;
                throw new InvalidOperationException("Enumerate exception");
            }
        }

        private class LifecycleProviderConnectThrows : IGpuProvider, IGpuProviderProbe, IGpuDeviceEnumerator, IGpuProviderLifecycle
        {
            public string Id => "lifecycle-connect-throws";
            public string DisplayName => "Lifecycle Connect Throws";
            public bool ConnectCalled { get; private set; }
            public bool ProbeCalled { get; private set; }
            public bool EnumerateCalled { get; private set; }
            public bool DisconnectCalled { get; private set; }

            public bool Connect(out string? error)
            {
                ConnectCalled = true;
                error = "Connect exception";
                throw new InvalidOperationException("Connect exception");
            }

            public void Disconnect()
            {
                DisconnectCalled = true;
            }

            public ProviderProbeResult Probe(CancellationToken cancellationToken = default)
            {
                ProbeCalled = true;
                return new ProviderProbeResult(true, null);
            }

            public IReadOnlyList<IGpuDevice> EnumerateDevices(CancellationToken cancellationToken = default)
            {
                EnumerateCalled = true;
                return new List<IGpuDevice>
                {
                    new FakeDevice("device-1", "Fake GPU")
                }.AsReadOnly();
            }
        }

        // Provider that cancels token inside Connect after returning success
        private class LifecycleProviderCancelsInConnect : IGpuProvider, IGpuProviderProbe, IGpuDeviceEnumerator, IGpuProviderLifecycle
        {
            public string Id => "lifecycle-cancels-in-connect";
            public string DisplayName => "Lifecycle Cancels In Connect";
            public bool ConnectCalled { get; private set; }
            public bool ProbeCalled { get; private set; }
            public bool DisconnectCalled { get; private set; }
            public CancellationTokenSource? TokenSource { get; set; }

            public bool Connect(out string? error)
            {
                ConnectCalled = true;
                error = null;
                TokenSource?.Cancel(); // Cancel token after Connect succeeds
                return true;
            }

            public void Disconnect()
            {
                DisconnectCalled = true;
            }

            public ProviderProbeResult Probe(CancellationToken cancellationToken = default)
            {
                ProbeCalled = true;
                return new ProviderProbeResult(true, null);
            }

            public IReadOnlyList<IGpuDevice> EnumerateDevices(CancellationToken cancellationToken = default)
            {
                return Array.Empty<IGpuDevice>();
            }
        }

        // Provider with Probe unavailable and Disconnect throwing
        private class LifecycleProviderProbeUnavailableDisconnectThrows : IGpuProvider, IGpuProviderProbe, IGpuDeviceEnumerator, IGpuProviderLifecycle
        {
            public string Id => "lifecycle-probe-unavailable-disconnect-throws";
            public string DisplayName => "Lifecycle Probe Unavailable Disconnect Throws";
            public bool ConnectCalled { get; private set; }
            public bool ProbeCalled { get; private set; }
            public int DisconnectCallCount { get; private set; }
            public string? ConnectError { get; set; }

            public bool Connect(out string? error)
            {
                ConnectCalled = true;
                error = ConnectError;
                return ConnectError == null;
            }

            public void Disconnect()
            {
                DisconnectCallCount++;
                throw new InvalidOperationException("Disconnect exception during cleanup");
            }

            public ProviderProbeResult Probe(CancellationToken cancellationToken = default)
            {
                ProbeCalled = true;
                return new ProviderProbeResult(false, "Probe unavailable");
            }

            public IReadOnlyList<IGpuDevice> EnumerateDevices(CancellationToken cancellationToken = default)
            {
                return Array.Empty<IGpuDevice>();
            }
        }

        [Fact]
        public void Create_LifecycleProvider_ConnectThenProbeThenEnumerateInOrder()
        {
            var provider = new LifecycleProviderWithEvents();
            var factory = new GpuMonitoringSessionFactory(
                new TestProviderFactory(new List<IGpuProvider> { provider }));

            var result = factory.Create(CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(new[] { "Connect", "Probe", "Enumerate" }, provider.Events);
            Assert.Equal("lifecycle-with-events", result.ProviderId);
            Assert.NotNull(result.ProviderLifecycle);
            Assert.Same(provider, result.ProviderLifecycle);
        }

        [Fact]
        public void Create_LifecycleProviderConnectFails_NoProbeNoEnumerate_NoDisconnect()
        {
            var provider = new LifecycleProvider { ConnectError = "Connect failed" };
            var factory = new GpuMonitoringSessionFactory(
                new TestProviderFactory(new List<IGpuProvider> { provider }));

            var result = factory.Create(CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.True(provider.ConnectCalled);
            Assert.False(provider.ProbeCalled);
            Assert.False(provider.EnumerateCalled);
            Assert.False(provider.DisconnectCalled); // Connect didn't establish session, no Disconnect
        }

        [Fact]
        public void Create_LifecycleProviderProbeFails_DisconnectCalled()
        {
            var provider = new LifecycleProviderProbeFails();
            var factory = new GpuMonitoringSessionFactory(
                new TestProviderFactory(new List<IGpuProvider> { provider }));

            var result = factory.Create(CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.True(provider.ConnectCalled);
            Assert.True(provider.ProbeCalled);
            Assert.False(provider.EnumerateCalled);
            Assert.True(provider.DisconnectCalled);
        }

        [Fact]
        public void Create_LifecycleProviderNoDevices_DisconnectCalled()
        {
            var provider = new LifecycleProviderNoDevices();
            var factory = new GpuMonitoringSessionFactory(
                new TestProviderFactory(new List<IGpuProvider> { provider }));

            var result = factory.Create(CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal("No supported GPU devices found.", result.Message);
            Assert.True(provider.ConnectCalled);
            Assert.True(provider.ProbeCalled);
            Assert.True(provider.EnumerateCalled);
            Assert.True(provider.DisconnectCalled);
        }

        [Fact]
        public void Create_LifecycleProviderSuccess_LifecycleNotDisconnectedAndPassedInResult()
        {
            var provider = new LifecycleProvider();
            var factory = new GpuMonitoringSessionFactory(
                new TestProviderFactory(new List<IGpuProvider> { provider }));

            var result = factory.Create(CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.False(provider.DisconnectCalled);
            Assert.NotNull(result.ProviderLifecycle);
            Assert.Same(provider, result.ProviderLifecycle);
        }

        [Fact]
        public void Create_LifecycleProviderThrowsInProbe_DisconnectCalledAndExceptionRethrown()
        {
            var provider = new LifecycleProviderThrowsInProbe();
            var factory = new GpuMonitoringSessionFactory(
                new TestProviderFactory(new List<IGpuProvider> { provider }));

            var ex = Assert.Throws<InvalidOperationException>(() => factory.Create(CancellationToken.None));
            Assert.Equal("Probe exception", ex.Message);

            Assert.True(provider.ConnectCalled);
            Assert.True(provider.ProbeCalled);
            Assert.False(provider.EnumerateCalled);
            Assert.True(provider.DisconnectCalled);
        }

        [Fact]
        public void Create_LifecycleProviderThrowsInEnumerate_DisconnectCalledAndExceptionRethrown()
        {
            var provider = new LifecycleProviderThrowsInEnumerate();
            var factory = new GpuMonitoringSessionFactory(
                new TestProviderFactory(new List<IGpuProvider> { provider }));

            var ex = Assert.Throws<InvalidOperationException>(() => factory.Create(CancellationToken.None));
            Assert.Equal("Enumerate exception", ex.Message);

            Assert.True(provider.ConnectCalled);
            Assert.True(provider.ProbeCalled);
            Assert.True(provider.EnumerateCalled);
            Assert.True(provider.DisconnectCalled);
        }

        [Fact]
        public void Create_LifecycleProviderCancelsInProbe_DisconnectCalledAndOperationCanceledRethrown()
        {
            var cts = new CancellationTokenSource();
            var provider = new LifecycleProviderCancelsInProbe { TokenSource = cts };
            var factory = new GpuMonitoringSessionFactory(
                new TestProviderFactory(new List<IGpuProvider> { provider }));

            Assert.Throws<OperationCanceledException>(() => factory.Create(cts.Token));

            Assert.True(provider.ConnectCalled);
            Assert.True(provider.ProbeCalled);
            Assert.False(provider.EnumerateCalled);
            Assert.True(provider.DisconnectCalled);
        }

        [Fact]
        public void Create_LifecycleProviderCancelsInEnumerate_DisconnectCalledAndOperationCanceledRethrown()
        {
            var cts = new CancellationTokenSource();
            var provider = new LifecycleProviderCancelsInEnumerate { TokenSource = cts };
            var factory = new GpuMonitoringSessionFactory(
                new TestProviderFactory(new List<IGpuProvider> { provider }));

            Assert.Throws<OperationCanceledException>(() => factory.Create(cts.Token));

            Assert.True(provider.ConnectCalled);
            Assert.True(provider.ProbeCalled);
            Assert.True(provider.EnumerateCalled);
            Assert.True(provider.DisconnectCalled);
        }

        [Fact]
        public void Create_NonLifecycleProvider_OldBehaviorPreserved()
        {
            var provider = new AvailableWithDevicesProvider();
            var factory = new GpuMonitoringSessionFactory(
                new TestProviderFactory(new List<IGpuProvider> { provider }));

            var result = factory.Create(CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Null(result.ProviderLifecycle);
            Assert.Equal("available-with-devices", result.ProviderId);
        }

        [Fact]
        public void Create_LifecycleProviderDisconnectThrowsDuringCleanup_OriginalExceptionRethrown()
        {
            var provider = new LifecycleProviderDisconnectThrows();
            var factory = new GpuMonitoringSessionFactory(
                new TestProviderFactory(new List<IGpuProvider> { provider }));

            var ex = Assert.Throws<InvalidOperationException>(() => factory.Create(CancellationToken.None));
            // Original exception from Probe/Enumerate should be rethrown, not the Disconnect exception
            Assert.Equal("Enumerate exception", ex.Message);

            Assert.True(provider.ConnectCalled);
            Assert.True(provider.ProbeCalled);
            Assert.True(provider.EnumerateCalled);
        }

        [Fact]
        public void Create_LifecycleProviderConnectThrows_ExceptionRethrown_NoDisconnect()
        {
            var provider = new LifecycleProviderConnectThrows();
            var factory = new GpuMonitoringSessionFactory(
                new TestProviderFactory(new List<IGpuProvider> { provider }));

            var ex = Assert.Throws<InvalidOperationException>(() => factory.Create(CancellationToken.None));
            Assert.Equal("Connect exception", ex.Message);

            Assert.True(provider.ConnectCalled);
            Assert.False(provider.ProbeCalled);
            Assert.False(provider.EnumerateCalled);
            Assert.False(provider.DisconnectCalled); // Connect threw, no session established, no Disconnect
        }

        [Fact]
        public void Create_CancellationAfterConnect_BeforeProbe_DisconnectCalledOnce()
        {
            var cts = new CancellationTokenSource();
            var provider = new LifecycleProviderCancelsInConnect { TokenSource = cts };
            var factory = new GpuMonitoringSessionFactory(
                new TestProviderFactory(new List<IGpuProvider> { provider }));

            // Connect returns success but cancels token inside
            Assert.Throws<OperationCanceledException>(() => factory.Create(cts.Token));

            // Connect was called, Probe was NOT called, Disconnect was called once
            Assert.True(provider.ConnectCalled);
            Assert.False(provider.ProbeCalled);
            Assert.True(provider.DisconnectCalled);
        }

        [Fact]
        public void Create_LifecycleProviderProbeUnavailable_DisconnectThrows_PropagatesException()
        {
            var provider = new LifecycleProviderProbeUnavailableDisconnectThrows();
            var factory = new GpuMonitoringSessionFactory(
                new TestProviderFactory(new List<IGpuProvider> { provider }));

            // Probe returns unavailable, Disconnect throws - exception should propagate
            var ex = Assert.Throws<InvalidOperationException>(() => factory.Create(CancellationToken.None));
            Assert.Equal("Disconnect exception during cleanup", ex.Message);

            Assert.True(provider.ConnectCalled);
            Assert.True(provider.ProbeCalled);
            Assert.Equal(1, provider.DisconnectCallCount);
        }

        [Fact]
        public void Create_DirectDeviceMode_MultipleReads_DoesNotReEnumerate()
        {
            // Provider that tracks how many times EnumerateDevices is called
            var provider = new TrackingEnumerateProvider();
            var factory = new GpuMonitoringSessionFactory(
                new TestProviderFactory(new List<IGpuProvider> { provider }));

            // Create session - this should call EnumerateDevices once
            var result = factory.Create(CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.HardwareMonitorService);
            Assert.Equal(1, provider.EnumerateDevicesCallCount); // EnumerateDevices called once during Create

            // Perform multiple reads through HardwareMonitorService
            var service = result.HardwareMonitorService!;
            var providerId = result.ProviderId!;
            var deviceId = result.DeviceId!;

            // First read
            bool result1 = service.TryReadTemperature(providerId, deviceId, out var reading1, out var error1, CancellationToken.None);
            // Second read
            bool result2 = service.TryReadTemperature(providerId, deviceId, out var reading2, out var error2, CancellationToken.None);
            // Third read
            bool result3 = service.TryReadTemperature(providerId, deviceId, out var reading3, out var error3, CancellationToken.None);

            Assert.True(result1);
            Assert.True(result2);
            Assert.True(result3);
            Assert.Equal(1, provider.EnumerateDevicesCallCount); // Still 1 - no re-enumeration on reads
        }

        private class TrackingEnumerateProvider : IGpuProvider, IGpuProviderProbe, IGpuDeviceEnumerator
        {
            public string Id => "tracking-enumerate";
            public string DisplayName => "Tracking Enumerate Provider";
            public int EnumerateDevicesCallCount { get; private set; }

            public ProviderProbeResult Probe(CancellationToken cancellationToken = default)
            {
                return new ProviderProbeResult(true, null);
            }

            public IReadOnlyList<IGpuDevice> EnumerateDevices(CancellationToken cancellationToken = default)
            {
                EnumerateDevicesCallCount++;
                return new List<IGpuDevice>
                {
                    new TrackingFakeDevice("device-1", "Tracking GPU")
                }.AsReadOnly();
            }
        }

        private class TrackingFakeDevice : IGpuDevice
        {
            public string Id { get; }
            public string ProviderId => "tracking-enumerate";
            public string Name { get; }
            public GpuVendor Vendor => GpuVendor.Amd;
            public GpuKind Kind => GpuKind.Discrete;

            public int ReadCallCount { get; private set; }

            public TrackingFakeDevice(string id, string name)
            {
                Id = id;
                Name = name;
            }

            public bool TryReadTemperature(out TemperatureReading? reading, out string? error, CancellationToken cancellationToken = default)
            {
                ReadCallCount++;
                reading = new TemperatureReading(50.0, "GPU", DateTimeOffset.UtcNow);
                error = null;
                return true;
            }
        }
    }
}