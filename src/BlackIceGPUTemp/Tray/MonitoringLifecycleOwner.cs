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
        private bool _registered;

        public bool TryRegister(IGpuProviderLifecycle? lifecycle)
        {
            lock (_lock)
            {
                if (_stopped || _registered)
                {
                    return false;
                }

                _lifecycle = lifecycle;
                _registered = true;
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