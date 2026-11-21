using System;
using TinyTemp.Monitoring.Abstractions;
using Xunit;

namespace TinyTemp.Tests
{
    public class TemperatureReadingTests
    {
        [Fact]
        public void Constructor_ValidValues_SetsPropertiesCorrectly()
        {
            var timestamp = DateTimeOffset.UtcNow;
            var reading = new TemperatureReading(65.5, "GPU Core", timestamp);

            Assert.Equal(65.5, reading.Celsius);
            Assert.Equal("GPU Core", reading.SensorName);
            Assert.Equal(timestamp, reading.TimestampUtc);
        }

        [Theory]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        [InlineData(double.NegativeInfinity)]
        public void Constructor_InvalidTemperature_ThrowsArgumentOutOfRangeException(double invalidCelsius)
        {
            var timestamp = DateTimeOffset.UtcNow;
            Assert.Throws<ArgumentOutOfRangeException>(() => new TemperatureReading(invalidCelsius, "GPU Core", timestamp));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_InvalidSensorName_ThrowsArgumentException(string? invalidSensorName)
        {
            var timestamp = DateTimeOffset.UtcNow;
            Assert.Throws<ArgumentException>(() => new TemperatureReading(50.0, invalidSensorName!, timestamp));
        }
    }
}
