using System;

namespace BlackIceGPUTemp.Monitoring
{
    /// <summary>
    /// Represents the result of GPU monitoring initialization.
    /// </summary>
    public sealed class GpuMonitoringInitializationResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GpuMonitoringInitializationResult"/> class for a successful initialization.
        /// </summary>
        /// <param name="monitorService">The hardware monitor service.</param>
        /// <param name="providerId">The ID of the selected provider.</param>
        /// <param name="deviceId">The ID of the selected device.</param>
        /// <param name="deviceName">The name of the selected device.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="monitorService"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="providerId"/>, <paramref name="deviceId"/>, or <paramref name="deviceName"/> is null, empty, or consists entirely of white-space characters.</exception>
        public GpuMonitoringInitializationResult(HardwareMonitorService monitorService, string providerId, string deviceId, string deviceName)
        {
            if (monitorService == null)
            {
                throw new ArgumentNullException(nameof(monitorService));
            }
            if (string.IsNullOrWhiteSpace(providerId))
            {
                throw new ArgumentException("Provider ID cannot be null, empty, or consist entirely of white-space characters.", nameof(providerId));
            }
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                throw new ArgumentException("Device ID cannot be null, empty, or consist entirely of white-space characters.", nameof(deviceId));
            }
            if (string.IsNullOrWhiteSpace(deviceName))
            {
                throw new ArgumentException("Device name cannot be null, empty, or consist entirely of white-space characters.", nameof(deviceName));
            }

            IsSuccess = true;
            HardwareMonitorService = monitorService;
            ProviderId = providerId;
            DeviceId = deviceId;
            DeviceName = deviceName;
            Message = null;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GpuMonitoringInitializationResult"/> class for a failed initialization.
        /// </summary>
        /// <param name="message">The reason for the failure.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="message"/> is null, empty, or consists entirely of white-space characters.</exception>
        public GpuMonitoringInitializationResult(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException("Failure message cannot be null, empty, or consist entirely of white-space characters.", nameof(message));
            }

            IsSuccess = false;
            HardwareMonitorService = null;
            ProviderId = null;
            DeviceId = null;
            DeviceName = null;
            Message = message;
        }

        /// <summary>
        /// Gets a value indicating whether the initialization was successful.
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// Gets the hardware monitor service. Only non-null when <see cref="IsSuccess"/> is true.
        /// </summary>
        public HardwareMonitorService? HardwareMonitorService { get; }

        /// <summary>
        /// Gets the ID of the selected provider. Only non-null when <see cref="IsSuccess"/> is true.
        /// </summary>
        public string? ProviderId { get; }

        /// <summary>
        /// Gets the ID of the selected device. Only non-null when <see cref="IsSuccess"/> is true.
        /// </summary>
        public string? DeviceId { get; }

        /// <summary>
        /// Gets the name of the selected device. Only non-null when <see cref="IsSuccess"/> is true.
        /// </summary>
        public string? DeviceName { get; }

        /// <summary>
        /// Gets the failure message. Only non-null when <see cref="IsSuccess"/> is false.
        /// </summary>
        public string? Message { get; }
    }
}