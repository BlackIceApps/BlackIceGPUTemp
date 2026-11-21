using System;
using TinyTemp.Monitoring.Abstractions;

namespace TinyTemp.Monitoring
{
    /// <summary>
    /// Provides services for monitoring hardware temperature and status through GPU devices.
    /// </summary>
    public class HardwareMonitorService
    {
        private readonly DeviceSelector _deviceSelector;

        /// <summary>
        /// Initializes a new instance of the <see cref="HardwareMonitorService"/> class.
        /// </summary>
        /// <param name="deviceSelector">The device selector used to find GPU devices.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="deviceSelector"/> is null.</exception>
        public HardwareMonitorService(DeviceSelector deviceSelector)
        {
            _deviceSelector = deviceSelector ?? throw new ArgumentNullException(nameof(deviceSelector));
        }

        /// <summary>
        /// Attempts to read the temperature from a specified GPU device.
        /// </summary>
        /// <param name="providerId">The unique identifier of the provider.</param>
        /// <param name="deviceId">The unique identifier of the device.</param>
        /// <param name="reading">When this method returns, contains the temperature reading if successful; otherwise, <c>null</c>.</param>
        /// <param name="error">When this method returns, contains the error message if unsuccessful; otherwise, <c>null</c>.</param>
        /// <returns><c>true</c> if the temperature was successfully read; otherwise, <c>false</c>.</returns>
        public bool TryReadTemperature(string providerId, string deviceId, out TemperatureReading? reading, out string? error)
        {
            var device = _deviceSelector.FindDevice(providerId, deviceId);
            if (device == null)
            {
                reading = null;
                error = "Device not found.";
                return false;
            }

            bool success = device.TryReadTemperature(out var deviceReading, out var deviceError);
            if (!success)
            {
                reading = null;
                error = string.IsNullOrWhiteSpace(deviceError) ? "Failed to read temperature from device." : deviceError;
                return false;
            }

            if (deviceReading == null)
            {
                reading = null;
                error = "Device reported success but returned a null reading.";
                return false;
            }

            reading = deviceReading;
            error = null;
            return true;
        }
    }
}
