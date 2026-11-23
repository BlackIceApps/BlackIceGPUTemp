using System;
using System.Collections.Generic;
using BlackIceGPUTemp.Monitoring.Abstractions;
using BlackIceGPUTemp.Monitoring.Providers.Amd;
using BlackIceGPUTemp.Monitoring.Providers.Amd.Native;
using Xunit;

namespace BlackIceGPUTemp.Tests
{
    public class AmdGpuProviderFactoryTests
    {
        private class FakeAdlxNative : IAdlxNative, IDisposable
        {
            public int Initialize(out IntPtr system)
            {
                system = new IntPtr(0x1000);
                return 0;
            }

            public int Terminate()
            {
                return 0;
            }

            public int GetGpuList(IntPtr system, out IntPtr gpuList)
            {
                gpuList = new IntPtr(0x2000);
                return 0;
            }

            public int GetGpuListSize(IntPtr gpuList, out int count)
            {
                count = 1;
                return 0;
            }

            public int GetGpuAt(IntPtr gpuList, int index, out IntPtr gpu)
            {
                gpu = new IntPtr(0x3000);
                return 0;
            }

            public int GetGpuName(IntPtr gpu, out string? name)
            {
                name = "AMD Radeon RX 7900 XTX";
                return 0;
            }

            public int GetGpuUniqueId(IntPtr gpu, out int uniqueId)
            {
                uniqueId = 12345;
                return 0;
            }

            public int GetGpuType(IntPtr gpu, out int gpuType)
            {
                gpuType = 2;
                return 0;
            }

            public int GetPerformanceMonitoringServices(IntPtr system, out IntPtr services)
            {
                services = new IntPtr(0x4000);
                return 0;
            }

            public int GetCurrentGpuMetrics(IntPtr services, IntPtr gpu, out IntPtr metrics)
            {
                metrics = new IntPtr(0x5000);
                return 0;
            }

            public int GetSupportedGpuMetrics(IntPtr services, IntPtr gpu, out IntPtr metricsSupport)
            {
                metricsSupport = new IntPtr(0x6000);
                return 0;
            }

            public int IsSupportedGpuTemperature(IntPtr metricsSupport, out bool supported)
            {
                supported = true;
                return 0;
            }

            public int GetGpuTemperature(IntPtr metrics, out double celsius)
            {
                celsius = 65.5;
                return 0;
            }

            public int Release(IntPtr adlxInterface)
            {
                return 0;
            }

            public void Dispose()
            {
            }
        }

        [Fact]
        public void CreateProviders_ReturnsExactlyOneProvider()
        {
            var factory = new AmdGpuProviderFactory();
            var providers = factory.CreateProviders();

            Assert.Single(providers);
        }

        [Fact]
        public void CreateProviders_ReturnedObjectImplementsIGpuProvider()
        {
            var factory = new AmdGpuProviderFactory();
            var providers = factory.CreateProviders();

            Assert.IsAssignableFrom<IGpuProvider>(providers[0]);
        }

        [Fact]
        public void CreateProviders_ReturnedProviderHasExpectedIdAndDisplayName()
        {
            var factory = new AmdGpuProviderFactory();
            var providers = factory.CreateProviders();
            var provider = providers[0];

            Assert.Equal("amd-adlx", provider.Id);
            Assert.Equal("AMD ADLX", provider.DisplayName);
        }

        [Fact]
        public void CreateProviders_CalledTwice_ReturnsNewInstanceEachTime()
        {
            var factory = new AmdGpuProviderFactory();
            var providers1 = factory.CreateProviders();
            var providers2 = factory.CreateProviders();

            Assert.NotSame(providers1[0], providers2[0]);
        }

        [Fact]
        public void CreateProviders_ReturnedCollectionIsReadOnly()
        {
            var factory = new AmdGpuProviderFactory();
            var providers = factory.CreateProviders();

            Assert.Throws<NotSupportedException>(() => providers.Add(null!));
            Assert.Throws<NotSupportedException>(() => providers.Remove(providers[0]));
            Assert.Throws<NotSupportedException>(() => providers.Clear());
        }
    }
}