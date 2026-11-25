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

        public static Result Create(string deviceName, bool success, TemperatureReading? reading)
        {
            if (success && reading != null)
            {
                var tooltipText = $"{deviceName}: {reading.Celsius:F1}°C";
                var iconText = ((int)Math.Round(reading.Celsius)).ToString();
                return new Result(tooltipText, iconText);
            }
            else
            {
                var tooltipText = $"{deviceName}: Temp unavailable";
                var iconText = "--";
                return new Result(tooltipText, iconText);
            }
        }
    }
}