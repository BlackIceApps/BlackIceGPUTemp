using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using BlackIceGPUTemp.Monitoring.Abstractions;
using BlackIceGPUTemp.Monitoring.Providers.Amd;
using BlackIceGPUTemp.Monitoring.Providers.Amd.Native;
using Xunit;

namespace BlackIceGPUTemp.Tests
{
    public class AmdAdlxProviderTests
    {
        private class FakeAdlxNative : IAdlxNative, IDisposable
        {
            public int InitializeCalls { get; private set; }
            public int TerminateCalls { get; protected set; }
            public int ReleaseCalls { get; private set; }
            public List<int> ReleasedPointers { get; } = new List<int>();
            public List<int> ReleaseOrder { get; } = new List<int>();
            public List<int> ReleaseReturnCodes { get; } = new List<int>();
            public bool IsDisposed { get; protected set; }
            public int LastReleaseResult { get; private set; }
            public int ReleaseFailAtPointer { get; set; } = -1;
            public int ReleaseFailCount { get; set; }
            public bool ThrowOnRelease { get; set; } = false;
            public int ThrowOnReleasePointer { get; set; } = -1;
            public int ThrowOnReleaseCount { get; set; }
            public List<string> Events { get; } = new List<string>();

            public delegate int InitializeDelegate(out IntPtr system);
            public delegate int TerminateDelegate();
            public delegate int GetGpuListDelegate(IntPtr system, out IntPtr gpuList);
            public delegate int GetGpuListSizeDelegate(IntPtr gpuList, out int count);
            public delegate int GetGpuAtDelegate(IntPtr gpuList, int index, out IntPtr gpu);
            public delegate int GetGpuNameDelegate(IntPtr gpu, out string? name);
            public delegate int GetGpuUniqueIdDelegate(IntPtr gpu, out int uniqueId);
            public delegate int GetGpuTypeDelegate(IntPtr gpu, out int gpuType);
            public delegate int GetPerformanceMonitoringServicesDelegate(IntPtr system, out IntPtr services);
            public delegate int GetCurrentGpuMetricsDelegate(IntPtr services, IntPtr gpu, out IntPtr metrics);
            public delegate int GetSupportedGpuMetricsDelegate(IntPtr services, IntPtr gpu, out IntPtr metricsSupport);
            public delegate int IsSupportedGpuTemperatureDelegate(IntPtr metricsSupport, out bool supported);
            public delegate int GetGpuTemperatureDelegate(IntPtr metrics, out double celsius);
            public delegate int ReleaseDelegate(IntPtr adlxInterface);

            public InitializeDelegate? InitializeHandler { get; set; }
            public TerminateDelegate? TerminateHandler { get; set; }
            public GetGpuListDelegate? GetGpuListHandler { get; set; }
            public GetGpuListSizeDelegate? GetGpuListSizeHandler { get; set; }
            public GetGpuAtDelegate? GetGpuAtHandler { get; set; }
            public GetGpuNameDelegate? GetGpuNameHandler { get; set; }
            public GetGpuUniqueIdDelegate? GetGpuUniqueIdHandler { get; set; }
            public GetGpuTypeDelegate? GetGpuTypeHandler { get; set; }
            public GetPerformanceMonitoringServicesDelegate? GetPerfServicesHandler { get; set; }
            public GetCurrentGpuMetricsDelegate? GetCurrentMetricsHandler { get; set; }
            public GetSupportedGpuMetricsDelegate? GetSupportedMetricsHandler { get; set; }
            public IsSupportedGpuTemperatureDelegate? IsSupportedTempHandler { get; set; }
            public GetGpuTemperatureDelegate? GetGpuTempHandler { get; set; }
            public ReleaseDelegate? ReleaseHandler { get; set; }

            public List<string> GpuNames { get; set; } = new List<string> { "AMD Radeon RX 7900 XTX" };
            public List<int> GpuUniqueIds { get; set; } = new List<int> { 12345 };
            public List<int> GpuTypes { get; set; } = new List<int> { 2 };
            public List<bool> GpuTemperatureSupported { get; set; } = new List<bool> { true };
            public List<double> GpuTemperatures { get; set; } = new List<double> { 65.5 };

            public int Initialize(out IntPtr system)
            {
                InitializeCalls++;
                system = new IntPtr(0x1000);
                if (InitializeHandler != null)
                {
                    return InitializeHandler(out system);
                }
                return 0;
            }

            public virtual int Terminate()
            {
                TerminateCalls++;
                Events.Add("Terminate");
                if (TerminateHandler != null)
                {
                    return TerminateHandler();
                }
                return 0;
            }

            public int GetGpuList(IntPtr system, out IntPtr gpuList)
            {
                gpuList = new IntPtr(0x2000);
                if (GetGpuListHandler != null)
                {
                    return GetGpuListHandler(system, out gpuList);
                }
                return 0;
            }

            public int GetGpuListSize(IntPtr gpuList, out int count)
            {
                count = GpuNames.Count;
                if (GetGpuListSizeHandler != null)
                {
                    return GetGpuListSizeHandler(gpuList, out count);
                }
                return 0;
            }

            public int GetGpuAt(IntPtr gpuList, int index, out IntPtr gpu)
            {
                gpu = new IntPtr(0x3000 + index);
                if (GetGpuAtHandler != null)
                {
                    return GetGpuAtHandler(gpuList, index, out gpu);
                }
                return 0;
            }

            public int GetGpuName(IntPtr gpu, out string? name)
            {
                int index = (int)(gpu.ToInt64() - 0x3000);
                name = index >= 0 && index < GpuNames.Count ? GpuNames[index] : "Unknown GPU";
                if (GetGpuNameHandler != null)
                {
                    return GetGpuNameHandler(gpu, out name);
                }
                return 0;
            }

            public int GetGpuUniqueId(IntPtr gpu, out int uniqueId)
            {
                int index = (int)(gpu.ToInt64() - 0x3000);
                uniqueId = index >= 0 && index < GpuUniqueIds.Count ? GpuUniqueIds[index] : 12345;
                if (GetGpuUniqueIdHandler != null)
                {
                    return GetGpuUniqueIdHandler(gpu, out uniqueId);
                }
                return 0;
            }

            public int GetGpuType(IntPtr gpu, out int gpuType)
            {
                int index = (int)(gpu.ToInt64() - 0x3000);
                gpuType = index >= 0 && index < GpuTypes.Count ? GpuTypes[index] : 2;
                if (GetGpuTypeHandler != null)
                {
                    return GetGpuTypeHandler(gpu, out gpuType);
                }
                return 0;
            }

            public int GetPerformanceMonitoringServices(IntPtr system, out IntPtr services)
            {
                services = new IntPtr(0x4000);
                if (GetPerfServicesHandler != null)
                {
                    return GetPerfServicesHandler(system, out services);
                }
                return 0;
            }

            public int GetCurrentGpuMetrics(IntPtr services, IntPtr gpu, out IntPtr metrics)
            {
                int index = (int)(gpu.ToInt64() - 0x3000);
                metrics = new IntPtr(0x5000 + index);
                if (GetCurrentMetricsHandler != null)
                {
                    return GetCurrentMetricsHandler(services, gpu, out metrics);
                }
                return 0;
            }

            public int GetSupportedGpuMetrics(IntPtr services, IntPtr gpu, out IntPtr metricsSupport)
            {
                int index = (int)(gpu.ToInt64() - 0x6000);
                metricsSupport = new IntPtr(0x6000 + index);
                if (GetSupportedMetricsHandler != null)
                {
                    return GetSupportedMetricsHandler(services, gpu, out metricsSupport);
                }
                return 0;
            }

            public int IsSupportedGpuTemperature(IntPtr metricsSupport, out bool supported)
            {
                int gpuIndex = (int)(metricsSupport.ToInt64() - 0x6000);
                supported = gpuIndex >= 0 && gpuIndex < GpuTemperatureSupported.Count ? GpuTemperatureSupported[gpuIndex] : true;
                if (IsSupportedTempHandler != null)
                {
                    return IsSupportedTempHandler(metricsSupport, out supported);
                }
                return 0;
            }

            public int GetGpuTemperature(IntPtr metrics, out double celsius)
            {
                int gpuIndex = (int)(metrics.ToInt64() - 0x5000);
                celsius = gpuIndex >= 0 && gpuIndex < GpuTemperatures.Count ? GpuTemperatures[gpuIndex] : 65.5;
                if (GetGpuTempHandler != null)
                {
                    return GetGpuTempHandler(metrics, out celsius);
                }
                return 0;
            }

            public int Release(IntPtr adlxInterface)
            {
                ReleaseCalls++;
                int ptr = adlxInterface.ToInt32();
                ReleasedPointers.Add(ptr);
                ReleaseOrder.Add(ptr);
                Events.Add($"Release:0x{ptr:X}");

                int result = 0;
                if (ThrowOnRelease && ptr == ThrowOnReleasePointer)
                {
                    ThrowOnReleaseCount++;
                    throw new InvalidOperationException($"Simulated Release failure for 0x{ptr:X}");
                }
                if (ReleaseFailAtPointer == ptr)
                {
                    ReleaseFailCount++;
                    result = -1;
                }
                else if (ReleaseHandler != null)
                {
                    result = ReleaseHandler(adlxInterface);
                }
                ReleaseReturnCodes.Add(result);
                LastReleaseResult = result;
                return result;
            }

            public virtual void Dispose()
            {
                IsDisposed = true;
            }
        }

        private class TerminateFailingFake : FakeAdlxNative
        {
            public int TerminateHResult { get; set; } = unchecked((int)0x80004005);
            public bool ThrowOnTerminate { get; set; } = false;

            public override int Terminate()
            {
                TerminateCalls++;
                Events.Add("Terminate");
                if (ThrowOnTerminate)
                {
                    throw new InvalidOperationException("Simulated Terminate exception");
                }
                return TerminateHResult;
            }
        }

        private class TerminateSequenceFake : FakeAdlxNative
        {
            private readonly Queue<int> _terminateResults = new Queue<int>();
            private readonly Queue<bool> _throwOnTerminate = new Queue<bool>();

            public void EnqueueTerminate(int result, bool throwEx = false)
            {
                _terminateResults.Enqueue(result);
                _throwOnTerminate.Enqueue(throwEx);
            }

            public override int Terminate()
            {
                TerminateCalls++;
                Events.Add("Terminate");
                if (_throwOnTerminate.Count > 0 && _throwOnTerminate.Dequeue())
                {
                    throw new InvalidOperationException("Simulated Terminate exception");
                }
                if (_terminateResults.Count > 0)
                {
                    return _terminateResults.Dequeue();
                }
                return 0;
            }
        }

        private class DisposeThrowingFake : FakeAdlxNative
        {
            public bool ThrowOnDispose { get; set; } = false;

            public override void Dispose()
            {
                IsDisposed = true;
                if (ThrowOnDispose)
                {
                    throw new InvalidOperationException("Simulated Dispose exception");
                }
            }
        }

        private class DisposeFailsAndRetainsFake : FakeAdlxNative
        {
            public int DisposeCallCount { get; private set; }

            public override void Dispose()
            {
                DisposeCallCount++;
                throw new InvalidOperationException($"Simulated Dispose failure on call {DisposeCallCount}");
            }
        }

        private class DisposeThrowsOnceThenSucceedsFake : FakeAdlxNative
        {
            public int DisposeCallCount { get; private set; }
            private bool _disposedSuccessfully;

            public override void Dispose()
            {
                if (_disposedSuccessfully)
                {
                    return;
                }
                DisposeCallCount++;
                if (DisposeCallCount == 1)
                {
                    throw new InvalidOperationException("Simulated Dispose failure on first call");
                }
                _disposedSuccessfully = true;
                IsDisposed = true;
            }
        }

        private class TerminateFailsThenSucceedsThenDisposeThrowsFake : FakeAdlxNative
        {
            public new int TerminateCalls { get; private set; }
            public int DisposeCallCount { get; private set; }

            public override int Terminate()
            {
                TerminateCalls++;
                if (TerminateCalls == 1)
                {
                    return -1;
                }
                return 0;
            }

            public override void Dispose()
            {
                DisposeCallCount++;
                if (DisposeCallCount == 1)
                {
                    throw new InvalidOperationException("Simulated Dispose failure on first call");
                }
                IsDisposed = true;
            }
        }

        private class TerminateFailsThenSucceedsThenDisposeThrowsTwiceFake : FakeAdlxNative
        {
            public new int TerminateCalls { get; private set; }
            public int DisposeCallCount { get; private set; }

            public override int Terminate()
            {
                TerminateCalls++;
                if (TerminateCalls == 1)
                {
                    return -1;
                }
                return 0;
            }

            public override void Dispose()
            {
                DisposeCallCount++;
                throw new InvalidOperationException($"Simulated Dispose failure on call {DisposeCallCount}");
            }
        }

        [Fact]
        public void RetryTerminate_FailsThenSucceedsButDisposeThrows_RetainsReferencesAndRetriesDisposeOnNextCall()
        {
            var fake = new TerminateFailsThenSucceedsThenDisposeThrowsFake
            {
                InitializeHandler = (out IntPtr sys) =>
                {
                    sys = new IntPtr(0x1000);
                    return 0;
                }
            };
            var fake2 = new FakeAdlxNative();
            var factoryCallCount = 0;
            var provider = new AmdAdlxProvider(() =>
            {
                factoryCallCount++;
                return factoryCallCount == 1 ? fake : fake2;
            });

            var result1 = ((IGpuProviderProbe)provider).Probe();
            Assert.True(result1.IsAvailable);
            Assert.Equal(1, fake.TerminateCalls);
            Assert.Equal(0, fake.DisposeCallCount);
            Assert.False(fake.IsDisposed);

            var result2 = ((IGpuProviderProbe)provider).Probe();
            Assert.False(result2.IsAvailable);
            Assert.Equal(2, fake.TerminateCalls);
            Assert.Equal(1, fake.DisposeCallCount);
            Assert.False(fake.IsDisposed);
            Assert.Equal(0, fake2.InitializeCalls);
            Assert.Equal(1, factoryCallCount);

            var result3 = ((IGpuProviderProbe)provider).Probe();
            Assert.True(result3.IsAvailable);
            Assert.Equal(2, fake.TerminateCalls);
            Assert.Equal(2, fake.DisposeCallCount);
            Assert.True(fake.IsDisposed);
            Assert.Equal(1, fake2.TerminateCalls);
            Assert.True(fake2.IsDisposed);
            Assert.Equal(2, factoryCallCount);
        }

        [Fact]
        public void RetryTerminate_FailsThenSucceedsButDisposeThrowsTwice_RetainsReferencesAndDoesNotInitNew()
        {
            var fake = new TerminateFailsThenSucceedsThenDisposeThrowsTwiceFake
            {
                InitializeHandler = (out IntPtr sys) =>
                {
                    sys = new IntPtr(0x1000);
                    return 0;
                }
            };
            var fake2 = new FakeAdlxNative();
            var factoryCallCount = 0;
            var provider = new AmdAdlxProvider(() =>
            {
                factoryCallCount++;
                return factoryCallCount == 1 ? fake : fake2;
            });

            var result1 = ((IGpuProviderProbe)provider).Probe();
            Assert.True(result1.IsAvailable);
            Assert.Equal(1, fake.TerminateCalls);

            var result2 = ((IGpuProviderProbe)provider).Probe();
            Assert.False(result2.IsAvailable);
            Assert.Equal(2, fake.TerminateCalls);
            Assert.Equal(1, fake.DisposeCallCount);
            Assert.False(fake.IsDisposed);

            var result3 = ((IGpuProviderProbe)provider).Probe();
            Assert.False(result3.IsAvailable);
            Assert.Equal(2, fake.TerminateCalls);
            Assert.Equal(2, fake.DisposeCallCount);
            Assert.False(fake.IsDisposed);
            Assert.Equal(0, fake2.InitializeCalls);
            Assert.Equal(1, factoryCallCount);
        }

        [Fact]
        public void Initialize_ReturnsErrorCode_ThenDisposeThrows_RetainsReferencesAndRetriesOnNextCall()
        {
            var fake = new DisposeThrowsOnceThenSucceedsFake
            {
                InitializeHandler = (out IntPtr sys) =>
                {
                    sys = new IntPtr(0x1000);
                    return -5;
                }
            };
            var fake2 = new FakeAdlxNative();
            var factoryCallCount = 0;
            var provider = new AmdAdlxProvider(() =>
            {
                factoryCallCount++;
                return factoryCallCount == 1 ? fake : fake2;
            });

            var result1 = ((IGpuProviderProbe)provider).Probe();
            Assert.False(result1.IsAvailable);
            Assert.Equal(1, fake.DisposeCallCount);
            Assert.False(fake.IsDisposed);

            var result2 = ((IGpuProviderProbe)provider).Probe();
            Assert.True(result2.IsAvailable);
            Assert.Equal(2, fake.DisposeCallCount);
            Assert.True(fake.IsDisposed);
            Assert.Equal(1, fake2.TerminateCalls);
            Assert.True(fake2.IsDisposed);
            Assert.Equal(2, factoryCallCount);
        }

        [Fact]
        public void Initialize_ThrowsException_ThenDisposeThrows_RetainsReferencesAndRetriesOnNextCall()
        {
            var fake = new DisposeThrowsOnceThenSucceedsFake
            {
                InitializeHandler = (out IntPtr sys) =>
                {
                    sys = IntPtr.Zero;
                    throw new InvalidOperationException("Initialize failed");
                }
            };
            var fake2 = new FakeAdlxNative();
            var factoryCallCount = 0;
            var provider = new AmdAdlxProvider(() =>
            {
                factoryCallCount++;
                return factoryCallCount == 1 ? fake : fake2;
            });

            var result1 = ((IGpuProviderProbe)provider).Probe();
            Assert.False(result1.IsAvailable);
            Assert.Contains("Initialize failed", result1.Message);
            Assert.Equal(1, fake.DisposeCallCount);
            Assert.False(fake.IsDisposed);

            var result2 = ((IGpuProviderProbe)provider).Probe();
            Assert.True(result2.IsAvailable);
            Assert.Equal(2, fake.DisposeCallCount);
            Assert.True(fake.IsDisposed);
            Assert.Equal(1, fake2.TerminateCalls);
            Assert.True(fake2.IsDisposed);
            Assert.Equal(2, factoryCallCount);
        }

        [Fact]
        public void SessionCreation_NotInitialized_DisposeThrows_RetainsReferencesAndRetriesOnNextCall()
        {
            var fake = new DisposeThrowsOnceThenSucceedsFake
            {
                InitializeHandler = (out IntPtr sys) =>
                {
                    sys = new IntPtr(0x1000);
                    return -5;
                }
            };
            var fake2 = new FakeAdlxNative();
            var factoryCallCount = 0;
            var provider = new AmdAdlxProvider(() =>
            {
                factoryCallCount++;
                return factoryCallCount == 1 ? fake : fake2;
            });

            var result1 = ((IGpuProviderProbe)provider).Probe();
            Assert.False(result1.IsAvailable);
            Assert.Equal(0, fake.TerminateCalls);
            Assert.Equal(1, fake.DisposeCallCount);
            Assert.False(fake.IsDisposed);

            var result2 = ((IGpuProviderProbe)provider).Probe();
            Assert.True(result2.IsAvailable);
            Assert.Equal(0, fake.TerminateCalls);
            Assert.Equal(2, fake.DisposeCallCount);
            Assert.True(fake.IsDisposed);
            Assert.Equal(1, fake2.TerminateCalls);
            Assert.True(fake2.IsDisposed);
            Assert.Equal(2, factoryCallCount);
        }

        [Fact]
        public void SessionCreation_NotInitialized_DisposeThrowsTwice_RetainsReferencesAndDoesNotInitNew()
        {
            var fake = new DisposeFailsAndRetainsFake
            {
                InitializeHandler = (out IntPtr sys) =>
                {
                    sys = new IntPtr(0x1000);
                    return -5;
                }
            };
            var fake2 = new FakeAdlxNative();
            var factoryCallCount = 0;
            var provider = new AmdAdlxProvider(() =>
            {
                factoryCallCount++;
                return factoryCallCount == 1 ? fake : fake2;
            });

            var result1 = ((IGpuProviderProbe)provider).Probe();
            Assert.False(result1.IsAvailable);
            Assert.Equal(0, fake.TerminateCalls);
            Assert.Equal(1, fake.DisposeCallCount);
            Assert.False(fake.IsDisposed);

            var result2 = ((IGpuProviderProbe)provider).Probe();
            Assert.False(result2.IsAvailable);
            Assert.Equal(0, fake.TerminateCalls);
            Assert.Equal(2, fake.DisposeCallCount);
            Assert.False(fake.IsDisposed);
            Assert.Equal(0, fake2.InitializeCalls);
            Assert.Equal(1, factoryCallCount);
        }

        [Fact]
        public void ProviderMetadata_AreCorrect()
        {
            var provider = new AmdAdlxProvider(() => new FakeAdlxNative());
            Assert.Equal("amd-adlx", provider.Id);
            Assert.Equal("AMD ADLX", provider.DisplayName);
        }

        [Fact]
        public void Probe_WhenSupported_ReturnsAvailable()
        {
            var fake = new FakeAdlxNative();
            var provider = new AmdAdlxProvider(() => fake);

            var result = ((IGpuProviderProbe)provider).Probe();

            Assert.True(result.IsAvailable);
            Assert.Null(result.Message);
            Assert.Equal(1, fake.InitializeCalls);
            Assert.Equal(1, fake.TerminateCalls);
        }

        [Fact]
        public void Probe_WhenTemperatureNotSupported_ReturnsUnavailable()
        {
            var fake = new FakeAdlxNative
            {
                GpuTemperatureSupported = new List<bool> { false }
            };
            var provider = new AmdAdlxProvider(() => fake);

            var result = ((IGpuProviderProbe)provider).Probe();

            Assert.False(result.IsAvailable);
            Assert.Equal("No GPU supports temperature monitoring via ADLX.", result.Message);
        }

        [Fact]
        public void Probe_WhenInitializeReturnsZeroWithNullSystem_PerformsTerminateAndDisposes()
        {
            var fake = new FakeAdlxNative
            {
                InitializeHandler = (out IntPtr outSystem) =>
                {
                    outSystem = IntPtr.Zero;
                    return 0;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            var result = ((IGpuProviderProbe)provider).Probe();

            Assert.False(result.IsAvailable);
            Assert.Equal(1, fake.TerminateCalls);
            Assert.True(fake.IsDisposed);
            Assert.Equal("ADLX initialization succeeded but returned a null system interface.", result.Message);
        }

        [Fact]
        public void Probe_WhenGpuListFails_ReleasesGpuList()
        {
            var fake = new FakeAdlxNative
            {
                GetGpuListHandler = (IntPtr sys, out IntPtr list) =>
                {
                    list = IntPtr.Zero;
                    return -1;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            var result = ((IGpuProviderProbe)provider).Probe();

            Assert.False(result.IsAvailable);
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);
            Assert.Equal(1, fake.TerminateCalls);
        }

        [Fact]
        public void Probe_WhenGpuListSizeFails_ReleasesGpuList()
        {
            var fake = new FakeAdlxNative
            {
                GetGpuListSizeHandler = (IntPtr list, out int count) =>
                {
                    count = 0;
                    return -1;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            var result = ((IGpuProviderProbe)provider).Probe();

            Assert.False(result.IsAvailable);
            Assert.Contains(0x2000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);
            Assert.Equal(1, fake.TerminateCalls);
        }

        [Fact]
        public void Probe_ReleaseReturnCodeIsObserved()
        {
            var fake = new FakeAdlxNative
            {
                ReleaseHandler = (IntPtr ptr) => 42
            };
            var provider = new AmdAdlxProvider(() => fake);

            var result = ((IGpuProviderProbe)provider).Probe();

            Assert.True(result.IsAvailable);
            Assert.Contains(42, fake.ReleaseReturnCodes);
        }

        [Fact]
        public void Probe_ReleaseFailureDoesNotMaskOriginalResult()
        {
            var fake = new FakeAdlxNative
            {
                ReleaseHandler = (IntPtr ptr) => -1,
                GpuTemperatureSupported = new List<bool> { false }
            };
            var provider = new AmdAdlxProvider(() => fake);

            var result = ((IGpuProviderProbe)provider).Probe();

            Assert.False(result.IsAvailable);
            Assert.Equal("No GPU supports temperature monitoring via ADLX.", result.Message);
        }

        [Fact]
        public void EnumerateDevices_ReturnsValidDevices()
        {
            var fake = new FakeAdlxNative();
            var provider = new AmdAdlxProvider(() => fake);

            var devices = ((IGpuDeviceEnumerator)provider).EnumerateDevices();

            Assert.Single(devices);
            var device = devices[0];
            Assert.Equal("amd-adlx:12345", device.Id);
            Assert.Equal("AMD Radeon RX 7900 XTX", device.Name);
            Assert.Equal(GpuVendor.Amd, device.Vendor);
            Assert.Equal(GpuKind.Discrete, device.Kind);
        }

        [Fact]
        public void EnumerateDevices_GpuKindIntegrated_MapsCorrectly()
        {
            var fake = new FakeAdlxNative
            {
                GpuTypes = new List<int> { 1 }
            };
            var provider = new AmdAdlxProvider(() => fake);

            var devices = ((IGpuDeviceEnumerator)provider).EnumerateDevices();

            Assert.Single(devices);
            Assert.Equal(GpuKind.Integrated, devices[0].Kind);
        }

        [Fact]
        public void EnumerateDevices_GpuKindDiscrete_MapsCorrectly()
        {
            var fake = new FakeAdlxNative
            {
                GpuTypes = new List<int> { 2 }
            };
            var provider = new AmdAdlxProvider(() => fake);

            var devices = ((IGpuDeviceEnumerator)provider).EnumerateDevices();

            Assert.Single(devices);
            Assert.Equal(GpuKind.Discrete, devices[0].Kind);
        }

        [Fact]
        public void EnumerateDevices_GpuKindUnknown_MapsCorrectly()
        {
            var fake = new FakeAdlxNative
            {
                GpuTypes = new List<int> { 0 }
            };
            var provider = new AmdAdlxProvider(() => fake);

            var devices = ((IGpuDeviceEnumerator)provider).EnumerateDevices();

            Assert.Single(devices);
            Assert.Equal(GpuKind.Unknown, devices[0].Kind);
        }

        [Fact]
        public void EnumerateDevices_GpuTypeReadError_DefaultsToUnknown()
        {
            var fake = new FakeAdlxNative
            {
                GetGpuTypeHandler = (IntPtr gpu, out int gpuType) =>
                {
                    gpuType = 0;
                    return -1;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            var devices = ((IGpuDeviceEnumerator)provider).EnumerateDevices();

            Assert.Single(devices);
            Assert.Equal(GpuKind.Unknown, devices[0].Kind);
        }

        [Fact]
        public void EnumerateDevices_GpuTypeUnknownValue_DefaultsToUnknown()
        {
            var fake = new FakeAdlxNative
            {
                GpuTypes = new List<int> { 99 }
            };
            var provider = new AmdAdlxProvider(() => fake);

            var devices = ((IGpuDeviceEnumerator)provider).EnumerateDevices();

            Assert.Single(devices);
            Assert.Equal(GpuKind.Unknown, devices[0].Kind);
        }

        [Fact]
        public void EnumerateDevices_GpuTypeReadThrowsException_DefaultsToUnknown()
        {
            var fake = new FakeAdlxNative
            {
                GetGpuTypeHandler = (IntPtr gpu, out int gpuType) =>
                {
                    gpuType = 0;
                    throw new InvalidOperationException("Simulated GetGpuType failure");
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            var devices = ((IGpuDeviceEnumerator)provider).EnumerateDevices();

            Assert.Single(devices);
            Assert.Equal(GpuKind.Unknown, devices[0].Kind);
        }

        [Fact]
        public void EnumerateDevices_MultipleGpus_DifferentTypes()
        {
            var fake = new FakeAdlxNative
            {
                GpuNames = new List<string> { "Integrated GPU", "Discrete GPU" },
                GpuUniqueIds = new List<int> { 11111, 22222 },
                GpuTypes = new List<int> { 1, 2 },
                GpuTemperatureSupported = new List<bool> { true, true }
            };
            var provider = new AmdAdlxProvider(() => fake);

            var devices = ((IGpuDeviceEnumerator)provider).EnumerateDevices();

            Assert.Equal(2, devices.Count);
            Assert.Equal(GpuKind.Integrated, devices[0].Kind);
            Assert.Equal(GpuKind.Discrete, devices[1].Kind);
        }

        [Fact]
        public void EnumerateDevices_WhenInitializeFailsWithNullSystem_ReleasesNothing()
        {
            var fake = new FakeAdlxNative
            {
                InitializeHandler = (out IntPtr sys) =>
                {
                    sys = IntPtr.Zero;
                    return -1;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            var devices = ((IGpuDeviceEnumerator)provider).EnumerateDevices();

            Assert.Empty(devices);
            Assert.Equal(1, fake.InitializeCalls);
            Assert.Equal(0, fake.ReleaseCalls);
        }

        [Fact]
        public void EnumerateDevices_WhenInitializeFailsWithNonZeroSystem_ZeroesSystem()
        {
            var fake = new FakeAdlxNative
            {
                InitializeHandler = (out IntPtr sys) =>
                {
                    sys = new IntPtr(0x1000);
                    return -1;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            var devices = ((IGpuDeviceEnumerator)provider).EnumerateDevices();

            Assert.Empty(devices);
            Assert.Equal(1, fake.InitializeCalls);
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);
        }

        [Fact]
        public void EnumerateDevices_WhenGpuListFails_ReleasesGpuList()
        {
            var fake = new FakeAdlxNative
            {
                GetGpuListHandler = (IntPtr sys, out IntPtr list) =>
                {
                    list = IntPtr.Zero;
                    return -1;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            var devices = ((IGpuDeviceEnumerator)provider).EnumerateDevices();

            Assert.Empty(devices);
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);
        }

        [Fact]
        public void EnumerateDevices_WhenGpuListSizeFails_ReleasesGpuList()
        {
            var fake = new FakeAdlxNative
            {
                GetGpuListSizeHandler = (IntPtr list, out int count) =>
                {
                    count = 0;
                    return -1;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            var devices = ((IGpuDeviceEnumerator)provider).EnumerateDevices();

            Assert.Empty(devices);
            Assert.Contains(0x2000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);
        }

        [Fact]
        public void EnumerateDevices_ReleaseFailureDoesNotMaskOriginalResult()
        {
            var fake = new FakeAdlxNative
            {
                ReleaseHandler = (IntPtr ptr) => -1
            };
            var provider = new AmdAdlxProvider(() => fake);

            var devices = ((IGpuDeviceEnumerator)provider).EnumerateDevices();

            Assert.Single(devices);
        }

        [Fact]
        public void Device_TryReadTemperature_ReturnsReading()
        {
            var fake = new FakeAdlxNative();
            var provider = new AmdAdlxProvider(() => fake);

            var devices = ((IGpuDeviceEnumerator)provider).EnumerateDevices();
            var device = devices[0];

            bool success = device.TryReadTemperature(out var reading, out var error);

            Assert.True(success);
            Assert.Null(error);
            Assert.NotNull(reading);
            Assert.Equal(65.5, reading.Celsius);
            Assert.Equal("GPU Edge", reading.SensorName);
        }

        [Fact]
        public void TryReadTemperature_WithValidDeviceId_ReturnsReading()
        {
            var fake = new FakeAdlxNative();
            var provider = new AmdAdlxProvider(() => fake);

            bool success = ((IGpuTemperatureReader)provider).TryReadTemperature("amd-adlx:12345", out var reading, out var error);

            Assert.True(success);
            Assert.Null(error);
            Assert.NotNull(reading);
            Assert.Equal(65.5, reading.Celsius);
        }

        [Fact]
        public void TryReadTemperature_WithInvalidDeviceId_ReturnsFalse()
        {
            var fake = new FakeAdlxNative();
            var provider = new AmdAdlxProvider(() => fake);

            bool success = ((IGpuTemperatureReader)provider).TryReadTemperature("invalid-id", out var reading, out var error);

            Assert.False(success);
            Assert.NotNull(error);
            Assert.Contains("Invalid device ID format", error!);
        }

        [Fact]
        public void TryReadTemperature_WithEmptyDeviceId_ReturnsFalse()
        {
            var fake = new FakeAdlxNative();
            var provider = new AmdAdlxProvider(() => fake);

            bool success = ((IGpuTemperatureReader)provider).TryReadTemperature("", out var reading, out var error);

            Assert.False(success);
            Assert.NotNull(error);
        }

        [Fact]
        public void TryReadTemperatureInternal_WhenInitializeFails_ReleasesNothing()
        {
            var fake = new FakeAdlxNative
            {
                InitializeHandler = (out IntPtr sys) =>
                {
                    sys = IntPtr.Zero;
                    return -1;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = provider.TryReadTemperatureInternal(12345, out var reading, out var error);

            Assert.False(success);
            Assert.NotNull(error);
            Assert.Equal(0, fake.ReleaseCalls);
        }

        [Fact]
        public void TryReadTemperatureInternal_WhenGpuListFails_ReleasesGpuList()
        {
            var fake = new FakeAdlxNative
            {
                GetGpuListHandler = (IntPtr sys, out IntPtr list) =>
                {
                    list = IntPtr.Zero;
                    return -1;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = provider.TryReadTemperatureInternal(12345, out var reading, out var error);

            Assert.False(success);
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);
        }

        [Fact]
        public void TryReadTemperatureInternal_WhenGpuListSizeFails_ReleasesGpuList()
        {
            var fake = new FakeAdlxNative
            {
                GetGpuListSizeHandler = (IntPtr list, out int count) =>
                {
                    count = 0;
                    return -1;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = provider.TryReadTemperatureInternal(12345, out var reading, out var error);

            Assert.False(success);
            Assert.Contains(0x2000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);
        }

        [Fact]
        public void TryReadTemperatureInternal_WhenTargetGpuNotFound_ReleasesAllLoopGpusAndGpuList()
        {
            var fake = new FakeAdlxNative
            {
                GpuUniqueIds = new List<int> { 11111, 22222 },
                GpuNames = new List<string> { "GPU1", "GPU2" }
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = provider.TryReadTemperatureInternal(99999, out var reading, out var error);

            Assert.False(success);
            Assert.Contains(0x3000, fake.ReleasedPointers);
            Assert.Contains(0x3001, fake.ReleasedPointers);
            Assert.Contains(0x2000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);
        }

        [Fact]
        public void TryReadTemperatureInternal_WhenGetPerfServicesFails_ReleasesTargetGpuAndGpuList()
        {
            var fake = new FakeAdlxNative
            {
                GetPerfServicesHandler = (IntPtr sys, out IntPtr svc) =>
                {
                    svc = IntPtr.Zero;
                    return -1;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = provider.TryReadTemperatureInternal(12345, out var reading, out var error);

            Assert.False(success);
            Assert.Contains(0x3000, fake.ReleasedPointers);
            Assert.Contains(0x2000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);
        }

        [Fact]
        public void TryReadTemperatureInternal_WhenGetSupportedMetricsFails_ReleasesServicesTargetGpuAndGpuList()
        {
            var fake = new FakeAdlxNative
            {
                GetSupportedMetricsHandler = (IntPtr svc, IntPtr gpu, out IntPtr ms) =>
                {
                    ms = IntPtr.Zero;
                    return -1;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = provider.TryReadTemperatureInternal(12345, out var reading, out var error);

            Assert.False(success);
            Assert.Contains(0x4000, fake.ReleasedPointers);
            Assert.Contains(0x3000, fake.ReleasedPointers);
            Assert.Contains(0x2000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);
        }

        [Fact]
        public void TryReadTemperatureInternal_WhenIsSupportedTempFails_ReleasesMetricsSupportServicesTargetGpuAndGpuList()
        {
            var fake = new FakeAdlxNative
            {
                IsSupportedTempHandler = (IntPtr ms, out bool supp) =>
                {
                    supp = false;
                    return -1;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = provider.TryReadTemperatureInternal(12345, out var reading, out var error);

            Assert.False(success);
            Assert.Contains(0x6000, fake.ReleasedPointers);
            Assert.Contains(0x4000, fake.ReleasedPointers);
            Assert.Contains(0x3000, fake.ReleasedPointers);
            Assert.Contains(0x2000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);
        }

        [Fact]
        public void TryReadTemperatureInternal_WhenGetCurrentMetricsFails_ReleasesMetricsSupportServicesTargetGpuAndGpuList()
        {
            var fake = new FakeAdlxNative
            {
                GetCurrentMetricsHandler = (IntPtr svc, IntPtr gpu, out IntPtr met) =>
                {
                    met = IntPtr.Zero;
                    return -1;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = provider.TryReadTemperatureInternal(12345, out var reading, out var error);

            Assert.False(success);
            Assert.Contains(0x6000, fake.ReleasedPointers);
            Assert.Contains(0x4000, fake.ReleasedPointers);
            Assert.Contains(0x3000, fake.ReleasedPointers);
            Assert.Contains(0x2000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);
        }

        [Fact]
        public void TryReadTemperatureInternal_WhenGetTemperatureFails_ReleasesMetricsMetricsSupportServicesTargetGpuAndGpuList()
        {
            var fake = new FakeAdlxNative
            {
                GetGpuTempHandler = (IntPtr met, out double temp) =>
                {
                    temp = 0;
                    return -1;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = provider.TryReadTemperatureInternal(12345, out var reading, out var error);

            Assert.False(success);
            Assert.Contains(0x5000, fake.ReleasedPointers);
            Assert.Contains(0x6000, fake.ReleasedPointers);
            Assert.Contains(0x4000, fake.ReleasedPointers);
            Assert.Contains(0x3000, fake.ReleasedPointers);
            Assert.Contains(0x2000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);
        }

        [Fact]
        public void TryReadTemperatureInternal_ReleaseReturnCodeIsObserved()
        {
            var fake = new FakeAdlxNative
            {
                ReleaseHandler = (IntPtr ptr) => 42
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = provider.TryReadTemperatureInternal(12345, out var reading, out var error);

            Assert.True(success);
            Assert.Contains(42, fake.ReleaseReturnCodes);
        }

        [Fact]
        public void TryReadTemperatureInternal_ReleaseFailureDoesNotMaskOriginalResult()
        {
            var fake = new FakeAdlxNative
            {
                ReleaseHandler = (IntPtr ptr) => -1
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = provider.TryReadTemperatureInternal(12345, out var reading, out var error);

            Assert.True(success);
            Assert.NotNull(reading);
        }

        [Fact]
        public void IsTemperatureSupported_UsesConsistentReleaseInterface()
        {
            var fake = new FakeAdlxNative();
            var provider = new AmdAdlxProvider(() => fake);

            var devices = ((IGpuDeviceEnumerator)provider).EnumerateDevices();
            var device = devices[0];

            bool success = device.TryReadTemperature(out var reading, out var error);

            Assert.True(success);
            Assert.Contains(0x6000, fake.ReleasedPointers);
            Assert.Contains(0x4000, fake.ReleasedPointers);
        }

        [Fact]
        public void ReleaseOrder_Probe_ChildBeforeParent()
        {
            var fake = new FakeAdlxNative();
            var provider = new AmdAdlxProvider(() => fake);

            ((IGpuProviderProbe)provider).Probe();

            int gpuIdx = fake.ReleaseOrder.IndexOf(0x3000);
            int servicesIdx = fake.ReleaseOrder.IndexOf(0x4000);
            int metricsSupportIdx = fake.ReleaseOrder.IndexOf(0x6000);
            int gpuListIdx = fake.ReleaseOrder.IndexOf(0x2000);

            Assert.True(gpuIdx >= 0 && metricsSupportIdx >= 0 && servicesIdx >= 0, "Child interfaces should be released");
            Assert.True(metricsSupportIdx < servicesIdx, "metricsSupport (child) before services (parent)");
            Assert.True(servicesIdx < gpuIdx, "services (child of gpu) before gpu");
            Assert.True(gpuIdx < gpuListIdx, "gpu (child) before gpuList (parent)");
        }

        [Fact]
        public void ReleaseOrder_TryReadTemperatureInternal_ChildBeforeParent()
        {
            var fake = new FakeAdlxNative();
            var provider = new AmdAdlxProvider(() => fake);

            provider.TryReadTemperatureInternal(12345, out var reading, out var error);

            int metricsIdx = fake.ReleaseOrder.IndexOf(0x5000);
            int metricsSupportIdx = fake.ReleaseOrder.IndexOf(0x6000);
            int servicesIdx = fake.ReleaseOrder.IndexOf(0x4000);
            int targetGpuIdx = fake.ReleaseOrder.IndexOf(0x3000);
            int gpuListIdx = fake.ReleaseOrder.IndexOf(0x2000);

            Assert.True(metricsIdx >= 0 && metricsSupportIdx >= 0 && servicesIdx >= 0 && targetGpuIdx >= 0, "All child interfaces should be released");
            Assert.True(metricsIdx < metricsSupportIdx, "metrics before metricsSupport");
            Assert.True(metricsSupportIdx < servicesIdx, "metricsSupport before services");
            Assert.True(servicesIdx < targetGpuIdx, "services before targetGpu");
            Assert.True(targetGpuIdx < gpuListIdx, "targetGpu before gpuList");
        }

        [Fact]
        public void ReleaseOrder_TerminateAfterInterfaces()
        {
            var fake = new FakeAdlxNative();
            var provider = new AmdAdlxProvider(() => fake);

            ((IGpuProviderProbe)provider).Probe();

            int gpuListIdx = fake.Events.IndexOf("Release:0x2000");
            int terminateIdx = fake.Events.IndexOf("Terminate");

            Assert.True(gpuListIdx >= 0, "gpuList should be released");
            Assert.True(terminateIdx >= 0, "Terminate should be called");
            Assert.True(gpuListIdx < terminateIdx, "gpuList released before Terminate");
        }

        [Fact]
        public void ReleaseException_DoesNotBlockOtherReleases_Probe()
        {
            var fake = new FakeAdlxNative
            {
                ThrowOnRelease = true,
                ThrowOnReleasePointer = 0x4000
            };
            var provider = new AmdAdlxProvider(() => fake);

            var result = ((IGpuProviderProbe)provider).Probe();

            Assert.True(result.IsAvailable);
            Assert.Contains(0x6000, fake.ReleasedPointers);
            Assert.Contains(0x4000, fake.ReleasedPointers);
            Assert.Contains(0x3000, fake.ReleasedPointers);
            Assert.Contains(0x2000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);
            Assert.Equal(1, fake.ThrowOnReleaseCount);
        }

        [Fact]
        public void ReleaseException_DoesNotBlockOtherReleases_TryReadTemperatureInternal()
        {
            var fake = new FakeAdlxNative
            {
                ThrowOnRelease = true,
                ThrowOnReleasePointer = 0x6000
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = provider.TryReadTemperatureInternal(12345, out var reading, out var error);

            Assert.True(success);
            Assert.Contains(0x5000, fake.ReleasedPointers);
            Assert.Contains(0x6000, fake.ReleasedPointers);
            Assert.Contains(0x4000, fake.ReleasedPointers);
            Assert.Contains(0x3000, fake.ReleasedPointers);
            Assert.Contains(0x2000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);
            Assert.Equal(1, fake.ThrowOnReleaseCount);
        }

        [Fact]
        public void ReleaseFailedHResult_DoesNotChangeOperationResult_Probe()
        {
            var fake = new FakeAdlxNative
            {
                ReleaseFailAtPointer = 0x4000
            };
            var provider = new AmdAdlxProvider(() => fake);

            var result = ((IGpuProviderProbe)provider).Probe();

            Assert.True(result.IsAvailable);
            Assert.Equal(1, fake.ReleaseFailCount);
        }

        [Fact]
        public void ReleaseFailedHResult_DoesNotChangeOperationResult_TryReadTemperatureInternal()
        {
            var fake = new FakeAdlxNative
            {
                ReleaseFailAtPointer = 0x6000
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = provider.TryReadTemperatureInternal(12345, out var reading, out var error);

            Assert.True(success);
            Assert.NotNull(reading);
            Assert.Equal(1, fake.ReleaseFailCount);
        }

        [Fact]
        public void ReleaseException_DoesNotChangeOperationResult_Probe()
        {
            var fake = new FakeAdlxNative
            {
                ThrowOnRelease = true,
                ThrowOnReleasePointer = 0x3000
            };
            var provider = new AmdAdlxProvider(() => fake);

            var result = ((IGpuProviderProbe)provider).Probe();

            Assert.True(result.IsAvailable);
        }

        [Fact]
        public void ReleaseException_DoesNotChangeOperationResult_TryReadTemperatureInternal()
        {
            var fake = new FakeAdlxNative
            {
                ThrowOnRelease = true,
                ThrowOnReleasePointer = 0x5000
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = provider.TryReadTemperatureInternal(12345, out var reading, out var error);

            Assert.True(success);
        }
    }

    public class AmdAdlxDeviceTests
    {
        private class FakeTemperatureReader : IGpuTemperatureReader
        {
            public int CallCount { get; private set; }
            public string? LastDeviceId { get; private set; }
            public CancellationToken? LastCancellationToken { get; private set; }

            private readonly bool _success;
            private readonly TemperatureReading? _reading;
            private readonly string? _error;

            public FakeTemperatureReader(bool success, TemperatureReading? reading, string? error)
            {
                _success = success;
                _reading = reading;
                _error = error;
            }

            public bool TryReadTemperature(string deviceId, out TemperatureReading? reading, out string? error, CancellationToken cancellationToken = default)
            {
                CallCount++;
                LastDeviceId = deviceId;
                LastCancellationToken = cancellationToken;
                reading = _reading;
                error = _error;
                return _success;
            }
        }

        [Fact]
        public void Constructor_NullTemperatureReader_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new AmdAdlxDevice(null!, "id", "name", GpuKind.Discrete));
        }

        [Fact]
        public void Constructor_NullId_ThrowsArgumentNullException()
        {
            var reader = new FakeTemperatureReader(true, new TemperatureReading(50.0, "GPU", DateTimeOffset.UtcNow), null);
            Assert.Throws<ArgumentNullException>(() => new AmdAdlxDevice(reader, null!, "name", GpuKind.Discrete));
        }

        [Fact]
        public void Constructor_NullName_ThrowsArgumentNullException()
        {
            var reader = new FakeTemperatureReader(true, new TemperatureReading(50.0, "GPU", DateTimeOffset.UtcNow), null);
            Assert.Throws<ArgumentNullException>(() => new AmdAdlxDevice(reader, "id", null!, GpuKind.Discrete));
        }

        [Fact]
        public void TryReadTemperature_Success_ReturnsReading()
        {
            var expectedReading = new TemperatureReading(75.5, "GPU Core", DateTimeOffset.UtcNow);
            var reader = new FakeTemperatureReader(true, expectedReading, null);
            var device = new AmdAdlxDevice(reader, "amd-adlx:12345", "Test GPU", GpuKind.Discrete);

            bool success = device.TryReadTemperature(out var reading, out var error);

            Assert.True(success);
            Assert.Same(expectedReading, reading);
            Assert.Null(error);
            Assert.Equal(1, reader.CallCount);
            Assert.Equal("amd-adlx:12345", reader.LastDeviceId);
        }

        [Fact]
        public void TryReadTemperature_Failure_ReturnsError()
        {
            var reader = new FakeTemperatureReader(false, null, "Hardware failure");
            var device = new AmdAdlxDevice(reader, "amd-adlx:12345", "Test GPU", GpuKind.Discrete);

            bool success = device.TryReadTemperature(out var reading, out var error);

            Assert.False(success);
            Assert.Null(reading);
            Assert.Equal("Hardware failure", error);
        }

        [Fact]
        public void TryReadTemperature_PassesCancellationToken()
        {
            var reader = new FakeTemperatureReader(true, new TemperatureReading(50.0, "GPU", DateTimeOffset.UtcNow), null);
            var device = new AmdAdlxDevice(reader, "amd-adlx:12345", "Test GPU", GpuKind.Discrete);
            var cts = new CancellationTokenSource();

            device.TryReadTemperature(out _, out _, cts.Token);

            Assert.Equal(cts.Token, reader.LastCancellationToken);
        }

        [Fact]
        public void TryReadTemperature_WorksWithAnyIGpuTemperatureReaderImplementation()
        {
            var mockReader = new FakeTemperatureReader(true, new TemperatureReading(60.0, "Mock Sensor", DateTimeOffset.UtcNow), null);
            var device = new AmdAdlxDevice(mockReader, "amd-adlx:99999", "Mock GPU", GpuKind.Integrated);

            bool success = device.TryReadTemperature(out var reading, out var error);

            Assert.True(success);
            Assert.NotNull(reading);
            Assert.Equal(60.0, reading.Celsius);
            Assert.Equal("Mock Sensor", reading.SensorName);
            Assert.Equal(1, mockReader.CallCount);
            Assert.Equal("amd-adlx:99999", mockReader.LastDeviceId);
        }
    }
}