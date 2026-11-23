using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
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

        private const int ADLX_ORPHAN_OBJECTS = 0xB;

        public ProviderProbeResult Probe()
        {
            lock (_lock)
            {
                return ProbeInternal();
            }
        }

        private ProviderProbeResult ProbeInternal()
        {
            IAdlxNative? native = null;
            IDisposable? disposableNative = null;
            IntPtr system = IntPtr.Zero;
            IntPtr gpuList = IntPtr.Zero;
            bool initialized = false;
            int initResult = 0;
            bool retryAttempted = false;

            try
            {
                if (!TryAcquireNative(out native, out disposableNative, out system, out gpuList, out initialized, out initResult, out retryAttempted))
                {
                    if (retryAttempted)
                    {
                        return new ProviderProbeResult(false, "Failed to initialize ADLX after retry.");
                    }
                    return new ProviderProbeResult(false, $"Failed to initialize ADLX. Result code: {initResult}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("ADLX Probe: exception during native acquisition", ex);
                return new ProviderProbeResult(false, $"ADLX probe failed: {ex.GetType().Name}: {ex.Message}");
            }

            try
            {
                if (system == IntPtr.Zero)
                {
                    return new ProviderProbeResult(false, $"Failed to initialize ADLX. Result code: {initResult}");
                }

                int gpuListResult = native!.GetGpuList(system, out gpuList);
                if (gpuListResult != 0 || gpuList == IntPtr.Zero)
                {
                    return new ProviderProbeResult(false, $"Failed to get GPU list. Result code: {gpuListResult}");
                }

                int sizeResult = native.GetGpuListSize(gpuList, out int count);
                if (sizeResult != 0)
                {
                    return new ProviderProbeResult(false, $"Failed to get GPU list size. Result code: {sizeResult}");
                }

                bool hasSupportedGpu = false;
                for (int i = 0; i < count; i++)
                {
                    IntPtr gpu = IntPtr.Zero;
                    IntPtr services = IntPtr.Zero;
                    IntPtr metricsSupport = IntPtr.Zero;

                    try
                    {
                        if (native.GetGpuAt(gpuList, i, out gpu) == 0 && gpu != IntPtr.Zero)
                        {
                            if (native.GetPerformanceMonitoringServices(system, out services) == 0 && services != IntPtr.Zero)
                            {
                                if (native.GetSupportedGpuMetrics(services, gpu, out metricsSupport) == 0 && metricsSupport != IntPtr.Zero)
                                {
                                    if (native.IsSupportedGpuTemperature(metricsSupport, out bool tempSupported) == 0 && tempSupported)
                                    {
                                        hasSupportedGpu = true;
                                    }
                                }
                            }
                        }
                    }
                    finally
                    {
                        AdlxInterop.ReleaseInterface(native, ref metricsSupport);
                        AdlxInterop.ReleaseInterface(native, ref services);
                        AdlxInterop.ReleaseInterface(native, ref gpu);
                    }

                    if (hasSupportedGpu)
                    {
                        break;
                    }
                }

                if (!hasSupportedGpu)
                {
                    return new ProviderProbeResult(false, "No GPU supports temperature monitoring via ADLX.");
                }

                return new ProviderProbeResult(true, null);
            }
            catch (Exception ex)
            {
                Logger.LogError("ADLX Probe: exception during GPU enumeration", ex);
                return new ProviderProbeResult(false, $"ADLX probe failed: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                Cleanup(native, ref gpuList, ref system, initialized, disposableNative);
            }
        }

        private bool TryAcquireNative(
            out IAdlxNative? native,
            out IDisposable? disposableNative,
            out IntPtr system,
            out IntPtr gpuList,
            out bool initialized,
            out int initResult,
            out bool retryAttempted)
        {
            native = null;
            disposableNative = null;
            system = IntPtr.Zero;
            gpuList = IntPtr.Zero;
            initialized = false;
            initResult = 0;
            retryAttempted = false;

            if (_pendingDisposeNative != null && _pendingDisposeDisposable != null)
            {
                try
                {
                    _pendingDisposeDisposable.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.LogError("Pending AdlxNative Dispose retry threw exception", ex);
                    return false;
                }
                _pendingDisposeNative = null;
                _pendingDisposeDisposable = null;
            }

            if (_pendingNative != null)
            {
                retryAttempted = true;
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

                if (retryException == null && (retryResult == 0 || retryResult == ADLX_ORPHAN_OBJECTS))
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
                        return false;
                    }
                    _pendingNative = null;
                    _pendingDisposable = null;
                }
                else
                {
                    Logger.LogError($"Retry Terminate failed with HRESULT: 0x{retryResult:X}" + (retryException != null ? $", exception: {retryException.Message}" : ""));
                    return false;
                }
            }

            native = _nativeFactory();
            disposableNative = native as IDisposable;

            try
            {
                initResult = native.Initialize(out system);
            }
            catch
            {
                if (disposableNative != null)
                {
                    try { disposableNative.Dispose(); }
                    catch (Exception ex)
                    {
                        Logger.LogError("AdlxNative Dispose threw exception after Initialize exception", ex);
                        _pendingDisposeNative = native;
                        _pendingDisposeDisposable = disposableNative;
                    }
                }
                throw;
            }

            if (initResult == 0)
            {
                initialized = true;
                return true;
            }

            if (disposableNative != null)
            {
                try { disposableNative.Dispose(); }
                catch (Exception ex)
                {
                    Logger.LogError("AdlxNative Dispose threw exception after Initialize error", ex);
                    _pendingDisposeNative = native;
                    _pendingDisposeDisposable = disposableNative;
                }
            }
            return false;
        }

        public IReadOnlyList<IGpuDevice> EnumerateDevices()
        {
            lock (_lock)
            {
                return EnumerateDevicesInternal();
            }
        }

        private IReadOnlyList<IGpuDevice> EnumerateDevicesInternal()
        {
            var devices = new List<IGpuDevice>();
            var seenUniqueIds = new HashSet<int>();

            IAdlxNative? native = null;
            IDisposable? disposableNative = null;
            IntPtr system = IntPtr.Zero;
            IntPtr gpuList = IntPtr.Zero;
            bool initialized = false;
            int initResult = 0;
            bool retryAttempted = false;

            try
            {
                if (!TryAcquireNative(out native, out disposableNative, out system, out gpuList, out initialized, out initResult, out retryAttempted))
                {
                    return devices;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("ADLX EnumerateDevices: exception during native acquisition", ex);
                return devices;
            }

            try
            {
                if (system == IntPtr.Zero)
                {
                    return devices;
                }

                int gpuListResult = native!.GetGpuList(system, out gpuList);
                if (gpuListResult != 0 || gpuList == IntPtr.Zero)
                {
                    return devices;
                }

                int sizeResult = native.GetGpuListSize(gpuList, out int count);
                if (sizeResult != 0)
                {
                    return devices;
                }

                for (int i = 0; i < count; i++)
                {
                    IntPtr gpu = IntPtr.Zero;
                    IntPtr services = IntPtr.Zero;
                    IntPtr metricsSupport = IntPtr.Zero;

                    try
                    {
                        if (native.GetGpuAt(gpuList, i, out gpu) != 0 || gpu == IntPtr.Zero)
                        {
                            continue;
                        }

                        if (native.GetUniqueIdAndName(system, gpu, out int uniqueId, out string? name))
                        {
                            if (uniqueId <= 0 || string.IsNullOrWhiteSpace(name))
                            {
                                continue;
                            }

                            if (native.IsTemperatureSupported(system, gpu, out bool tempSupported) && tempSupported)
                            {
                                if (seenUniqueIds.Add(uniqueId))
                                {
                                    string deviceId = $"amd-adlx:{uniqueId.ToString(CultureInfo.InvariantCulture)}";
                                    devices.Add(new AmdAdlxDevice(this, deviceId, name!, uniqueId));
                                }
                            }
                        }
                    }
                    finally
                    {
                        AdlxInterop.ReleaseInterface(native, ref metricsSupport);
                        AdlxInterop.ReleaseInterface(native, ref services);
                        AdlxInterop.ReleaseInterface(native, ref gpu);
                    }
                }

                return devices;
            }
            catch (Exception ex)
            {
                Logger.LogError("ADLX EnumerateDevices: exception during GPU enumeration", ex);
                return devices;
            }
            finally
            {
                Cleanup(native, ref gpuList, ref system, initialized, disposableNative);
            }
        }

        internal bool TryReadTemperatureInternal(int targetUniqueId, out TemperatureReading? reading, out string? error)
        {
            lock (_lock)
            {
                return TryReadTemperatureInternalImpl(targetUniqueId, out reading, out error);
            }
        }

        private bool TryReadTemperatureInternalImpl(int targetUniqueId, out TemperatureReading? reading, out string? error)
        {
            reading = null;
            error = null;

            IAdlxNative? native = null;
            IDisposable? disposableNative = null;
            IntPtr system = IntPtr.Zero;
            IntPtr gpuList = IntPtr.Zero;
            bool initialized = false;
            int initResult = 0;
            bool retryAttempted = false;

            try
            {
                if (!TryAcquireNative(out native, out disposableNative, out system, out gpuList, out initialized, out initResult, out retryAttempted))
                {
                    if (retryAttempted)
                    {
                        error = "Failed to initialize ADLX after retry.";
                    }
                    else
                    {
                        error = $"Failed to initialize ADLX. Result code: {initResult}";
                    }
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("ADLX TryReadTemperature: exception during native acquisition", ex);
                error = $"Exception during temperature read: {ex.GetType().Name}: {ex.Message}";
                return false;
            }

            try
            {
                if (system == IntPtr.Zero)
                {
                    error = $"Failed to initialize ADLX. Result code: {initResult}";
                    return false;
                }

                int gpuListResult = native!.GetGpuList(system, out gpuList);
                if (gpuListResult != 0 || gpuList == IntPtr.Zero)
                {
                    error = $"Failed to get GPU list. Result code: {gpuListResult}";
                    return false;
                }

                int sizeResult = native.GetGpuListSize(gpuList, out int count);
                if (sizeResult != 0)
                {
                    error = $"Failed to get GPU list size. Result code: {sizeResult}";
                    return false;
                }

                IntPtr targetGpu = IntPtr.Zero;
                for (int i = 0; i < count; i++)
                {
                    IntPtr gpu = IntPtr.Zero;
                    try
                    {
                        if (native.GetGpuAt(gpuList, i, out gpu) == 0 && gpu != IntPtr.Zero)
                        {
                            if (native.GetGpuUniqueId(gpu, out int uid) == 0 && uid == targetUniqueId)
                            {
                                targetGpu = gpu;
                                break;
                            }
                        }
                    }
                    finally
                    {
                        if (gpu != targetGpu)
                        {
                            AdlxInterop.ReleaseInterface(native, ref gpu);
                        }
                    }
                }

                if (targetGpu == IntPtr.Zero)
                {
                    error = $"GPU with unique ID {targetUniqueId} not found.";
                    return false;
                }

                IntPtr services = IntPtr.Zero;
                IntPtr metricsSupport = IntPtr.Zero;
                IntPtr metrics = IntPtr.Zero;

                try
                {
                    if (native.GetPerformanceMonitoringServices(system, out services) != 0 || services == IntPtr.Zero)
                    {
                        error = "Failed to get performance monitoring services.";
                        return false;
                    }

                    if (native.GetSupportedGpuMetrics(services, targetGpu, out metricsSupport) != 0 || metricsSupport == IntPtr.Zero)
                    {
                        error = "Failed to get supported GPU metrics.";
                        return false;
                    }

                    if (native.IsSupportedGpuTemperature(metricsSupport, out bool tempSupported) != 0 || !tempSupported)
                    {
                        error = "GPU temperature monitoring is not supported.";
                        return false;
                    }

                    if (native.GetCurrentGpuMetrics(services, targetGpu, out metrics) != 0 || metrics == IntPtr.Zero)
                    {
                        error = "Failed to get current GPU metrics.";
                        return false;
                    }

                    if (native.GetGpuTemperature(metrics, out double celsius) != 0)
                    {
                        error = "Failed to get GPU temperature reading.";
                        return false;
                    }

                    if (double.IsNaN(celsius) || double.IsInfinity(celsius))
                    {
                        error = "GPU temperature is NaN or Infinity.";
                        return false;
                    }

                    reading = new TemperatureReading(celsius, "GPU Edge", DateTimeOffset.UtcNow);
                    return true;
                }
                finally
                {
                    AdlxInterop.ReleaseInterface(native, ref metrics);
                    AdlxInterop.ReleaseInterface(native, ref metricsSupport);
                    AdlxInterop.ReleaseInterface(native, ref services);
                    AdlxInterop.ReleaseInterface(native, ref targetGpu);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("ADLX TryReadTemperature: exception during temperature read", ex);
                error = $"Exception during temperature read: {ex.GetType().Name}: {ex.Message}";
                return false;
            }
            finally
            {
                Cleanup(native, ref gpuList, ref system, initialized, disposableNative);
            }
        }

        private void Cleanup(
            IAdlxNative? native,
            ref IntPtr gpuList,
            ref IntPtr system,
            bool initialized,
            IDisposable? disposableNative)
        {
            Logger.LogInfo("Cleanup: entry");

            Logger.LogInfo($"Cleanup: releasing gpuList=0x{gpuList:X}");
            AdlxInterop.ReleaseInterface(native, ref gpuList);
            Logger.LogInfo($"Cleanup: gpuList released");

            Logger.LogInfo($"Cleanup: zeroing system=0x{system:X} (no Release, managed by ADLX)");
            system = IntPtr.Zero;
            Logger.LogInfo($"Cleanup: system zeroed");

            if (initialized && native != null)
            {
                Logger.LogInfo("Cleanup: Terminate start");
                int terminateResult = 0;
                Exception? terminateException = null;

                try
                {
                    terminateResult = native.Terminate();
                }
                catch (Exception ex)
                {
                    terminateException = ex;
                    Logger.LogError("ADLX Terminate threw exception", ex);
                }

                if (terminateException == null && (terminateResult == 0 || terminateResult == ADLX_ORPHAN_OBJECTS))
                {
                    Logger.LogInfo($"Cleanup: Terminate succeeded with HRESULT: 0x{terminateResult:X}");
                    if (disposableNative != null)
                    {
                        Logger.LogInfo("Cleanup: Dispose start");
                        try
                        {
                            disposableNative.Dispose();
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError("AdlxNative Dispose threw exception", ex);
                            _pendingDisposeNative = native;
                            _pendingDisposeDisposable = disposableNative;
                        }
                        Logger.LogInfo("Cleanup: Dispose end");
                    }
                }
                else
                {
                    Logger.LogError($"ADLX Terminate failed with HRESULT: 0x{terminateResult:X}" + (terminateException != null ? $", exception: {terminateException.Message}" : ""));
                    _pendingNative = native;
                    _pendingDisposable = disposableNative;
                }
                Logger.LogInfo("Cleanup: Terminate end");
            }
            else if (disposableNative != null)
            {
                Logger.LogInfo("Cleanup: Dispose start (not initialized)");
                try
                {
                    disposableNative.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.LogError("AdlxNative Dispose threw exception", ex);
                    _pendingDisposeNative = native;
                    _pendingDisposeDisposable = disposableNative;
                }
                Logger.LogInfo("Cleanup: Dispose end");
            }
        }
    }

    internal static class AdlxHelperExtensions
    {
        public static bool GetUniqueIdAndName(this IAdlxNative native, IntPtr system, IntPtr gpu, out int uniqueId, out string? name)
        {
            uniqueId = 0;
            name = null;

            int idResult = native.GetGpuUniqueId(gpu, out uniqueId);
            int nameResult = native.GetGpuName(gpu, out name);

            return idResult == 0 && nameResult == 0;
        }

        public static bool IsTemperatureSupported(this IAdlxNative native, IntPtr system, IntPtr gpu, out bool supported)
        {
            supported = false;
            IntPtr services = IntPtr.Zero;
            IntPtr metricsSupport = IntPtr.Zero;

            try
            {
                if (native.GetPerformanceMonitoringServices(system, out services) != 0 || services == IntPtr.Zero)
                {
                    return false;
                }

                if (native.GetSupportedGpuMetrics(services, gpu, out metricsSupport) != 0 || metricsSupport == IntPtr.Zero)
                {
                    return false;
                }

                if (native.IsSupportedGpuTemperature(metricsSupport, out supported) != 0)
                {
                    supported = false;
                    return false;
                }

                return true;
            }
            finally
            {
                AdlxInterop.ReleaseInterface(native, ref metricsSupport);
                AdlxInterop.ReleaseInterface(native, ref services);
            }
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