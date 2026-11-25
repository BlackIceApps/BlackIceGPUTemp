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

            bool scheduled = false;
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
                        _readGate.EndRead();
                    }
                });
                scheduled = true;
            }
            catch
            {
                if (!scheduled)
                {
                    _readGate.EndRead();
                }
                throw;
            }

            return true;
        }

        public void Stop()
        {
            _readGate.Stop();
        }
    }
}