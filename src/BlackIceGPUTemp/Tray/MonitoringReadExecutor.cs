using System;
using System.Threading;

namespace BlackIceGPUTemp.Tray
{
    internal sealed class MonitoringReadExecutor
    {
        private readonly MonitoringReadGate _readGate = new();
        private readonly Action<Action> _scheduler;
        private int _gateReleased;

        public MonitoringReadExecutor(Action<Action> scheduler)
        {
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        }

        public bool TryExecute(Action<CancellationToken> operation, CancellationToken cancellationToken)
        {
            if (!_readGate.TryBeginRead())
            {
                return false;
            }

            try
            {
                _scheduler(() =>
                {
                    try
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            return;
                        }
                        operation(cancellationToken);
                    }
                    finally
                    {
                        ReleaseGateOnce();
                    }
                });
            }
            catch
            {
                ReleaseGateOnce();
                throw;
            }

            return true;
        }

        private void ReleaseGateOnce()
        {
            if (Interlocked.Exchange(ref _gateReleased, 1) == 0)
            {
                _readGate.EndRead();
            }
        }

        public void Stop()
        {
            _readGate.Stop();
        }
    }
}