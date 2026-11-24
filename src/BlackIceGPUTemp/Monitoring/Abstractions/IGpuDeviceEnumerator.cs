using System;
using System.Collections.Generic;
using System.Threading;

namespace BlackIceGPUTemp.Monitoring.Abstractions
{
    public interface IGpuDeviceEnumerator : IGpuProviderMetadata
    {
        /// <summary>
        /// Attempts to enumerate GPU devices supported by this provider.
        /// </summary>
        /// <param name="devices">When the method returns <see langword="true"/>, contains the read-only list of enumerated devices.
        /// May be empty if no supported GPUs are found. When the method returns <see langword="false"/>, contains an empty list.</param>
        /// <param name="error">When the method returns <see langword="false"/>, contains a description of the failure reason.
        /// When the method returns <see langword="true"/>, is <see langword="null"/>.</param>
        /// <param name="cancellationToken">A token to observe while waiting for the enumeration to complete.</param>
        /// <returns>
        /// <see langword="true"/> if enumeration completed successfully (even if zero devices were found);
        /// <see langword="false"/> if an error occurred during enumeration (e.g., ADLX initialization failure, native call error, or unhandled exception).
        /// </returns>
        /// <exception cref="OperationCanceledException">Thrown if <paramref name="cancellationToken"/> is canceled during enumeration.</exception>
        bool TryEnumerateDevices(out IReadOnlyList<IGpuDevice> devices, out string? error, CancellationToken cancellationToken = default);
    }
}