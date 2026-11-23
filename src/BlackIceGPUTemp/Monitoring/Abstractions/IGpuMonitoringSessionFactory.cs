using System;
using System.Threading;
using BlackIceGPUTemp.Monitoring.Abstractions;

namespace BlackIceGPUTemp.Monitoring.Abstractions
{
    /// <summary>
    /// Factory abstraction for creating GPU monitoring sessions.
    /// </summary>
    public interface IGpuMonitoringSessionFactory
    {
        /// <summary>
        /// Creates a GPU monitoring session.
        /// </summary>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>The result of the initialization.</returns>
        GpuMonitoringInitializationResult Create(CancellationToken cancellationToken);
    }
}