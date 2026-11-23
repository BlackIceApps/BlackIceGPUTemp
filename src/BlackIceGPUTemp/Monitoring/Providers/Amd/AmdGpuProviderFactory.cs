using System;
using System.Collections.Generic;
using BlackIceGPUTemp.Monitoring.Abstractions;
using BlackIceGPUTemp.Monitoring.Providers.Amd.Native;

namespace BlackIceGPUTemp.Monitoring.Providers.Amd
{
    /// <summary>
    /// Default implementation of <see cref="IGpuProviderFactory"/> for AMD GPUs using ADLX.
    /// </summary>
    public sealed class AmdGpuProviderFactory : IGpuProviderFactory
    {
        /// <inheritdoc />
        public IReadOnlyList<IGpuProvider> CreateProviders()
        {
            return new List<IGpuProvider>
            {
                new AmdAdlxProvider()
            }.AsReadOnly();
        }
    }
}