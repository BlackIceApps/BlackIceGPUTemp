using System;
using System.Threading;

namespace BlackIceGPUTemp.Tray
{
    internal sealed class MonitoringReadGate
    {
        private readonly object _gate = new();
        private bool _readInProgress;
        private bool _stopped;

        public bool TryBeginRead()
        {
            lock (_gate)
            {
                if (_stopped || _readInProgress)
                {
                    return false;
                }
                _readInProgress = true;
                return true;
            }
        }

        public void EndRead()
        {
            lock (_gate)
            {
                _readInProgress = false;
            }
        }

        public void Stop()
        {
            lock (_gate)
            {
                _stopped = true;
            }
        }
    }
}