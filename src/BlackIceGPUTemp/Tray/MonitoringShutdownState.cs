using System;
using System.Threading;

namespace BlackIceGPUTemp.Tray
{
    internal sealed class MonitoringShutdownState
    {
        private readonly object _lock = new();
        private readonly CancellationTokenSource _cancellationTokenSource;
        private bool _disposing;

        public MonitoringShutdownState(CancellationTokenSource cancellationTokenSource)
        {
            _cancellationTokenSource = cancellationTokenSource ?? throw new ArgumentNullException(nameof(cancellationTokenSource));
        }

        public bool IsDisposing
        {
            get
            {
                lock (_lock)
                {
                    return _disposing;
                }
            }
        }

        public bool TryBeginDispose()
        {
            lock (_lock)
            {
                if (_disposing)
                {
                    return false;
                }
                _disposing = true;
                return true;
            }
        }

        public bool TryGetCancellationToken(out CancellationToken token)
        {
            lock (_lock)
            {
                if (_disposing)
                {
                    token = default;
                    return false;
                }
                token = _cancellationTokenSource.Token;
                return true;
            }
        }
    }
}