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

        [Fact]
        public void Constructor_NonUtcTimestamp_ConvertsToUtc()
        {
            var localTime = new DateTimeOffset(2026, 11, 21, 12, 0, 0, TimeSpan.FromHours(2));
            var reading = new TemperatureReading(70.0, "GPU Core", localTime);

            Assert.Equal(TimeSpan.Zero, reading.TimestampUtc.Offset);
            Assert.Equal(localTime, reading.TimestampUtc);
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
