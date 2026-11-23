using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using BlackIceGPUTemp.Logging;
using BlackIceGPUTemp.Monitoring.Abstractions;
using BlackIceGPUTemp.Monitoring.Providers.Amd.Native;

namespace BlackIceGPUTemp.Monitoring.Providers.Amd
{
    public sealed class AmdAdlxProvider : IGpuProvider
    {
        private readonly Func<IAdlxNative> _nativeFactory;
        private IAdlxNative? _pendingNative;
        private IDisposable? _pendingDisposable;
        private IAdlxNative? _pendingDisposeNative;
        private IDisposable? _pendingDisposeDisposable;
        private readonly object _lock = new object();

        public string Id => "amd-adlx";
        public string DisplayName => "AMD ADLX";

        public AmdAdlxProvider(Func<IAdlxNative>? nativeFactory = null)
        {
            _nativeFactory = nativeFactory ?? (() => new AdlxNative());
        }

        public ProviderProbeResult Probe(CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                return ProbeInternal(cancellationToken);
            }
        }

        private ProviderProbeResult ProbeInternal(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            if (!TryHandlePendingCleanup(out string? error))
            {
                return new ProviderProbeResult(false, error);
            }

            var session = TryCreateSession(out error);
            if (session == null)
            {
                return new ProviderProbeResult(false, error);
            }

            ProviderProbeResult result;
            try
            {
                using (session)
                {
                    try
                    {
                        int sizeResult = session.GetGpuListSize(out int count);
                        if (sizeResult != 0)
                        {
                            result = new ProviderProbeResult(false, $"Failed to get GPU list size. Result code: {sizeResult}");
                        }
                        else
                        {
                            bool hasSupportedGpu = false;
                            for (int i = 0; i < count; i++)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                
                                IntPtr gpu = IntPtr.Zero;
                                try
                                {
                                    if (session.GetGpuAt(i, out gpu) != 0 || gpu == IntPtr.Zero)
                                    {
                                        continue;
                                    }

                                    cancellationToken.ThrowIfCancellationRequested();

                                    if (session.IsTemperatureSupported(gpu, out bool tempSupported) && tempSupported)
                                    {
                                        hasSupportedGpu = true;
                                    }
                                }
                                finally
                                {
                                    session.ReleaseInterface(ref gpu);
                                }

                                if (hasSupportedGpu)
                                {
                                    break;
                                }
                            }

                            if (!hasSupportedGpu)
                            {
                                result = new ProviderProbeResult(false, "No GPU supports temperature monitoring via ADLX.");
                            }
                            else
                            {
                                result = new ProviderProbeResult(true, null);
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("ADLX Probe: exception during GPU enumeration", ex);
                        result = new ProviderProbeResult(false, $"ADLX probe failed: {ex.GetType().Name}: {ex.Message}");
                    }
                }
            }
            finally
            {
                CaptureSessionPendingState(session);
            }

            return result;
        }

        public IReadOnlyList<IGpuDevice> EnumerateDevices(CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                return EnumerateDevicesInternal(cancellationToken);
            }
        }

        private IReadOnlyList<IGpuDevice> EnumerateDevicesInternal(CancellationToken cancellationToken)
        {
            var devices = new List<IGpuDevice>();
            var seenUniqueIds = new HashSet<int>();

            cancellationToken.ThrowIfCancellationRequested();
            
            if (!TryHandlePendingCleanup(out string? error))
            {
                return devices;
            }

            var session = TryCreateSession(out error);
            if (session == null)
            {
                return devices;
            }

            IReadOnlyList<IGpuDevice> result;
            try
            {
                using (session)
                {
                    try
                    {
                        int sizeResult = session.GetGpuListSize(out int count);
                        if (sizeResult != 0)
                        {
                            result = devices;
                        }
                        else
                        {
                            for (int i = 0; i < count; i++)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                
                                IntPtr gpu = IntPtr.Zero;
                                try
                                {
                                    if (session.GetGpuAt(i, out gpu) != 0 || gpu == IntPtr.Zero)
                                    {
                                        continue;
                                    }

                                    cancellationToken.ThrowIfCancellationRequested();

                                    if (!session.GetUniqueIdAndName(gpu, out int uniqueId, out string? name, cancellationToken))
                                    {
                                        continue;
                                    }

                                    cancellationToken.ThrowIfCancellationRequested();

                                    if (uniqueId <= 0 || string.IsNullOrWhiteSpace(name))
                                    {
                                        continue;
                                    }

                                    GpuKind gpuKind = GpuKind.Unknown;
                                    try
                                    {
                                        int typeResult = session.GetGpuType(gpu, out int gpuType);
                                        if (typeResult == 0)
                                        {
                                            switch (gpuType)
                                            {
                                                case 1:
                                                    gpuKind = GpuKind.Integrated;
                                                    break;
                                                case 2:
                                                    gpuKind = GpuKind.Discrete;
                                                    break;
                                                default:
                                                    gpuKind = GpuKind.Unknown;
                                                    break;
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Logger.LogError("ADLX EnumerateDevices: exception while reading GPU type", ex);
                                        gpuKind = GpuKind.Unknown;
                                    }

                                    cancellationToken.ThrowIfCancellationRequested();

                                    if (session.IsTemperatureSupported(gpu, out bool tempSupported) && tempSupported)
                                    {
                                        if (seenUniqueIds.Add(uniqueId))
                                        {
                                            string deviceId = $"amd-adlx:{uniqueId.ToString(CultureInfo.InvariantCulture)}";
                                            devices.Add(new AmdAdlxDevice(this, deviceId, name!, uniqueId, gpuKind));
                                        }
                                    }
                                }
                                finally
                                {
                                    session.ReleaseInterface(ref gpu);
                                }
                            }

                            result = devices;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("ADLX EnumerateDevices: exception during GPU enumeration", ex);
                        result = devices;
                    }
                }
            }
            finally
            {
                CaptureSessionPendingState(session);
            }

            return result;
        }

        internal bool TryReadTemperatureInternal(int targetUniqueId, out TemperatureReading? reading, out string? error, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                return TryReadTemperatureInternalImpl(targetUniqueId, out reading, out error, cancellationToken);
            }
        }

        private bool TryReadTemperatureInternalImpl(int targetUniqueId, out TemperatureReading? reading, out string? error, CancellationToken cancellationToken)
        {
            reading = null;
            error = null;

            cancellationToken.ThrowIfCancellationRequested();
            
            if (!TryHandlePendingCleanup(out error))
            {
                return false;
            }

            var session = TryCreateSession(out error);
            if (session == null)
            {
                return false;
            }

            bool result;
            try
            {
                using (session)
                {
                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        IntPtr targetGpu = FindGpuByUniqueId(session, targetUniqueId, cancellationToken);
                        if (targetGpu == IntPtr.Zero)
                        {
                            error = $"GPU with unique ID {targetUniqueId} not found.";
                            result = false;
                        }
                        else
                        {
                            IntPtr services = IntPtr.Zero;
                            IntPtr metricsSupport = IntPtr.Zero;
                            IntPtr metrics = IntPtr.Zero;

                            try
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                
                                if (session.GetPerformanceMonitoringServices(out services) != 0 || services == IntPtr.Zero)
                                {
                                    error = "Failed to get performance monitoring services.";
                                    result = false;
                                }
                                else
                                {
                                    cancellationToken.ThrowIfCancellationRequested();

                                    if (session.GetSupportedGpuMetrics(services, targetGpu, out metricsSupport) != 0 || metricsSupport == IntPtr.Zero)
                                    {
                                        error = "Failed to get supported GPU metrics.";
                                        result = false;
                                    }
                                    else
                                    {
                                        cancellationToken.ThrowIfCancellationRequested();

                                        if (session.IsSupportedGpuTemperature(metricsSupport, out bool tempSupported) != 0 || !tempSupported)
                                        {
                                            error = "GPU temperature monitoring is not supported.";
                                            result = false;
                                        }
                                        else
                                        {
                                            cancellationToken.ThrowIfCancellationRequested();

                                            if (session.GetCurrentGpuMetrics(services, targetGpu, out metrics) != 0 || metrics == IntPtr.Zero)
                                            {
                                                error = "Failed to get current GPU metrics.";
                                                result = false;
                                            }
                                            else
                                            {
                                                cancellationToken.ThrowIfCancellationRequested();

                                                if (session.GetGpuTemperature(metrics, out double celsius) != 0)
                                                {
                                                    error = "Failed to get GPU temperature reading.";
                                                    result = false;
                                                }
                                                else if (double.IsNaN(celsius) || double.IsInfinity(celsius))
                                                {
                                                    error = "GPU temperature is NaN or Infinity.";
                                                    result = false;
                                                }
                                                else
                                                {
                                                    reading = new TemperatureReading(celsius, "GPU Edge", DateTimeOffset.UtcNow);
                                                    result = true;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            finally
                            {
                                session.ReleaseInterface(ref metrics);
                                session.ReleaseInterface(ref metricsSupport);
                                session.ReleaseInterface(ref services);
                                session.ReleaseInterface(ref targetGpu);
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("ADLX TryReadTemperature: exception during temperature read", ex);
                        error = $"Exception during temperature read: {ex.GetType().Name}: {ex.Message}";
                        result = false;
                    }
                }
            }
            finally
            {
                CaptureSessionPendingState(session);
            }

            return result;
        }

        private bool TryHandlePendingCleanup(out string? error)
        {
            error = null;

            if (_pendingDisposeNative != null && _pendingDisposeDisposable != null)
            {
                try
                {
                    _pendingDisposeDisposable.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.LogError("Pending AdlxNative Dispose retry threw exception", ex);
                    error = "Pending AdlxNative Dispose retry threw exception";
                    return false;
                }
                _pendingDisposeNative = null;
                _pendingDisposeDisposable = null;
            }

            if (_pendingNative != null)
            {
                int retryResult = 0;
                Exception? retryException = null;
                try
                {
                    retryResult = _pendingNative.Terminate();
                }
                catch (Exception ex)
                {
                    retryException = ex;
                }

                if (retryException == null && (retryResult == 0 || retryResult == AdlxSession.ADLX_ORPHAN_OBJECTS))
                {
                    Logger.LogInfo($"Retry Terminate succeeded with HRESULT: 0x{retryResult:X}");
                    try
                    {
                        _pendingDisposable?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Pending AdlxNative Dispose threw exception on retry", ex);
                        _pendingDisposeNative = _pendingNative;
                        _pendingDisposeDisposable = _pendingDisposable;
                        _pendingNative = null;
                        _pendingDisposable = null;
                        error = "Pending AdlxNative Dispose threw exception on retry";
                        return false;
                    }
                    _pendingNative = null;
                    _pendingDisposable = null;
                }
                else
                {
                    Logger.LogError($"Retry Terminate failed with HRESULT: 0x{retryResult:X}" + (retryException != null ? $", exception: {retryException.Message}" : ""));
                    error = $"Retry Terminate failed with HRESULT: 0x{retryResult:X}";
                    return false;
                }
            }

            return true;
        }

        private AdlxSession? TryCreateSession(out string? error)
        {
            IAdlxNative? pendingNative = null;
            IDisposable? pendingDisposable = null;
            IAdlxNative? pendingDisposeNative = null;
            IDisposable? pendingDisposeDisposable = null;

            var session = AdlxSession.Create(_nativeFactory, out error,
                out pendingNative, out pendingDisposable,
                out pendingDisposeNative, out pendingDisposeDisposable);

            _pendingNative = pendingNative;
            _pendingDisposable = pendingDisposable;
            _pendingDisposeNative = pendingDisposeNative;
            _pendingDisposeDisposable = pendingDisposeDisposable;

            return session;
        }

        private void CaptureSessionPendingState(AdlxSession session)
        {
            _pendingNative = session.PendingNative;
            _pendingDisposable = session.PendingDisposable;
            _pendingDisposeNative = session.PendingDisposeNative;
            _pendingDisposeDisposable = session.PendingDisposeDisposable;
        }

        private static IntPtr FindGpuByUniqueId(AdlxSession session, int targetUniqueId, CancellationToken cancellationToken)
        {
            int sizeResult = session.GetGpuListSize(out int count);
            if (sizeResult != 0)
            {
                return IntPtr.Zero;
            }

            for (int i = 0; i < count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                IntPtr gpu = IntPtr.Zero;
                bool isTarget = false;
                try
                {
                    if (session.GetGpuAt(i, out gpu) == 0 && gpu != IntPtr.Zero)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (session.GetGpuUniqueId(gpu, out int uid) == 0 && uid == targetUniqueId)
                        {
                            isTarget = true;
                            return gpu;
                        }
                    }
                }
                finally
                {
                    if (gpu != IntPtr.Zero && !isTarget)
                    {
                        session.ReleaseInterface(ref gpu);
                    }
                }
            }

            return IntPtr.Zero;
        }
    }

    internal static class AdlxInterop
    {
        public static void ReleaseInterface(IAdlxNative? native, ref IntPtr ptr)
        {
            if (native != null && ptr != IntPtr.Zero)
            {
                IntPtr currentPtr = ptr;
                try
                {
                    int result = native.Release(currentPtr);
                    Trace.TraceInformation("ADLX Release: interface=0x{0:X}, ref count={1}", currentPtr, result);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"ADLX Release exception: interface=0x{currentPtr:X}", ex);
                }
                finally
                {
                    ptr = IntPtr.Zero;
                }
            }
        }
    }
}