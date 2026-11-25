using System;
using BlackIceGPUTemp.Monitoring.Abstractions;

namespace BlackIceGPUTemp.Tray
{
    internal static class TemperatureDisplayState
    {
        public readonly struct Result
        {
            public string TooltipText { get; }
            public string IconText { get; }

            public Result(string tooltipText, string iconText)
            {
                TooltipText = tooltipText;
                IconText = iconText;
            }
        }

        public static Result Create(string deviceName, bool success, TemperatureReading? reading, string? error = null)
        {
            if (success && reading != null && IsFinite(reading.Celsius))
            {
                var tooltipText = $"{deviceName}: {reading.Celsius:F1}°C";
                var iconText = ((int)Math.Round(reading.Celsius)).ToString();
                return new Result(tooltipText, iconText);
            }
            else
            {
                var tooltipText = string.IsNullOrWhiteSpace(error)
                    ? $"{deviceName}: Temp unavailable"
                    : $"{deviceName}: {error}";
                var iconText = "--";
                return new Result(tooltipText, iconText);
            }
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}