using System;
using System.Threading;

namespace BlackIceGPUTemp.Monitoring.Abstractions
{
    public interface IGpuTemperatureReader
    {
        /// <summary>
        /// Attempts to read the current temperature for the specified GPU device.
        /// </summary>
        /// <param name="deviceId">The unique identifier of the GPU device to read temperature from.</param>
        /// <param name="reading">
        /// When this method returns <see langword="true"/>, contains the temperature reading.
        /// When this method returns <see langword="false"/>, contains <see langword="null"/>.
        /// </param>
        /// <param name="error">
        /// When this method returns <see langword="false"/>, contains a description of the failure reason.
        /// When this method returns <see langword="true"/>, contains <see langword="null"/>.
        /// </param>
        /// <param name="cancellationToken">A token to observe while waiting for the read operation to complete.</param>
        /// <returns>
        /// <see langword="true"/> if the temperature was read successfully; <see langword="false"/> if the read failed.
        /// </returns>
        /// <exception cref="OperationCanceledException">
        /// Thrown when the operation is cancelled via <paramref name="cancellationToken"/>.
        /// </exception>
        /// <remarks>
        /// On success (<see langword="true"/>), <paramref name="reading"/> is non-null and <paramref name="error"/> is <see langword="null"/>.
        /// On failure (<see langword="false"/>), <paramref name="reading"/> is <see langword="null"/> and <paramref name="error"/> contains the failure reason.
        /// Cancellation propagates <see cref="OperationCanceledException"/> and does not set the output parameters.
        /// </remarks>
        bool TryReadTemperature(string deviceId, out TemperatureReading? reading, out string? error, CancellationToken cancellationToken = default);
    }
}