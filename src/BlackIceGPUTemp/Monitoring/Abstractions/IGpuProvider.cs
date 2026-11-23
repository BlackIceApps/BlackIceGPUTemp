using System;
using System.Collections.Generic;
using System.Threading;

namespace BlackIceGPUTemp.Monitoring.Abstractions
{
    public interface IGpuProvider : IGpuProviderProbe, IGpuDeviceEnumerator
    {
    }
}