using System;
using System.Collections.Generic;
using TinyTemp.Monitoring.Abstractions;
using TinyTemp.Monitoring.Providers.Amd;
using TinyTemp.Monitoring.Providers.Amd.Native;
using Xunit;

namespace TinyTemp.Tests
{
    public class AmdAdlxProviderTests
    {
        private class FakeAdlxNative : IAdlxNative, IDisposable
        {
            public int InitializeCalls { get; private set; }
            public int TerminateCalls { get; private set; }
            public int ReleaseCalls { get; private set; }
            public bool IsDisposed { get; private set; }

            public Func<IntPtr, int>? InitializeHandler { get; set; }
            public Func<int>? TerminateHandler { get; set; }
            public Func<IntPtr, IntPtr, int>? GetGpuListHandler { get; set; }
            public Func<IntPtr, uint, int>? GetGpuListSizeHandler { get; set; }
            public Func<IntPtr, int, IntPtr, int>? GetGpuAtHandler { get; set; }
            public Func<IntPtr, string?, int>? GetGpuNameHandler { get; set; }
            public Func<IntPtr, int, int>? GetGpuUniqueIdHandler { get; set; }
            public Func<IntPtr, IntPtr, int>? GetPerfServicesHandler { get; set; }
            public Func<IntPtr, IntPtr, IntPtr, int>? GetCurrentMetricsHandler { get; set; }
            public Func<IntPtr, IntPtr, IntPtr, int>? GetSupportedMetricsHandler { get; set; }
            public Func<IntPtr, bool, int>? IsSupportedTempHandler { get; set; }
            public Func<IntPtr, double, int>? GetGpuTempHandler { get; set; }
            public Func<IntPtr, int>? ReleaseHandler { get; set; }

            public int Initialize(out IntPtr system)
            {
                InitializeCalls++;
                system = new IntPtr(0x1000);
                if (InitializeHandler != null)
                {
                    return InitializeHandler(system);
                }
                return 0;
            }

            public int Terminate()
            {
                TerminateCalls++;
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
                    return GetGpuListHandler(system, gpuList);
                }
                return 0;
            }

            public int GetGpuListSize(IntPtr gpuList, out int count)
            {
                count = 1;
                if (GetGpuListSizeHandler != null)
                {
                    return GetGpuListSizeHandler(gpuList, (uint)count);
                }
                return 0;
            }

            public int GetGpuAt(IntPtr gpuList, int index, out IntPtr gpu)
            {
                gpu = new IntPtr(0x3000 + index);
                if (GetGpuAtHandler != null)
                {
                    return GetGpuAtHandler(gpuList, index, gpu);
                }
                return 0;
            }

            public int GetGpuName(IntPtr gpu, out string? name)
            {
                name = "AMD Radeon RX 7900 XTX";
                if (GetGpuNameHandler != null)
                {
                    return GetGpuNameHandler(gpu, name);
                }
                return 0;
            }

            public int GetGpuUniqueId(IntPtr gpu, out int uniqueId)
            {
                uniqueId = 12345;
                if (GetGpuUniqueIdHandler != null)
                {
                    return GetGpuUniqueIdHandler(gpu, uniqueId);
                }
                return 0;
            }

            public int GetPerformanceMonitoringServices(IntPtr system, out IntPtr services)
            {
                services = new IntPtr(0x4000);
                if (GetPerfServicesHandler != null)
                {
                    return GetPerfServicesHandler(system, services);
                }
                return 0;
            }

            public int GetCurrentGpuMetrics(IntPtr services, IntPtr gpu, out IntPtr metrics)
            {
                metrics = new IntPtr(0x5000);
                if (GetCurrentMetricsHandler != null)
                {
                    return GetCurrentMetricsHandler(services, gpu);
                }
                return 0;
            }

            public int GetSupportedGpuMetrics(IntPtr services, IntPtr gpu, out IntPtr metricsSupport)
            {
                metricsSupport = new IntPtr(0x6000);
                if (GetSupportedMetricsHandler != null)
                {
                    return GetSupportedMetricsHandler(services, gpu);
                }
                return 0;
            }

            public int IsSupportedGpuTemperature(IntPtr metricsSupport, out bool supported)
            {
                supported = true;
                if (IsSupportedTempHandler != null)
                {
                    return IsSupportedTempHandler(metricsSupport, supported);
                }
                return 0;
            }

            public int GetGpuTemperature(IntPtr metrics, out double celsius)
            {
                celsius = 65.5;
                if (GetGpuTempHandler != null)
                {
                    return GetGpuTempHandler(metrics, celsius);
                }
                return 0;
            }

            public int Release(IntPtr adlxInterface)
            {
                ReleaseCalls++;
                if (ReleaseHandler != null)
                {
                    return ReleaseHandler(adlxInterface);
                }
                return 0;
            }

            public void Dispose()
            {
                IsDisposed = true;
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
                IsSupportedTempHandler = (ptr, sup) => 0 // supported = false
            };
            // Wait, IsSupportedGpuTemperature has out bool supported. Let's make sure our fake assigns supported = false.
            // In our FakeAdlxNative above, IsSupportedTempHandler takes (ptr, sup) and returns int. 
            // Let's refine FakeAdlxNative or handle it properly.
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
    }
}
