using System;
using System.Collections.Generic;
using System.Threading;

namespace BlackIceGPUTemp.Monitoring.Abstractions
{
    public interface IGpuDeviceEnumerator
    {
        string Id { get; }
        string DisplayName { get; }
        IReadOnlyList<IGpuDevice> EnumerateDevices(CancellationToken cancellationToken = default);
    }
}