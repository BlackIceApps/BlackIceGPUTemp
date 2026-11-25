using System;
using System.Diagnostics;
using System.Threading;
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

        public int GetUniqueIdAndName(IntPtr gpu, out int uniqueId, out string? name, CancellationToken cancellationToken = default)
        {
            uniqueId = 0;
            name = null;
            if (_disposed)
            {
                return -1;
            }

            int idResult = _native.GetGpuUniqueId(gpu, out uniqueId);

            cancellationToken.ThrowIfCancellationRequested();

            if (idResult != 0)
            {
                return idResult;
            }

            int nameResult = _native.GetGpuName(gpu, out name);

            return nameResult;
        }

        public int IsTemperatureSupported(IntPtr gpu, out bool supported, CancellationToken cancellationToken = default)
        {
            supported = false;
            if (_disposed)
            {
                return -1;
            }

            IntPtr services = IntPtr.Zero;
            IntPtr metricsSupport = IntPtr.Zero;

            try
            {
                int perfResult = _native.GetPerformanceMonitoringServices(_system, out services);
                if (perfResult != 0 || services == IntPtr.Zero)
                {
                    return perfResult != 0 ? perfResult : -1;
                }

                cancellationToken.ThrowIfCancellationRequested();

                int metricsResult = _native.GetSupportedGpuMetrics(services, gpu, out metricsSupport);
                if (metricsResult != 0 || metricsSupport == IntPtr.Zero)
                {
                    return metricsResult != 0 ? metricsResult : -1;
                }

                cancellationToken.ThrowIfCancellationRequested();

                int tempResult = _native.IsSupportedGpuTemperature(metricsSupport, out supported);
                if (tempResult != 0)
                {
                    supported = false;
                    return tempResult;
                }

                return 0;
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
            AdlxInterop.ReleaseInterface(_native, ref _gpuList);

            if (_initialized && _native != null)
            {
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
                    if (_disposableNative != null)
                    {
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
                    }
                }
                else
                {
                    Logger.LogError($"ADLX Terminate failed with HRESULT: 0x{terminateResult:X}" + (terminateException != null ? $", exception: {terminateException.Message}" : ""));
                    PendingNative = _native;
                    PendingDisposable = _disposableNative;
                }
            }
            else if (_disposableNative != null)
            {
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

            AdlxInterop.ReleaseInterface(native, ref gpuList);
            system = IntPtr.Zero;

            if (initialized && native != null)
            {
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
                    if (disposableNative != null)
                    {
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
                    }
                }
                else
                {
                    Logger.LogError($"ADLX Terminate failed with HRESULT: 0x{terminateResult:X}" + (terminateException != null ? $", exception: {terminateException.Message}" : ""));
                    pendingNative = native;
                    pendingDisposable = disposableNative;
                }
            }
            else if (disposableNative != null)
            {
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
            }
        }
    }
}