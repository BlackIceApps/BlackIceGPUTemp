using System;
using System.Threading;

namespace BlackIceGPUTemp.Monitoring.Abstractions
{
    public interface IGpuProviderProbe : IGpuProviderMetadata
    {
        /// <summary>
        /// Attempts to probe the provider to check if it is available.
        /// </summary>
        /// <param name="error">
        /// When this method returns <see langword="true"/>, contains <see langword="null"/>.
        /// When this method returns <see langword="false"/>, contains the reason why the provider is not available.
        /// </param>
        /// <param name="cancellationToken">A token to observe while waiting for the probe to complete.</param>
        /// <returns>
        /// <see langword="true"/> if the provider is available and ready to use; otherwise, <see langword="false"/>.
        /// </returns>
        /// <exception cref="OperationCanceledException">
        /// Thrown when the operation is cancelled via <paramref name="cancellationToken"/>.
        /// </exception>
        /// <remarks>
        /// Errors other than cancellation are caught internally and reported via the <paramref name="error"/> parameter,
        /// resulting in a <see langword="false"/> return value. No exceptions other than <see cref="OperationCanceledException"/>
        /// are thrown by this method.
        /// </remarks>
        bool TryProbe(out string? error, CancellationToken cancellationToken = default);
    }
}