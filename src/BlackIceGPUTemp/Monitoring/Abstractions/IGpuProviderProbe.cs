using System;
using System.Threading;

namespace BlackIceGPUTemp.Monitoring.Abstractions
{
    public interface IGpuProviderProbe : IGpuProviderMetadata
    {
        ProviderProbeResult Probe(CancellationToken cancellationToken = default);
    }
}