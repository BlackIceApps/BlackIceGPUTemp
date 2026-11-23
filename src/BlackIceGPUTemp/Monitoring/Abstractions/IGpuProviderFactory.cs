using System;
using System.Collections.Generic;

namespace BlackIceGPUTemp.Monitoring.Abstractions
{
    /// <summary>
    /// Factory abstraction for creating GPU provider instances.
    /// </summary>
    public interface IGpuProviderFactory
    {
        /// <summary>
        /// Creates a read-only collection of GPU providers.
        /// </summary>
        /// <returns>A read-only collection of <see cref="IGpuProvider"/> instances.</returns>
        IReadOnlyList<IGpuProvider> CreateProviders();
    }
}