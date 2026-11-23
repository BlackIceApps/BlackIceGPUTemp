using System;
using System.Collections.Generic;
using System.Threading;

namespace BlackIceGPUTemp.Monitoring.Abstractions
{
    public interface IGpuDeviceEnumerator : IGpuProviderMetadata
    {
        IReadOnlyList<IGpuDevice> EnumerateDevices(CancellationToken cancellationToken = default);
    }
}