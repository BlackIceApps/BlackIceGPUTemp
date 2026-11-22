using System;

namespace BlackIceGPUTemp.Monitoring.Abstractions
{
    /// <summary>
    /// Represents an immutable temperature reading from a GPU sensor.
    /// </summary>
    public sealed class TemperatureReading
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TemperatureReading"/> class.
        /// </summary>
        /// <param name="celsius">The temperature in degrees Celsius.</param>
        /// <param name="sensorName">The name of the sensor that produced the reading.</param>
        /// <param name="timestampUtc">The UTC timestamp when the reading was taken.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="celsius"/> is NaN or infinity.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="sensorName"/> is null, empty, or whitespace-only.</exception>
        public TemperatureReading(double celsius, string sensorName, DateTimeOffset timestampUtc)
        {
            if (double.IsNaN(celsius) || double.IsInfinity(celsius))
            {
                throw new ArgumentOutOfRangeException(nameof(celsius), celsius, "Temperature must be a finite number.");
            }

            if (string.IsNullOrWhiteSpace(sensorName))
            {
                throw new ArgumentException("Sensor name cannot be null or whitespace.", nameof(sensorName));
            }

            Celsius = celsius;
            SensorName = sensorName;
            TimestampUtc = timestampUtc.ToUniversalTime();
        }

        /// <summary>
        /// Gets the temperature in degrees Celsius.
        /// </summary>
        public double Celsius { get; }

        /// <summary>
        /// Gets the name of the sensor.
        /// </summary>
        public string SensorName { get; }

        /// <summary>
        /// Gets the UTC timestamp when the reading was taken.
        /// </summary>
        public DateTimeOffset TimestampUtc { get; }
    }
}
