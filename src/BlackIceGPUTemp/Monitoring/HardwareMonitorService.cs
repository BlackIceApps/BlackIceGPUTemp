using System;
using System.Threading;
using BlackIceGPUTemp.Monitoring.Abstractions;

namespace BlackIceGPUTemp.Monitoring
{
    /// <summary>
    /// Provides services for monitoring hardware temperature and status through GPU devices.
    /// </summary>
    public class HardwareMonitorService
    {
        private readonly IGpuDevice _directDevice;

        /// <summary>
        /// Initializes a new instance of the <see cref="HardwareMonitorService"/> class using a directly specified GPU device.
        /// </summary>
        /// <param name="directDevice">The GPU device to read temperature from directly.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="directDevice"/> is null.</exception>
        public HardwareMonitorService(IGpuDevice directDevice)
        {
            _directDevice = directDevice ?? throw new ArgumentNullException(nameof(directDevice));
        }

        /// <summary>
        /// Attempts to read the temperature from a specified GPU device.
        /// </summary>
        /// <param name="providerId">The unique identifier of the provider.</param>
        /// <param name="deviceId">The unique identifier of the device.</param>
        /// <param name="reading">When this method returns, contains the temperature reading if successful; otherwise, <c>null</c>.</param>
        /// <param name="error">When this method returns, contains the error message if unsuccessful; otherwise, <c>null</c>.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns><c>true</c> if the temperature was successfully read; otherwise, <c>false</c>.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="providerId"/> or <paramref name="deviceId"/> is null, empty, or consists entirely of white-space characters.</exception>
        public bool TryReadTemperature(string providerId, string deviceId, out TemperatureReading? reading, out string? error, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(providerId))
            {
                throw new ArgumentException("Provider ID cannot be null, empty, or consist entirely of white-space characters.", nameof(providerId));
            }

            if (string.IsNullOrWhiteSpace(deviceId))
            {
                throw new ArgumentException("Device ID cannot be null, empty, or consist entirely of white-space characters.", nameof(deviceId));
            }

            if (!string.Equals(_directDevice.ProviderId, providerId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(_directDevice.Id, deviceId, StringComparison.OrdinalIgnoreCase))
            {
                reading = null;
                error = "Provider ID or Device ID does not match the configured device.";
                return false;
            }

            bool success = _directDevice.TryReadTemperature(out var deviceReading, out var deviceError, cancellationToken);
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
