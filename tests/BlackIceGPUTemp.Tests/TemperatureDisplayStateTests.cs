using System;
using BlackIceGPUTemp.Monitoring.Abstractions;
using BlackIceGPUTemp.Tray;
using Xunit;

namespace BlackIceGPUTemp.Tests
{
    public class TemperatureDisplayStateTests
    {
        private static TemperatureReading CreateReading(double celsius)
        {
            return new TemperatureReading(celsius, "GPU Core", DateTimeOffset.UtcNow);
        }


        [Fact]
        public void Create_SuccessfulReading_ReturnsFormattedTooltipAndRoundedIconText()
        {
            var reading = CreateReading(65.5);
            var result = TemperatureDisplayState.Create("Test GPU", true, reading);

            Assert.Equal("Test GPU: 66°C", result.TooltipText);
            Assert.Equal("66", result.IconText);
        }

        [Fact]
        public void Create_TemperatureFormatting_UsesWholeDegrees()
        {
            var reading = CreateReading(70.25);
            var result = TemperatureDisplayState.Create("Test GPU", true, reading);

            Assert.Equal("Test GPU: 70°C", result.TooltipText);
        }

        [Fact]
        public void Create_SuccessfulReading_TooltipAndIconUseSameRounding()
        {
            var reading = CreateReading(65.5);
            var result = TemperatureDisplayState.Create("Test GPU", true, reading);

            Assert.Equal("Test GPU: 66°C", result.TooltipText);
            Assert.Equal("66", result.IconText);
        }

        [Fact]
        public void Create_FailedReading_ReturnsUnavailableTooltipAndDashIcon()
        {
            var result = TemperatureDisplayState.Create("Test GPU", false, null);

            Assert.Equal("Test GPU: Temp unavailable", result.TooltipText);
            Assert.Equal("--", result.IconText);
        }

        [Fact]
        public void Create_FailedReading_WithNullError_ReturnsUnavailableTooltipAndDashIcon()
        {
            var result = TemperatureDisplayState.Create("Test GPU", false, null, null);

            Assert.Equal("Test GPU: Temp unavailable", result.TooltipText);
            Assert.Equal("--", result.IconText);
        }

        [Fact]
        public void Create_FailedReading_WithEmptyError_ReturnsUnavailableTooltipAndDashIcon()
        {
            var result = TemperatureDisplayState.Create("Test GPU", false, null, "");

            Assert.Equal("Test GPU: Temp unavailable", result.TooltipText);
            Assert.Equal("--", result.IconText);
        }

        [Fact]
        public void Create_FailedReading_WithWhitespaceError_ReturnsUnavailableTooltipAndDashIcon()
        {
            var result = TemperatureDisplayState.Create("Test GPU", false, null, "   ");

            Assert.Equal("Test GPU: Temp unavailable", result.TooltipText);
            Assert.Equal("--", result.IconText);
        }

        [Fact]
        public void Create_FailedReading_WithSpecificError_ReturnsErrorTooltipAndDashIcon()
        {
            var result = TemperatureDisplayState.Create("Test GPU", false, null, "Device not found");

            Assert.Equal("Test GPU: Device not found", result.TooltipText);
            Assert.Equal("--", result.IconText);
        }

        [Fact]
        public void Create_FailedReading_WithNonNullReading_ReturnsErrorTooltipAndDashIcon()
        {
            var reading = CreateReading(75.0);
            var result = TemperatureDisplayState.Create("Test GPU", false, reading, "Failed to read temperature from device.");

            Assert.Equal("Test GPU: Failed to read temperature from device.", result.TooltipText);
            Assert.Equal("--", result.IconText);
        }

        [Fact]
        public void Create_SuccessfulReading_WithErrorMessage_IgnoresErrorAndReturnsSuccessTooltip()
        {
            var reading = CreateReading(65.5);
            var result = TemperatureDisplayState.Create("Test GPU", true, reading, "Some error");

            Assert.Equal("Test GPU: 66°C", result.TooltipText);
            Assert.Equal("66", result.IconText);
        }


        [Fact]
        public void Create_SuccessWithNullReading_ReturnsUnavailableTooltipAndDashIcon()
        {
            var result = TemperatureDisplayState.Create("Test GPU", true, null);

            Assert.Equal("Test GPU: Temp unavailable", result.TooltipText);
            Assert.Equal("--", result.IconText);
        }

        [Fact]
        public void Create_FailedWithNonNullReading_ReturnsUnavailableTooltipAndDashIcon()
        {
            var reading = CreateReading(75.0);
            var result = TemperatureDisplayState.Create("Test GPU", false, reading);

            Assert.Equal("Test GPU: Temp unavailable", result.TooltipText);
            Assert.Equal("--", result.IconText);
        }

        [Fact]
        public void Create_ZeroTemperature_ReturnsCorrectValues()
        {
            var reading = CreateReading(0.0);
            var result = TemperatureDisplayState.Create("Test GPU", true, reading);

            Assert.Equal("Test GPU: 0°C", result.TooltipText);
            Assert.Equal("0", result.IconText);
        }

        [Fact]
        public void Create_NegativeTemperature_ReturnsCorrectValues()
        {
            var reading = CreateReading(-5.5);
            var result = TemperatureDisplayState.Create("Test GPU", true, reading);

            Assert.Equal("Test GPU: -6°C", result.TooltipText);
            Assert.Equal("-6", result.IconText);
        }

        [Fact]
        public void Create_HighTemperature_ReturnsCorrectValues()
        {
            var reading = CreateReading(99.9);
            var result = TemperatureDisplayState.Create("Test GPU", true, reading);

            Assert.Equal("Test GPU: 100°C", result.TooltipText);
            Assert.Equal("100", result.IconText);
        }
    }
}