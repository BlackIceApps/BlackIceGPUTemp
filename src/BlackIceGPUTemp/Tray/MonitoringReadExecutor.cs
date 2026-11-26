using System;
using System.Threading;

namespace BlackIceGPUTemp.Tray
{
    internal sealed class MonitoringReadExecutor
    {
        private readonly MonitoringReadGate _readGate = new();
        private readonly Action<Action> _scheduler;

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

            int gateReleased = 0;

            void ReleaseGateOnce()
            {
                if (Interlocked.Exchange(ref gateReleased, 1) == 0)
                {
                    _readGate.EndRead();
                }
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

        public void Stop()
        {
            _readGate.Stop();
        }

        public void WaitForIdle()
        {
            _readGate.WaitForIdle();
        }
    }
}