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
    public sealed class AmdAdlxProvider : IGpuProvider, IGpuTemperatureReader, IGpuProviderLifecycle
    {
        private const string ProviderId = "amd-adlx";
        private const string DeviceIdPrefix = "amd-adlx:";
        private const string SensorName = "GPU Edge";

        private readonly Func<IAdlxNative> _nativeFactory;
        private IAdlxNative? _pendingNative;
        private IDisposable? _pendingDisposable;
        private IAdlxNative? _pendingDisposeNative;
        private IDisposable? _pendingDisposeDisposable;
        private AdlxSession? _activeSession;
        private bool _disconnected;
        private readonly object _lock = new object();

        public string Id => ProviderId;
        public string DisplayName => "AMD ADLX";

        public AmdAdlxProvider(Func<IAdlxNative>? nativeFactory = null)
        {
            _nativeFactory = nativeFactory ?? (() => new AdlxNative());
        }

        public bool Connect(out string? error)
        {
            lock (_lock)
            {
                error = null;

                // Idempotent: already connected
                if (_activeSession != null && !_activeSession.IsDisposed)
                {
                    return true;
                }

                // Handle any pending cleanup from previous disconnect
                if (!TryHandlePendingCleanup(out error))
                {
                    return false;
                }

                var session = TryCreateSession(out error);
                if (session == null)
                {
                    return false;
                }

                _activeSession = session;
                _disconnected = false; // Reset disconnected state on successful Connect
                return true;
            }
        }

        public void Disconnect()
        {
            lock (_lock)
            {
                // Always mark as explicitly disconnected
                _disconnected = true;

                var session = _activeSession;
                _activeSession = null;

                if (session != null)
                {
                    try
                    {
                        session.Dispose();
                    }
                    finally
                    {
                        // Capture any pending state for retry on next Connect
                        CaptureSessionPendingState(session);
                    }
                }
            }
        }

        public bool TryProbe(out string? error, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                return TryProbeInternal(cancellationToken, out error);
            }
        }

        private bool TryProbeInternal(CancellationToken cancellationToken, out string? error)
        {
            error = null;

            cancellationToken.ThrowIfCancellationRequested();

            var session = EnsureSession(out error);
            if (session == null)
            {
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                int sizeResult = session.GetGpuListSize(out int count);
                if (sizeResult != 0)
                {
                    error = $"Failed to get GPU list size. Result code: {sizeResult}";
                    return false;
                }

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

                        if (session.IsTemperatureSupported(gpu, out bool tempSupported, cancellationToken) && tempSupported)
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
                    error = "No GPU supports temperature monitoring via ADLX.";
                    return false;
                }

                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogError("ADLX TryProbe: exception during GPU enumeration", ex);
                error = $"ADLX probe failed: {ex.GetType().Name}: {ex.Message}";
                return false;
            }
        }

        public bool TryEnumerateDevices(out IReadOnlyList<IGpuDevice> devices, out string? error, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                return TryEnumerateDevicesInternal(out devices, out error, cancellationToken);
            }
        }

        private bool TryEnumerateDevicesInternal(out IReadOnlyList<IGpuDevice> devices, out string? error, CancellationToken cancellationToken)
        {
            var devicesList = new List<IGpuDevice>();
            var seenUniqueIds = new HashSet<int>();

            cancellationToken.ThrowIfCancellationRequested();

            var session = EnsureSession(out error);
            if (session == null)
            {
                devices = Array.Empty<IGpuDevice>();
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                int sizeResult = session.GetGpuListSize(out int count);
                if (sizeResult != 0)
                {
                    error = $"Failed to get GPU list size. Result code: {sizeResult}";
                    devices = Array.Empty<IGpuDevice>();
                    return false;
                }

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

                        if (session.IsTemperatureSupported(gpu, out bool tempSupported, cancellationToken) && tempSupported)
                        {
                            if (seenUniqueIds.Add(uniqueId))
                            {
                                string deviceId = $"{DeviceIdPrefix}{uniqueId.ToString(CultureInfo.InvariantCulture)}";
                                devicesList.Add(new AmdAdlxDevice(this, Id, deviceId, name!, gpuKind));
                            }
                        }
                    }
                    finally
                    {
                        session.ReleaseInterface(ref gpu);
                    }
                }

                devices = devicesList;
                error = null;
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogError("ADLX TryEnumerateDevices: exception during GPU enumeration", ex);
                error = $"Exception during GPU enumeration: {ex.GetType().Name}: {ex.Message}";
                devices = Array.Empty<IGpuDevice>();
                return false;
            }
        }

        public bool TryReadTemperature(string deviceId, out TemperatureReading? reading, out string? error, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                reading = null;
                error = "Device ID cannot be null, empty, or whitespace.";
                return false;
            }

            if (!TryParseUniqueId(deviceId, out int uniqueId))
            {
                reading = null;
                error = $"Invalid device ID format: '{deviceId}'. Expected format: '{DeviceIdPrefix}<uniqueId>'";
                return false;
            }

            lock (_lock)
            {
                return TryReadTemperatureInternalImpl(uniqueId, out reading, out error, cancellationToken);
            }
        }

        private static bool TryParseUniqueId(string deviceId, out int uniqueId)
        {
            uniqueId = 0;
            if (!deviceId.StartsWith(DeviceIdPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            string idPart = deviceId.Substring(DeviceIdPrefix.Length);
            return int.TryParse(idPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out uniqueId);
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

            var session = EnsureSession(out error);
            if (session == null)
            {
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();

            bool result;
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
                                            reading = new TemperatureReading(celsius, SensorName, DateTimeOffset.UtcNow);
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

        private AdlxSession? EnsureSession(out string? error)
        {
            // Return existing valid session
            if (_activeSession != null && !_activeSession.IsDisposed)
            {
                error = null;
                return _activeSession;
            }

            // If explicitly disconnected, do not auto-recreate session
            if (_disconnected)
            {
                error = "Provider has been disconnected. Call Connect() to reinitialize.";
                return null;
            }

            // Handle any pending cleanup from previous disconnect/failed session
            if (!TryHandlePendingCleanup(out error))
            {
                return null;
            }

            var session = TryCreateSession(out error);
            if (session == null)
            {
                return null;
            }

            _activeSession = session;
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
            cancellationToken.ThrowIfCancellationRequested();
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