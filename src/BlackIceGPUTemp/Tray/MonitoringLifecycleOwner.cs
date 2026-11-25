using System;
using System.Threading;
using BlackIceGPUTemp.Monitoring.Abstractions;

namespace BlackIceGPUTemp.Tray
{
    internal sealed class MonitoringLifecycleOwner
    {
        private readonly object _lock = new();
        private IGpuProviderLifecycle? _lifecycle;
        private bool _stopped;

        public bool TryRegister(IGpuProviderLifecycle? lifecycle)
        {
            lock (_lock)
            {
                if (_stopped)
                {
                    return false;
                }

                _lifecycle = lifecycle;
                return true;
            }
        }

        public IGpuProviderLifecycle? StopAndTake()
        {
            lock (_lock)
            {
                if (_stopped)
                {
                    return null;
                }

                _stopped = true;
                var lifecycle = _lifecycle;
                _lifecycle = null;
                return lifecycle;
            }
        }
    }
}