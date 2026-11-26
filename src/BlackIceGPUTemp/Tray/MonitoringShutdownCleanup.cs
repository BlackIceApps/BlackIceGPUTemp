using System;
using BlackIceGPUTemp.Logging;
using BlackIceGPUTemp.Monitoring.Abstractions;

namespace BlackIceGPUTemp.Tray
{
    internal sealed class MonitoringShutdownCleanup
    {
        private readonly Action _stopTimer;
        private readonly Func<IGpuProviderLifecycle?> _stopAndTakeLifecycle;
        private readonly Action _stopExecutor;
        private readonly Action _cancelTokenSource;
        private readonly Action _waitForIdle;
        private readonly Action _disposeTokenSource;
        private readonly Action _disposeTimer;
        private readonly Action _hideNotifyIcon;
        private readonly Action _disposeNotifyIcon;
        private readonly Action _disposeCurrentIcon;
        private readonly Action _disposeUiDispatcher;
        private readonly Action<string, Exception> _logger;

        public MonitoringShutdownCleanup(
            Action stopTimer,
            Func<IGpuProviderLifecycle?> stopAndTakeLifecycle,
            Action stopExecutor,
            Action cancelTokenSource,
            Action waitForIdle,
            Action disposeTokenSource,
            Action disposeTimer,
            Action hideNotifyIcon,
            Action disposeNotifyIcon,
            Action disposeCurrentIcon,
            Action disposeUiDispatcher,
            Action<string, Exception>? logger = null)
        {
            _stopTimer = stopTimer ?? throw new ArgumentNullException(nameof(stopTimer));
            _stopAndTakeLifecycle = stopAndTakeLifecycle ?? throw new ArgumentNullException(nameof(stopAndTakeLifecycle));
            _stopExecutor = stopExecutor ?? throw new ArgumentNullException(nameof(stopExecutor));
            _cancelTokenSource = cancelTokenSource ?? throw new ArgumentNullException(nameof(cancelTokenSource));
            _waitForIdle = waitForIdle ?? throw new ArgumentNullException(nameof(waitForIdle));
            _disposeTokenSource = disposeTokenSource ?? throw new ArgumentNullException(nameof(disposeTokenSource));
            _disposeTimer = disposeTimer ?? throw new ArgumentNullException(nameof(disposeTimer));
            _hideNotifyIcon = hideNotifyIcon ?? throw new ArgumentNullException(nameof(hideNotifyIcon));
            _disposeNotifyIcon = disposeNotifyIcon ?? throw new ArgumentNullException(nameof(disposeNotifyIcon));
            _disposeCurrentIcon = disposeCurrentIcon ?? throw new ArgumentNullException(nameof(disposeCurrentIcon));
            _disposeUiDispatcher = disposeUiDispatcher ?? throw new ArgumentNullException(nameof(disposeUiDispatcher));
            _logger = logger ?? DefaultLogger;
        }

        private static void DefaultLogger(string message, Exception exception)
        {
            Logger.LogError(message, exception);
        }

        public void Execute()
        {
            IGpuProviderLifecycle? lifecycleToDisconnect = null;
            bool waitForIdleSucceeded = false;

            SafeExecute(1, "Stop timer", _stopTimer);

            SafeExecute(2, "Stop and take lifecycle", () => lifecycleToDisconnect = _stopAndTakeLifecycle());

            SafeExecute(3, "Stop executor", _stopExecutor);

            SafeExecute(4, "Cancel token source", _cancelTokenSource);

            try
            {
                _waitForIdle();
                waitForIdleSucceeded = true;
            }
            catch (Exception ex)
            {
                SafeLogError("WaitForIdle failed", ex);
            }

            if (waitForIdleSucceeded)
            {
                SafeExecute(6, "Dispose token source", _disposeTokenSource);
            }

            SafeExecute(7, "Dispose timer", _disposeTimer);
            SafeExecute(8, "Hide NotifyIcon", _hideNotifyIcon);
            SafeExecute(9, "Dispose NotifyIcon", _disposeNotifyIcon);
            SafeExecute(10, "Dispose current icon", _disposeCurrentIcon);

            if (waitForIdleSucceeded)
            {
                SafeExecute(11, "Dispose UI dispatcher", _disposeUiDispatcher);
            }

            if (lifecycleToDisconnect != null && waitForIdleSucceeded)
            {
                SafeExecute(12, "Disconnect provider", () => lifecycleToDisconnect.Disconnect());
            }
        }

        private void SafeExecute(int stepNumber, string operationName, Action operation)
        {
            try
            {
                operation();
            }
            catch (Exception ex)
            {
                SafeLogError($"Step {stepNumber} ({operationName}) failed", ex);
            }
        }

        private void SafeLogError(string message, Exception exception)
        {
            try
            {
                _logger(message, exception);
            }
            catch
            {
            }
        }
    }
}