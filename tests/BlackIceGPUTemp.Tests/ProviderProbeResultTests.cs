using System;
using BlackIceGPUTemp.Monitoring.Abstractions;
using Xunit;

namespace BlackIceGPUTemp.Tests
{
    public class ProviderProbeResultTests
    {
        [Fact]
        public void Constructor_UnavailableWithNullMessage_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new ProviderProbeResult(false, null));
        }

        [Fact]
        public void Constructor_UnavailableWithMessage_Accepted()
        {
            var result = new ProviderProbeResult(false, "Provider not found");

            Assert.False(result.IsAvailable);
            Assert.Equal("Provider not found", result.Message);
        }

        [Fact]
        public void Constructor_AvailableWithNullMessage_Accepted()
        {
            var result = new ProviderProbeResult(true, null);

            Assert.True(result.IsAvailable);
            Assert.Null(result.Message);
        }

        [Fact]
        public void Constructor_AvailableWithMessage_Accepted()
        {
            var result = new ProviderProbeResult(true, "OK");

            Assert.True(result.IsAvailable);
            Assert.Equal("OK", result.Message);
        }
    }
}