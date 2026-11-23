using System;
using System.Threading;

namespace BlackIceGPUTemp.Monitoring.Abstractions
{
    public interface IGpuProviderProbe
    {
        string Id { get; }
        string DisplayName { get; }
        ProviderProbeResult Probe(CancellationToken cancellationToken = default);
    }
}