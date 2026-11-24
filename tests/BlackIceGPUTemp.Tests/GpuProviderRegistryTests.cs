using System;
using System.Collections.Generic;
using System.Threading;
using BlackIceGPUTemp.Monitoring;
using BlackIceGPUTemp.Monitoring.Abstractions;
using Xunit;

namespace BlackIceGPUTemp.Tests
{
    public class GpuProviderRegistryTests
    {
        private class DummyProvider : IGpuDeviceEnumerator
        {
            public string Id { get; }
            public string DisplayName => Id;

            public DummyProvider(string id)
            {
                Id = id;
            }

            public bool TryEnumerateDevices(out IReadOnlyList<IGpuDevice> devices, out string? error, CancellationToken cancellationToken = default)
            {
                devices = Array.Empty<IGpuDevice>();
                error = null;
                return true;
            }
        }

        [Fact]
        public void Constructor_NullProviders_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new GpuProviderRegistry(null!));
        }

        [Fact]
        public void Constructor_NullElement_ThrowsArgumentException()
        {
            var providers = new IGpuDeviceEnumerator[] { new DummyProvider("gpu1"), null! };
            Assert.Throws<ArgumentException>(() => new GpuProviderRegistry(providers));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_InvalidId_ThrowsArgumentException(string? invalidId)
        {
            var providers = new IGpuDeviceEnumerator[] { new DummyProvider(invalidId!) };
            Assert.Throws<ArgumentException>(() => new GpuProviderRegistry(providers));
        }

        [Fact]
        public void Constructor_DuplicateIdCaseInsensitive_ThrowsArgumentException()
        {
            var providers = new IGpuDeviceEnumerator[]
            {
                new DummyProvider("Nvidia"),
                new DummyProvider("nvidia")
            };
            Assert.Throws<ArgumentException>(() => new GpuProviderRegistry(providers));
        }

        [Fact]
        public void Constructor_EmptyCollection_Allowed()
        {
            var registry = new GpuProviderRegistry(Array.Empty<IGpuDeviceEnumerator>());
            Assert.Empty(registry.Providers);
        }

        [Fact]
        public void Providers_PreservesOrder()
        {
            var p1 = new DummyProvider("first");
            var p2 = new DummyProvider("second");
            var p3 = new DummyProvider("third");

            var registry = new GpuProviderRegistry(new[] { p1, p2, p3 });

            Assert.Equal(3, registry.Providers.Count);
            Assert.Same(p1, registry.Providers[0]);
            Assert.Same(p2, registry.Providers[1]);
            Assert.Same(p3, registry.Providers[2]);
        }

        [Fact]
        public void Providers_NotModifiableViaIListCast()
        {
            var registry = new GpuProviderRegistry(new[] { new DummyProvider("test") });
            var list = registry.Providers as IList<IGpuDeviceEnumerator>;

            if (list != null)
            {
                Assert.Throws<NotSupportedException>(() => list.Add(new DummyProvider("other")));
            }
        }

        [Fact]
        public void FindById_CaseInsensitiveMatch_ReturnsProvider()
        {
            var provider = new DummyProvider("Nvidia-GPU");
            var registry = new GpuProviderRegistry(new[] { provider });

            Assert.Same(provider, registry.FindById("nvidia-gpu"));
            Assert.Same(provider, registry.FindById("NVIDIA-GPU"));
        }

        [Fact]
        public void FindById_UnknownId_ReturnsNull()
        {
            var registry = new GpuProviderRegistry(new[] { new DummyProvider("amd") });
            Assert.Null(registry.FindById("nvidia"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void FindById_InvalidId_ThrowsArgumentException(string? invalidId)
        {
            var registry = new GpuProviderRegistry(Array.Empty<IGpuDeviceEnumerator>());
            Assert.Throws<ArgumentException>(() => registry.FindById(invalidId!));
        }
    }
}