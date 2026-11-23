using System;
using System.Collections.Generic;
using BlackIceGPUTemp.Monitoring.Abstractions;
using BlackIceGPUTemp.Monitoring.Providers.Amd;
using Xunit;

namespace BlackIceGPUTemp.Tests
{
    public class AmdGpuProviderFactoryTests
    {
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

            var list = Assert.IsAssignableFrom<IList<IGpuProvider>>(providers);
            Assert.Throws<NotSupportedException>(() => list.Add(null!));
            Assert.Throws<NotSupportedException>(() => list.Remove(providers[0]));
            Assert.Throws<NotSupportedException>(() => list.Clear());
        }
    }
}