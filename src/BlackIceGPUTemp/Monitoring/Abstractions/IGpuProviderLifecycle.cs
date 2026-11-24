using System;

namespace BlackIceGPUTemp.Monitoring.Abstractions
{
    public interface IGpuProviderLifecycle
    {
        bool Connect(out string? error);
        void Disconnect();
    }
}