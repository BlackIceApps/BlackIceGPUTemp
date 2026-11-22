namespace BlackIceGPUTemp.Monitoring.Abstractions
{
    /// <summary>
    /// Defines a physical or logical GPU device capable of reporting telemetry.
    /// </summary>
    public interface IGpuDevice
    {
        /// <summary>
        /// Gets the unique identifier of the GPU device.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Gets the display name of the GPU device.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the vendor of the GPU device.
        /// </summary>
        GpuVendor Vendor { get; }

        /// <summary>
        /// Gets the kind/form factor of the GPU device.
        /// </summary>
        GpuKind Kind { get; }

        /// <summary>
        /// Attempts to read the current temperature from the GPU device.
        /// </summary>
        /// <param name="reading">When this method returns, contains the temperature reading if successful, or <c>null</c> otherwise.</param>
        /// <param name="error">When this method returns, contains an error message if the read failed, or <c>null</c> otherwise.</param>
        /// <returns><c>true</c> if the temperature was read successfully; otherwise, <c>false</c>.</returns>
        bool TryReadTemperature(out TemperatureReading? reading, out string? error);
    }
}
