using System;
using System.Threading;

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
        /// Gets the unique identifier of the provider that owns this device.
        /// </summary>
        string ProviderId { get; }

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
        /// <param name="reading">
        /// When this method returns <see langword="true"/>, contains the temperature reading.
        /// When this method returns <see langword="false"/>, contains <see langword="null"/>.
        /// </param>
        /// <param name="error">
        /// When this method returns <see langword="false"/>, contains a description of the failure reason.
        /// When this method returns <see langword="true"/>, contains <see langword="null"/>.
        /// </param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns><c>true</c> if the temperature was read successfully; otherwise, <c>false</c>.</returns>
        /// <exception cref="OperationCanceledException">
        /// Thrown when the operation is cancelled via <paramref name="cancellationToken"/>.
        /// </exception>
        /// <remarks>
        /// On success (<see langword="true"/>), <paramref name="reading"/> is non-null and <paramref name="error"/> is <see langword="null"/>.
        /// On failure (<see langword="false"/>), <paramref name="reading"/> is <see langword="null"/> and <paramref name="error"/> contains the failure reason.
        /// Cancellation propagates <see cref="OperationCanceledException"/> and does not set the output parameters.
        /// </remarks>
        bool TryReadTemperature(out TemperatureReading? reading, out string? error, CancellationToken cancellationToken = default);
    }
}
