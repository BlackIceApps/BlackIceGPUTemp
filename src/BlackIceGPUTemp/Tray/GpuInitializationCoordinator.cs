using System;
using System.Threading;
using System.Threading.Tasks;
using BlackIceGPUTemp.Logging;
using BlackIceGPUTemp.Monitoring;
using BlackIceGPUTemp.Monitoring.Abstractions;

namespace BlackIceGPUTemp.Tray
{
    internal sealed class GpuInitializationCoordinator
    {
        private readonly IGpuMonitoringSessionFactory _monitoringSessionFactory;
        private int _initializationInProgress = 0;

        public GpuInitializationCoordinator(IGpuMonitoringSessionFactory monitoringSessionFactory)
        {
            _monitoringSessionFactory = monitoringSessionFactory ?? throw new ArgumentNullException(nameof(monitoringSessionFactory));
        }

        public async Task TryInitializeAsync(
            CancellationToken token,
            Action onAttemptStart,
            Func<GpuMonitoringInitializationResult, CancellationToken, Task> onResult)
        {
            if (Interlocked.CompareExchange(ref _initializationInProgress, 1, 0) != 0)
            {
                return;
            }

            try
            {
                onAttemptStart();

                var initResult = await Task.Run(() =>
                {
                    try
                    {
                        return _monitoringSessionFactory.Create(token);
                    }
                    catch (OperationCanceledException)
                    {
                        return new GpuMonitoringInitializationResult("Operation canceled");
                    }
                    catch (Exception ex)
                    {
                        if (!token.IsCancellationRequested)
                        {
                            Logger.LogError("Initialization error", ex);
                            return new GpuMonitoringInitializationResult("Initialization error");
                        }
                        return new GpuMonitoringInitializationResult("Operation canceled");
                    }
                });

                if (token.IsCancellationRequested)
                {
                    if (initResult.ProviderLifecycle != null)
                    {
                        try
                        {
                            initResult.ProviderLifecycle.Disconnect();
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError("Error disconnecting provider after cancellation", ex);
                        }
                    }
                    return;
                }

                await onResult(initResult, token);
            }
            finally
            {
                Interlocked.Exchange(ref _initializationInProgress, 0);
            }
        }
    }
}