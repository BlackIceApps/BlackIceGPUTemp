using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            public GetPerformanceMonitoringServicesDelegate? GetPerfServicesHandler { get; set; }
            public GetCurrentGpuMetricsDelegate? GetCurrentMetricsHandler { get; set; }
            public GetSupportedGpuMetricsDelegate? GetSupportedMetricsHandler { get; set; }
            public IsSupportedGpuTemperatureDelegate? IsSupportedTempHandler { get; set; }
            public GetGpuTemperatureDelegate? GetGpuTempHandler { get; set; }
            public ReleaseDelegate? ReleaseHandler { get; set; }

            public List<string> GpuNames { get; set; } = new List<string> { "AMD Radeon RX 7900 XTX" };
            public List<int> GpuUniqueIds { get; set; } = new List<int> { 12345 };
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
                int index = (int)(gpu.ToInt64() - 0x3000);
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
            public int TerminateHResult { get; set; } = unchecked((int)0x80004005); // E_FAIL
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

            var result = provider.Probe();

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

            var result = provider.Probe();

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

            var result = provider.Probe();

            Assert.False(result.IsAvailable);
            Assert.Equal(1, fake.TerminateCalls);
            Assert.True(fake.IsDisposed);
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

            var result = provider.Probe();

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

            var result = provider.Probe();

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

            var result = provider.Probe();

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

            var result = provider.Probe();

            Assert.False(result.IsAvailable);
            Assert.Equal("No GPU supports temperature monitoring via ADLX.", result.Message);
        }

        [Fact]
        public void EnumerateDevices_ReturnsValidDevices()
        {
            var fake = new FakeAdlxNative();
            var provider = new AmdAdlxProvider(() => fake);

            var devices = provider.EnumerateDevices();

            Assert.Single(devices);
            var device = devices[0];
            Assert.Equal("amd-adlx:12345", device.Id);
            Assert.Equal("AMD Radeon RX 7900 XTX", device.Name);
            Assert.Equal(GpuVendor.Amd, device.Vendor);
            Assert.Equal(GpuKind.Unknown, device.Kind);
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

            var devices = provider.EnumerateDevices();

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

            var devices = provider.EnumerateDevices();

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

            var devices = provider.EnumerateDevices();

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

            var devices = provider.EnumerateDevices();

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

            var devices = provider.EnumerateDevices();

            Assert.Single(devices);
        }

        [Fact]
        public void Device_TryReadTemperature_ReturnsReading()
        {
            var fake = new FakeAdlxNative();
            var provider = new AmdAdlxProvider(() => fake);

            var devices = provider.EnumerateDevices();
            var device = devices[0];

            bool success = device.TryReadTemperature(out var reading, out var error);

            Assert.True(success);
            Assert.Null(error);
            Assert.NotNull(reading);
            Assert.Equal(65.5, reading.Celsius);
            Assert.Equal("GPU Edge", reading.SensorName);
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

            var devices = provider.EnumerateDevices();
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

            provider.Probe();

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

            provider.Probe();

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

            var result = provider.Probe();

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

            var result = provider.Probe();

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

            var result = provider.Probe();

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
            Assert.NotNull(reading);
        }

        [Fact]
        public void Terminate_ReturnsErrorHResult_Probe_ReturnsSuccessAndLogsError()
        {
            var fake = new TerminateFailingFake { TerminateHResult = unchecked((int)0x80004005) };
            var provider = new AmdAdlxProvider(() => fake);

            var result = provider.Probe();

            Assert.True(result.IsAvailable);
            Assert.Equal(1, fake.TerminateCalls);
            Assert.True(fake.IsDisposed);
        }

        [Fact]
        public void Terminate_ReturnsErrorHResult_TryReadTemperatureInternal_ReturnsSuccessAndLogsError()
        {
            var fake = new TerminateFailingFake { TerminateHResult = unchecked((int)0x80004005) };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = provider.TryReadTemperatureInternal(12345, out var reading, out var error);

            Assert.True(success);
            Assert.NotNull(reading);
            Assert.Equal(1, fake.TerminateCalls);
            Assert.True(fake.IsDisposed);
        }

        [Fact]
        public void Terminate_ReturnsErrorHResult_EnumerateDevices_ReturnsDevicesAndLogsError()
        {
            var fake = new TerminateFailingFake { TerminateHResult = unchecked((int)0x80004005) };
            var provider = new AmdAdlxProvider(() => fake);

            var devices = provider.EnumerateDevices();

            Assert.Single(devices);
            Assert.Equal(1, fake.TerminateCalls);
            Assert.True(fake.IsDisposed);
        }

        [Fact]
        public void Terminate_ThrowsException_Probe_ReturnsSuccessAndLogsError()
        {
            var fake = new TerminateFailingFake { ThrowOnTerminate = true };
            var provider = new AmdAdlxProvider(() => fake);

            var result = provider.Probe();

            Assert.True(result.IsAvailable);
            Assert.Equal(1, fake.TerminateCalls);
            Assert.True(fake.IsDisposed);
        }

        [Fact]
        public void Terminate_ThrowsException_TryReadTemperatureInternal_ReturnsSuccessAndLogsError()
        {
            var fake = new TerminateFailingFake { ThrowOnTerminate = true };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = provider.TryReadTemperatureInternal(12345, out var reading, out var error);

            Assert.True(success);
            Assert.NotNull(reading);
            Assert.Equal(1, fake.TerminateCalls);
            Assert.True(fake.IsDisposed);
        }

        [Fact]
        public void Terminate_ThrowsException_EnumerateDevices_ReturnsDevicesAndLogsError()
        {
            var fake = new TerminateFailingFake { ThrowOnTerminate = true };
            var provider = new AmdAdlxProvider(() => fake);

            var devices = provider.EnumerateDevices();

            Assert.Single(devices);
            Assert.Equal(1, fake.TerminateCalls);
            Assert.True(fake.IsDisposed);
        }

        [Fact]
        public void Dispose_ThrowsException_Probe_ReturnsSuccessAndLogsError()
        {
            var fake = new DisposeThrowingFake { ThrowOnDispose = true };
            var provider = new AmdAdlxProvider(() => fake);

            var result = provider.Probe();

            Assert.True(result.IsAvailable);
            Assert.Equal(1, fake.TerminateCalls);
            Assert.True(fake.IsDisposed);
        }

        [Fact]
        public void Dispose_ThrowsException_TryReadTemperatureInternal_ReturnsSuccessAndLogsError()
        {
            var fake = new DisposeThrowingFake { ThrowOnDispose = true };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = provider.TryReadTemperatureInternal(12345, out var reading, out var error);

            Assert.True(success);
            Assert.NotNull(reading);
            Assert.Equal(1, fake.TerminateCalls);
            Assert.True(fake.IsDisposed);
        }

        [Fact]
        public void Dispose_ThrowsException_EnumerateDevices_ReturnsDevicesAndLogsError()
        {
            var fake = new DisposeThrowingFake { ThrowOnDispose = true };
            var provider = new AmdAdlxProvider(() => fake);

            var devices = provider.EnumerateDevices();

            Assert.Single(devices);
            Assert.Equal(1, fake.TerminateCalls);
            Assert.True(fake.IsDisposed);
        }

        [Fact]
        public void Terminate_ErrorDoesNotMaskOriginalFailure_Probe()
        {
            var fake = new TerminateFailingFake
            {
                TerminateHResult = unchecked((int)0x80004005),
                GpuTemperatureSupported = new List<bool> { false }
            };
            var provider = new AmdAdlxProvider(() => fake);

            var result = provider.Probe();

            Assert.False(result.IsAvailable);
            Assert.Equal("No GPU supports temperature monitoring via ADLX.", result.Message);
        }

        [Fact]
        public void Terminate_ErrorDoesNotMaskOriginalFailure_TryReadTemperatureInternal()
        {
            var fake = new TerminateFailingFake
            {
                TerminateHResult = unchecked((int)0x80004005),
                GetGpuTempHandler = (IntPtr met, out double temp) =>
                {
                    temp = 0;
                    return -1;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = provider.TryReadTemperatureInternal(12345, out var reading, out var error);

            Assert.False(success);
            Assert.Equal("Failed to get GPU temperature reading.", error);
        }

        [Fact]
        public void Dispose_ErrorDoesNotMaskOriginalFailure_Probe()
        {
            var fake = new DisposeThrowingFake
            {
                ThrowOnDispose = true,
                GpuTemperatureSupported = new List<bool> { false }
            };
            var provider = new AmdAdlxProvider(() => fake);

            var result = provider.Probe();

            Assert.False(result.IsAvailable);
            Assert.Equal("No GPU supports temperature monitoring via ADLX.", result.Message);
        }

        [Fact]
        public void Dispose_ErrorDoesNotMaskOriginalFailure_TryReadTemperatureInternal()
        {
            var fake = new DisposeThrowingFake
            {
                ThrowOnDispose = true,
                GetGpuTempHandler = (IntPtr met, out double temp) =>
                {
                    temp = 0;
                    return -1;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = provider.TryReadTemperatureInternal(12345, out var reading, out var error);

            Assert.False(success);
            Assert.Equal("Failed to get GPU temperature reading.", error);
        }

        [Fact]
        public void Terminate_Success_DisposesLibrary()
        {
            var fake = new FakeAdlxNative();
            var provider = new AmdAdlxProvider(() => fake);

            provider.Probe();

            Assert.Equal(1, fake.TerminateCalls);
            Assert.True(fake.IsDisposed);
        }

        [Fact]
        public void AdlxNative_Terminate_ReturnsZero_IsInitializedBecomesFalse()
        {
            int freeCallCount = 0;
            var native = new AdlxNative(
                new IntPtr(0x1234),
                _ => { freeCallCount++; return true; },
                true,
                () => 0);

            int result = native.Terminate();

            Assert.Equal(0, result);
            native.Dispose();
            Assert.Equal(1, freeCallCount);
            Assert.True(native.IsDisposed);
        }

        [Fact]
        public void AdlxNative_Terminate_ReturnsOrphanObjects_IsInitializedBecomesFalse()
        {
            int freeCallCount = 0;
            var native = new AdlxNative(
                new IntPtr(0x1234),
                _ => { freeCallCount++; return true; },
                true,
                () => 0xB);

            int result = native.Terminate();

            Assert.Equal(0xB, result);
            native.Dispose();
            Assert.Equal(1, freeCallCount);
            Assert.True(native.IsDisposed);
        }

        [Fact]
        public void AdlxNative_Terminate_ReturnsOtherError_IsInitializedRemainsTrue()
        {
            int freeCallCount = 0;
            var native = new AdlxNative(
                new IntPtr(0x1234),
                _ => { freeCallCount++; return true; },
                true,
                () => -1);

            int result = native.Terminate();

            Assert.Equal(-1, result);
            native.Dispose();
            Assert.Equal(0, freeCallCount);
            Assert.False(native.IsDisposed);
        }

        [Fact]
        public void AdlxNative_Dispose_WhenInitialized_DoesNotCallFreeLibrary()
        {
            int freeCallCount = 0;
            var native = new AdlxNative(new IntPtr(0x1234), _ => { freeCallCount++; return true; }, true);

            native.Dispose();

            Assert.Equal(0, freeCallCount);
            Assert.False(native.IsDisposed);
            Assert.Equal(new IntPtr(0x1234), native.LibHandle);
        }

        [Fact]
        public void AdlxNative_Dispose_WhenNotInitialized_SuccessfulFree_CallsFreeLibraryOnce()
        {
            int freeCallCount = 0;
            var native = new AdlxNative(new IntPtr(0x1234), _ => { freeCallCount++; return true; }, false);

            native.Dispose();

            Assert.Equal(1, freeCallCount);
            Assert.True(native.IsDisposed);
            Assert.Equal(IntPtr.Zero, native.LibHandle);
        }

        [Fact]
        public void AdlxNative_Dispose_WhenNotInitialized_FailedFree_RetainsHandleAndAllowsRetry()
        {
            int freeCallCount = 0;
            bool freeResult = false;
            var native = new AdlxNative(new IntPtr(0x1234), _ => { freeCallCount++; return freeResult; }, false);

            native.Dispose();

            Assert.Equal(1, freeCallCount);
            Assert.False(native.IsDisposed);
            Assert.Equal(new IntPtr(0x1234), native.LibHandle);

            freeResult = true;
            native.Dispose();

            Assert.Equal(2, freeCallCount);
            Assert.True(native.IsDisposed);
            Assert.Equal(IntPtr.Zero, native.LibHandle);
        }

        [Fact]
        public void Terminate_OrphanObjects_DisposesLibrary()
        {
            var fake = new TerminateFailingFake { TerminateHResult = 0xB };
            var provider = new AmdAdlxProvider(() => fake);

            provider.Probe();

            Assert.Equal(1, fake.TerminateCalls);
            Assert.True(fake.IsDisposed);
        }

        [Fact]
        public void Terminate_OtherError_RetainsNativeAndDoesNotDispose()
        {
            var fake = new TerminateFailingFake { TerminateHResult = -1 };
            var provider = new AmdAdlxProvider(() => fake);

            provider.Probe();

            Assert.Equal(1, fake.TerminateCalls);
            Assert.False(fake.IsDisposed);
        }

        [Fact]
        public void Terminate_Exception_RetainsNativeAndDoesNotDispose()
        {
            var fake = new TerminateFailingFake { ThrowOnTerminate = true };
            var provider = new AmdAdlxProvider(() => fake);

            provider.Probe();

            Assert.Equal(1, fake.TerminateCalls);
            Assert.False(fake.IsDisposed);
        }

        [Fact]
        public void NextCall_RetriesTerminate_OnSuccess_DisposesAndAllowsNewInit()
        {
            var fake = new TerminateFailingFake { TerminateHResult = -1 };
            var fake2 = new FakeAdlxNative();
            var factoryCallCount = 0;
            var provider = new AmdAdlxProvider(() =>
            {
                factoryCallCount++;
                return factoryCallCount == 1 ? fake : fake2;
            });

            provider.Probe();

            Assert.Equal(1, fake.TerminateCalls);
            Assert.False(fake.IsDisposed);

            var result = provider.Probe();

            Assert.True(result.IsAvailable);
            Assert.Equal(2, fake.TerminateCalls);
            Assert.True(fake.IsDisposed);
            Assert.Equal(1, fake2.TerminateCalls);
            Assert.True(fake2.IsDisposed);
        }

        [Fact]
        public void NextCall_RetriesTerminate_OnFailure_ReturnsErrorAndDoesNotInit()
        {
            var fake = new TerminateFailingFake { TerminateHResult = -1 };
            var fake2 = new TerminateFailingFake { TerminateHResult = -1 };
            var factoryCallCount = 0;
            var provider = new AmdAdlxProvider(() =>
            {
                factoryCallCount++;
                return factoryCallCount == 1 ? fake : fake2;
            });

            provider.Probe();

            Assert.Equal(1, fake.TerminateCalls);
            Assert.False(fake.IsDisposed);

            var result = provider.Probe();

            Assert.False(result.IsAvailable);
            Assert.Equal("Failed to initialize ADLX after retry.", result.Message);
            Assert.Equal(2, fake.TerminateCalls);
            Assert.False(fake.IsDisposed);
            Assert.Equal(0, fake2.InitializeCalls);
        }

        [Fact]
        public void NextCall_RetriesTerminate_OnException_ReturnsErrorAndDoesNotInit()
        {
            var fake = new TerminateFailingFake { ThrowOnTerminate = true };
            var fake2 = new TerminateFailingFake { ThrowOnTerminate = true };
            var factoryCallCount = 0;
            var provider = new AmdAdlxProvider(() =>
            {
                factoryCallCount++;
                return factoryCallCount == 1 ? fake : fake2;
            });

            provider.Probe();

            Assert.Equal(1, fake.TerminateCalls);
            Assert.False(fake.IsDisposed);

            var result = provider.Probe();

            Assert.False(result.IsAvailable);
            Assert.Equal("Failed to initialize ADLX after retry.", result.Message);
            Assert.Equal(2, fake.TerminateCalls);
            Assert.False(fake.IsDisposed);
            Assert.Equal(0, fake2.InitializeCalls);
        }

        [Fact]
        public void RetryTerminate_ThrowsException_RetainsNativeAndDoesNotDispose()
        {
            var fake = new TerminateFailingFake { ThrowOnTerminate = true };
            var fake2 = new FakeAdlxNative();
            var factoryCallCount = 0;
            var provider = new AmdAdlxProvider(() =>
            {
                factoryCallCount++;
                return factoryCallCount == 1 ? fake : fake2;
            });

            provider.Probe();

            Assert.Equal(1, fake.TerminateCalls);
            Assert.False(fake.IsDisposed);

            var result = provider.Probe();

            Assert.False(result.IsAvailable);
            Assert.Equal("Failed to initialize ADLX after retry.", result.Message);
            Assert.Equal(2, fake.TerminateCalls);
            Assert.False(fake.IsDisposed);
            Assert.Equal(0, fake2.InitializeCalls);
        }

        [Fact]
        public void FactoryThrowsException_Probe_ReturnsError()
        {
            var provider = new AmdAdlxProvider(() => throw new InvalidOperationException("Factory failed"));

            var result = provider.Probe();

            Assert.False(result.IsAvailable);
            Assert.Contains("Factory failed", result.Message);
        }

        [Fact]
        public void InitializeThrowsException_Probe_ReturnsError()
        {
            var fake = new FakeAdlxNative
            {
                InitializeHandler = (out IntPtr sys) =>
                {
                    sys = IntPtr.Zero;
                    throw new InvalidOperationException("Initialize failed");
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            var result = provider.Probe();

            Assert.False(result.IsAvailable);
            Assert.Contains("Initialize failed", result.Message);
        }

        [Fact]
        public void InitializeThrowsException_EnumerateDevices_ReturnsEmpty()
        {
            var fake = new FakeAdlxNative
            {
                InitializeHandler = (out IntPtr sys) =>
                {
                    sys = IntPtr.Zero;
                    throw new InvalidOperationException("Initialize failed");
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            var devices = provider.EnumerateDevices();

            Assert.Empty(devices);
        }

        [Fact]
        public void InitializeThrowsException_TryReadTemperatureInternal_ReturnsError()
        {
            var fake = new FakeAdlxNative
            {
                InitializeHandler = (out IntPtr sys) =>
                {
                    sys = IntPtr.Zero;
                    throw new InvalidOperationException("Initialize failed");
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = provider.TryReadTemperatureInternal(12345, out var reading, out var error);

            Assert.False(success);
            Assert.Contains("Initialize failed", error);
        }

        [Fact]
        public void Initialize_ReturnsZeroWithNullSystem_CallsTerminateAndDisposes()
        {
            var fake = new FakeAdlxNative
            {
                InitializeHandler = (out IntPtr sys) =>
                {
                    sys = IntPtr.Zero;
                    return 0;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            var result = provider.Probe();

            Assert.False(result.IsAvailable);
            Assert.Equal(1, fake.TerminateCalls);
            Assert.True(fake.IsDisposed);
            Assert.Contains("Result code: 0", result.Message);
        }

        [Fact]
        public void Initialize_ReturnsNonZero_ReturnsErrorCode()
        {
            var fake = new FakeAdlxNative
            {
                InitializeHandler = (out IntPtr sys) =>
                {
                    sys = new IntPtr(0x1000);
                    return -5;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            var result = provider.Probe();

            Assert.False(result.IsAvailable);
            Assert.Equal(0, fake.TerminateCalls);
            Assert.Contains("Result code: -5", result.Message);
        }

        [Fact]
        public void Initialize_ZeroWithNullSystem_GetGpuListNotCalled()
        {
            var fake = new FakeAdlxNative
            {
                InitializeHandler = (out IntPtr sys) =>
                {
                    sys = IntPtr.Zero;
                    return 0;
                },
                GetGpuListHandler = (IntPtr sys, out IntPtr list) =>
                {
                    list = IntPtr.Zero;
                    throw new InvalidOperationException("GetGpuList should not be called");
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            var result = provider.Probe();

            Assert.False(result.IsAvailable);
            Assert.Contains("Result code: 0", result.Message);
        }

        [Fact]
        public void Initialize_ZeroWithNullSystem_TerminateFailure_RetainsNative()
        {
            var fake = new TerminateFailingFake 
            { 
                ThrowOnTerminate = true,
                InitializeHandler = (out IntPtr sys) =>
                {
                    sys = IntPtr.Zero;
                    return 0;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            var result = provider.Probe();

            Assert.False(result.IsAvailable);
            Assert.Equal(1, fake.TerminateCalls);
            Assert.False(fake.IsDisposed);
            Assert.Contains("Result code: 0", result.Message);

            var result2 = provider.Probe();

            Assert.False(result2.IsAvailable);
            Assert.Equal("Failed to initialize ADLX after retry.", result2.Message);
            Assert.Equal(2, fake.TerminateCalls);
            Assert.False(fake.IsDisposed);
        }

        [Fact]
        public void Initialize_ZeroWithNullSystem_TerminateErrorHResult_RetainsNative()
        {
            var fake = new TerminateFailingFake 
            { 
                TerminateHResult = -1,
                InitializeHandler = (out IntPtr sys) =>
                {
                    sys = IntPtr.Zero;
                    return 0;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            var result = provider.Probe();

            Assert.False(result.IsAvailable);
            Assert.Equal(1, fake.TerminateCalls);
            Assert.False(fake.IsDisposed);
            Assert.Contains("Result code: 0", result.Message);

            var result2 = provider.Probe();

            Assert.False(result2.IsAvailable);
            Assert.Equal("Failed to initialize ADLX after retry.", result2.Message);
            Assert.Equal(2, fake.TerminateCalls);
            Assert.False(fake.IsDisposed);
        }

        [Fact]
        public void Initialize_ZeroWithNullSystem_TerminateSuccess_AllowsNewInit()
        {
            var fake = new FakeAdlxNative
            {
                InitializeHandler = (out IntPtr sys) =>
                {
                    sys = IntPtr.Zero;
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

            var result1 = provider.Probe();

            Assert.False(result1.IsAvailable);
            Assert.Equal(1, fake.TerminateCalls);
            Assert.True(fake.IsDisposed);

            var result2 = provider.Probe();

            Assert.True(result2.IsAvailable);
            Assert.Equal(1, fake2.TerminateCalls);
            Assert.True(fake2.IsDisposed);
        }
    }
}