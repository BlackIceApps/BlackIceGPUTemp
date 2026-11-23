using System;
using System.Diagnostics;
using BlackIceGPUTemp.Logging;
using BlackIceGPUTemp.Monitoring.Providers.Amd.Native;

namespace BlackIceGPUTemp.Monitoring.Providers.Amd
{
    internal sealed class AdlxSession : IDisposable
    {
        internal const int ADLX_ORPHAN_OBJECTS = 0xB;

        private readonly IAdlxNative _native;
        private readonly IDisposable? _disposableNative;
        private readonly IntPtr _system;
        private IntPtr _gpuList;
        private readonly bool _initialized;
        private bool _disposed;

        public IAdlxNative? PendingNative { get; private set; }
        public IDisposable? PendingDisposable { get; private set; }
        public IAdlxNative? PendingDisposeNative { get; private set; }
        public IDisposable? PendingDisposeDisposable { get; private set; }

        private AdlxSession(
            IAdlxNative native,
            IDisposable? disposableNative,
            IntPtr system,
            IntPtr gpuList,
            bool initialized)
        {
            _native = native;
            _disposableNative = disposableNative;
            _system = system;
            _gpuList = gpuList;
            _initialized = initialized;
        }

        public IntPtr System => _system;
        public IntPtr GpuList => _gpuList;
        public IAdlxNative Native => _native;
        public bool IsInitialized => _initialized;
        public bool IsDisposed => _disposed;

        public static AdlxSession? Create(
            Func<IAdlxNative> nativeFactory,
            out string? error,
            out IAdlxNative? pendingNative,
            out IDisposable? pendingDisposable,
            out IAdlxNative? pendingDisposeNative,
            out IDisposable? pendingDisposeDisposable)
        {
            error = null;
            pendingNative = null;
            pendingDisposable = null;
            pendingDisposeNative = null;
            pendingDisposeDisposable = null;

            IAdlxNative? native = null;
            IDisposable? disposableNative = null;
            IntPtr system = IntPtr.Zero;
            IntPtr gpuList = IntPtr.Zero;
            bool initialized = false;
            int initResult = 0;

            try
            {
                native = nativeFactory();
                disposableNative = native as IDisposable;

                initResult = native.Initialize(out system);
                if (initResult != 0)
                {
                    error = $"Failed to initialize ADLX. Result code: {initResult}";
                    if (disposableNative != null)
                    {
                        try { disposableNative.Dispose(); }
                        catch (Exception ex)
                        {
                            Logger.LogError("AdlxNative Dispose threw exception after Initialize error", ex);
                            pendingDisposeNative = native;
                            pendingDisposeDisposable = disposableNative;
                        }
                    }
                    return null;
                }

                initialized = true;

                if (system == IntPtr.Zero)
                {
                    error = "ADLX initialization succeeded but returned a null system interface.";
                    CleanupRef(native, ref gpuList, ref system, initialized, disposableNative,
                        out pendingNative, out pendingDisposable, out pendingDisposeNative, out pendingDisposeDisposable);
                    return null;
                }

                int gpuListResult = native.GetGpuList(system, out gpuList);
                if (gpuListResult != 0 || gpuList == IntPtr.Zero)
                {
                    error = $"Failed to get GPU list. Result code: {gpuListResult}";
                    CleanupRef(native, ref gpuList, ref system, initialized, disposableNative,
                        out pendingNative, out pendingDisposable, out pendingDisposeNative, out pendingDisposeDisposable);
                    return null;
                }

                return new AdlxSession(native, disposableNative, system, gpuList, initialized);
            }
            catch (Exception ex)
            {
                Logger.LogError("ADLX Session: exception during native acquisition", ex);
                error = $"ADLX session creation failed: {ex.GetType().Name}: {ex.Message}";
                CleanupRef(native, ref gpuList, ref system, initialized, disposableNative,
                    out pendingNative, out pendingDisposable, out pendingDisposeNative, out pendingDisposeDisposable);
                return null;
            }
        }

        public int GetGpuListSize(out int count)
        {
            count = 0;
            if (_disposed || _gpuList == IntPtr.Zero)
            {
                return -1;
            }
            return _native.GetGpuListSize(_gpuList, out count);
        }

        public int GetGpuAt(int index, out IntPtr gpu)
        {
            gpu = IntPtr.Zero;
            if (_disposed || _gpuList == IntPtr.Zero)
            {
                return -1;
            }
            return _native.GetGpuAt(_gpuList, index, out gpu);
        }

        public bool GetUniqueIdAndName(IntPtr gpu, out int uniqueId, out string? name)
        {
            uniqueId = 0;
            name = null;
            if (_disposed)
            {
                return false;
            }

            int idResult = _native.GetGpuUniqueId(gpu, out uniqueId);
            int nameResult = _native.GetGpuName(gpu, out name);

            return idResult == 0 && nameResult == 0;
        }

        public bool IsTemperatureSupported(IntPtr gpu, out bool supported)
        {
            supported = false;
            if (_disposed)
            {
                return false;
            }

            IntPtr services = IntPtr.Zero;
            IntPtr metricsSupport = IntPtr.Zero;

            try
            {
                if (_native.GetPerformanceMonitoringServices(_system, out services) != 0 || services == IntPtr.Zero)
                {
                    return false;
                }

                if (_native.GetSupportedGpuMetrics(services, gpu, out metricsSupport) != 0 || metricsSupport == IntPtr.Zero)
                {
                    return false;
                }

                if (_native.IsSupportedGpuTemperature(metricsSupport, out supported) != 0)
                {
                    supported = false;
                    return false;
                }

                return true;
            }
            finally
            {
                AdlxInterop.ReleaseInterface(_native, ref metricsSupport);
                AdlxInterop.ReleaseInterface(_native, ref services);
            }
        }

        public int GetPerformanceMonitoringServices(out IntPtr services)
        {
            services = IntPtr.Zero;
            if (_disposed)
            {
                return -1;
            }
            return _native.GetPerformanceMonitoringServices(_system, out services);
        }

        public int GetSupportedGpuMetrics(IntPtr services, IntPtr gpu, out IntPtr metricsSupport)
        {
            metricsSupport = IntPtr.Zero;
            if (_disposed)
            {
                return -1;
            }
            return _native.GetSupportedGpuMetrics(services, gpu, out metricsSupport);
        }

        public int IsSupportedGpuTemperature(IntPtr metricsSupport, out bool supported)
        {
            supported = false;
            if (_disposed)
            {
                return -1;
            }
            return _native.IsSupportedGpuTemperature(metricsSupport, out supported);
        }

        public int GetCurrentGpuMetrics(IntPtr services, IntPtr gpu, out IntPtr metrics)
        {
            metrics = IntPtr.Zero;
            if (_disposed)
            {
                return -1;
            }
            return _native.GetCurrentGpuMetrics(services, gpu, out metrics);
        }

        public int GetGpuTemperature(IntPtr metrics, out double celsius)
        {
            celsius = 0.0;
            if (_disposed)
            {
                return -1;
            }
            return _native.GetGpuTemperature(metrics, out celsius);
        }

        public int GetGpuUniqueId(IntPtr gpu, out int uniqueId)
        {
            uniqueId = 0;
            if (_disposed)
            {
                return -1;
            }
            return _native.GetGpuUniqueId(gpu, out uniqueId);
        }

        public int GetGpuType(IntPtr gpu, out int gpuType)
        {
            gpuType = 0;
            if (_disposed)
            {
                return -1;
            }
            return _native.GetGpuType(gpu, out gpuType);
        }

        public void ReleaseInterface(ref IntPtr ptr)
        {
            if (!_disposed && ptr != IntPtr.Zero)
            {
                AdlxInterop.ReleaseInterface(_native, ref ptr);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Cleanup();
            _disposed = true;
        }

        private void Cleanup()
        {
            Logger.LogInfo("AdlxSession Cleanup: entry");

            Logger.LogInfo($"AdlxSession Cleanup: releasing gpuList=0x{_gpuList:X}");
            AdlxInterop.ReleaseInterface(_native, ref _gpuList);
            Logger.LogInfo($"AdlxSession Cleanup: gpuList released");

            Logger.LogInfo($"AdlxSession Cleanup: zeroing system=0x{_system:X} (no Release, managed by ADLX)");

            if (_initialized && _native != null)
            {
                Logger.LogInfo("AdlxSession Cleanup: Terminate start");
                int terminateResult = 0;
                Exception? terminateException = null;

                try
                {
                    terminateResult = _native.Terminate();
                }
                catch (Exception ex)
                {
                    terminateException = ex;
                    Logger.LogError("ADLX Terminate threw exception", ex);
                }

                if (terminateException == null && (terminateResult == 0 || terminateResult == ADLX_ORPHAN_OBJECTS))
                {
                    Logger.LogInfo($"AdlxSession Cleanup: Terminate succeeded with HRESULT: 0x{terminateResult:X}");
                    if (_disposableNative != null)
                    {
                        Logger.LogInfo("AdlxSession Cleanup: Dispose start");
                        try
                        {
                            _disposableNative.Dispose();
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError("AdlxNative Dispose threw exception", ex);
                            PendingDisposeNative = _native;
                            PendingDisposeDisposable = _disposableNative;
                        }
                        Logger.LogInfo("AdlxSession Cleanup: Dispose end");
                    }
                }
                else
                {
                    Logger.LogError($"ADLX Terminate failed with HRESULT: 0x{terminateResult:X}" + (terminateException != null ? $", exception: {terminateException.Message}" : ""));
                    PendingNative = _native;
                    PendingDisposable = _disposableNative;
                }
                Logger.LogInfo("AdlxSession Cleanup: Terminate end");
            }
            else if (_disposableNative != null)
            {
                Logger.LogInfo("AdlxSession Cleanup: Dispose start (not initialized)");
                try
                {
                    _disposableNative.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.LogError("AdlxNative Dispose threw exception", ex);
                    PendingDisposeNative = _native;
                    PendingDisposeDisposable = _disposableNative;
                }
                Logger.LogInfo("AdlxSession Cleanup: Dispose end");
            }
        }

        private static void CleanupRef(
            IAdlxNative? native,
            ref IntPtr gpuList,
            ref IntPtr system,
            bool initialized,
            IDisposable? disposableNative,
            out IAdlxNative? pendingNative,
            out IDisposable? pendingDisposable,
            out IAdlxNative? pendingDisposeNative,
            out IDisposable? pendingDisposeDisposable)
        {
            pendingNative = null;
            pendingDisposable = null;
            pendingDisposeNative = null;
            pendingDisposeDisposable = null;

            Logger.LogInfo("AdlxSession CleanupRef: entry");

            Logger.LogInfo($"AdlxSession CleanupRef: releasing gpuList=0x{gpuList:X}");
            AdlxInterop.ReleaseInterface(native, ref gpuList);
            Logger.LogInfo($"AdlxSession CleanupRef: gpuList released");

            Logger.LogInfo($"AdlxSession CleanupRef: zeroing system=0x{system:X} (no Release, managed by ADLX)");
            system = IntPtr.Zero;
            Logger.LogInfo($"AdlxSession CleanupRef: system zeroed");

            if (initialized && native != null)
            {
                Logger.LogInfo("AdlxSession CleanupRef: Terminate start");
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
                    Logger.LogInfo($"AdlxSession CleanupRef: Terminate succeeded with HRESULT: 0x{terminateResult:X}");
                    if (disposableNative != null)
                    {
                        Logger.LogInfo("AdlxSession CleanupRef: Dispose start");
                        try
                        {
                            disposableNative.Dispose();
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError("AdlxNative Dispose threw exception", ex);
                            pendingDisposeNative = native;
                            pendingDisposeDisposable = disposableNative;
                        }
                        Logger.LogInfo("AdlxSession CleanupRef: Dispose end");
                    }
                }
                else
                {
                    Logger.LogError($"ADLX Terminate failed with HRESULT: 0x{terminateResult:X}" + (terminateException != null ? $", exception: {terminateException.Message}" : ""));
                    pendingNative = native;
                    pendingDisposable = disposableNative;
                }
                Logger.LogInfo("AdlxSession CleanupRef: Terminate end");
            }
            else if (disposableNative != null)
            {
                Logger.LogInfo("AdlxSession CleanupRef: Dispose start (not initialized)");
                try
                {
                    disposableNative.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.LogError("AdlxNative Dispose threw exception", ex);
                    pendingDisposeNative = native;
                    pendingDisposeDisposable = disposableNative;
                }
                Logger.LogInfo("AdlxSession CleanupRef: Dispose end");
            }
        }
    }
}