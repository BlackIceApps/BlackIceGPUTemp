using System;
using System.Threading;

namespace BlackIceGPUTemp.Tray
{
    internal sealed class MonitoringReadTrigger
    {
        private readonly MonitoringReadExecutor _executor;

        public MonitoringReadTrigger(MonitoringReadExecutor executor)
        {
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        }

        public bool TryStart(
            bool hasSession,
            string? providerId,
            string? deviceId,
            bool isDisposing,
            Action<CancellationToken> operation,
            CancellationToken cancellationToken)
        {
            if (!hasSession)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(providerId))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(deviceId))
            {
                return false;
            }

            if (isDisposing)
            {
                return false;
            }

            return _executor.TryExecute(operation, cancellationToken);
        }
    }
}