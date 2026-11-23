using System;

namespace BlackIceGPUTemp.Monitoring.Abstractions
{
    public interface IGpuProviderMetadata
    {
        string Id { get; }
        string DisplayName { get; }
    }
}