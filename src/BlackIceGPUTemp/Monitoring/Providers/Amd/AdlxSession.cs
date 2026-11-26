using System;
using System.Collections.Generic;
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
        private readonly Dictionary<int, IntPtr> _gpuCache = new Dictionary<int, IntPtr>();

        // Cached performance monitoring services (session-wide)
        private IntPtr _perfServices = IntPtr.Zero;

        // Cached metrics support per GPU (indexed by UniqueId)
        private readonly Dictionary<int, (IntPtr MetricsSupport, bool TempSupported)> _metricsSupportCache = new Dictionary<int, (IntPtr, bool)>();

        public IAdlxNative? PendingNative { get; private set; }
        public IDisposable? PendingDisposable { get; private set; }
        public IAdlxNative? PendingDisposeNative { get; private set; }
        public IDisposable? PendingDisposeDisposable { get; private set; }

        private readonly struct CleanupResult
        {
            public readonly IAdlxNative? PendingNative;
            public readonly IDisposable? PendingDisposable;
            public readonly IAdlxNative? PendingDisposeNative;
            public readonly IDisposable? PendingDisposeDisposable;

            public CleanupResult(
                IAdlxNative? pendingNative,
                IDisposable? pendingDisposable,
                IAdlxNative? pendingDisposeNative,
                IDisposable? pendingDisposeDisposable)
            {
                PendingNative = pendingNative;
                PendingDisposable = pendingDisposable;
                PendingDisposeNative = pendingDisposeNative;
                PendingDisposeDisposable = pendingDisposeDisposable;
            }
        }

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

            // Get GPU unique ID first to check cache
            int uidResult = _native.GetGpuUniqueId(gpu, out int uniqueId);
            if (uidResult != 0 || uniqueId <= 0)
            {
                // If we can't get the unique ID, fall back to non-cached path
                return IsTemperatureSupportedUncached(gpu, out supported, cancellationToken);
            }

            // Check cache for existing metrics support
            if (_metricsSupportCache.TryGetValue(uniqueId, out var cached))
            {
                supported = cached.TempSupported;
                return 0;
            }

            // Not in cache - fetch and cache
            return IsTemperatureSupportedAndCache(gpu, uniqueId, out supported, cancellationToken);
        }

        private int IsTemperatureSupportedUncached(IntPtr gpu, out bool supported, CancellationToken cancellationToken)
        {
            supported = false;
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

        private int IsTemperatureSupportedAndCache(IntPtr gpu, int uniqueId, out bool supported, CancellationToken cancellationToken)
        {
            supported = false;
            IntPtr services = IntPtr.Zero;
            IntPtr metricsSupport = IntPtr.Zero;
            bool metricsSupportOwned = false;

            try
            {
                // Get or create performance monitoring services
                int perfResult = GetOrCreatePerfServices(out services);
                if (perfResult != 0 || services == IntPtr.Zero)
                {
                    return perfResult != 0 ? perfResult : -1;
                }

                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    int metricsResult = _native.GetSupportedGpuMetrics(services, gpu, out metricsSupport);
                    if (metricsResult != 0 || metricsSupport == IntPtr.Zero)
                    {
                        // On error with non-zero pointer, release it
                        if (metricsSupport != IntPtr.Zero)
                        {
                            AdlxInterop.ReleaseInterface(_native, ref metricsSupport);
                        }
                        return metricsResult != 0 ? metricsResult : -1;
                    }

                    // We now own metricsSupport
                    metricsSupportOwned = true;
                }
                finally
                {
                    // If exception occurred after GetSupportedGpuMetrics set the pointer,
                    // and we didn't take ownership, release it
                    if (metricsSupport != IntPtr.Zero && !metricsSupportOwned)
                    {
                        AdlxInterop.ReleaseInterface(_native, ref metricsSupport);
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

                int tempResult = _native.IsSupportedGpuTemperature(metricsSupport, out supported);
                if (tempResult != 0)
                {
                    supported = false;
                    // Release the metricsSupport we just got since we won't cache it
                    AdlxInterop.ReleaseInterface(_native, ref metricsSupport);
                    metricsSupportOwned = false;
                    return tempResult;
                }

                // Success - cache the metricsSupport and temperature support result
                _metricsSupportCache[uniqueId] = (metricsSupport, supported);
                metricsSupportOwned = false; // Ownership transferred to cache
                return 0;
            }
            finally
            {
                // Release metricsSupport if we still own it (exception, cancellation, or failed cache write)
                if (metricsSupportOwned && metricsSupport != IntPtr.Zero)
                {
                    AdlxInterop.ReleaseInterface(_native, ref metricsSupport);
                }
                // Note: services is cached, so we don't release it here
            }
        }

        private int GetOrCreatePerfServices(out IntPtr services)
        {
            services = IntPtr.Zero;
            if (_disposed)
            {
                return -1;
            }

            if (_perfServices != IntPtr.Zero)
            {
                services = _perfServices;
                return 0;
            }

            bool servicesOwned = false;
            try
            {
                int result = _native.GetPerformanceMonitoringServices(_system, out services);
                if (result != 0)
                {
                    // Error from ADLX - if non-zero pointer returned, release it
                    if (services != IntPtr.Zero)
                    {
                        AdlxInterop.ReleaseInterface(_native, ref services);
                    }
                    return result;
                }

                // Success from ADLX - but zero pointer is invalid
                if (services == IntPtr.Zero)
                {
                    return -1;
                }

                // Take ownership of the pointer
                servicesOwned = true;
                _perfServices = services;
                return 0;
            }
            finally
            {
                // If exception occurred after GetPerformanceMonitoringServices set the pointer,
                // and we didn't take ownership, release it
                if (services != IntPtr.Zero && !servicesOwned)
                {
                    AdlxInterop.ReleaseInterface(_native, ref services);
                }
            }
        }

        public int GetPerformanceMonitoringServices(out IntPtr services)
        {
            return GetOrCreatePerfServices(out services);
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

        public bool TryGetCachedGpu(int uniqueId, out IntPtr gpu)
        {
            gpu = IntPtr.Zero;
            if (_disposed)
            {
                return false;
            }
            return _gpuCache.TryGetValue(uniqueId, out gpu);
        }

        public bool TryCacheGpu(int uniqueId, IntPtr gpu)
        {
            if (_disposed || uniqueId <= 0 || gpu == IntPtr.Zero)
            {
                return false;
            }
            if (_gpuCache.ContainsKey(uniqueId))
            {
                return false;
            }
            _gpuCache[uniqueId] = gpu;
            return true;
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
            // Release cached GPU handles first
            foreach (var kvp in _gpuCache)
            {
                IntPtr gpuHandle = kvp.Value;
                if (gpuHandle != IntPtr.Zero)
                {
                    AdlxInterop.ReleaseInterface(_native, ref gpuHandle);
                }
            }
            _gpuCache.Clear();

            // Release cached metricsSupport (child before parent - before services)
            foreach (var kvp in _metricsSupportCache)
            {
                IntPtr metricsSupport = kvp.Value.MetricsSupport;
                if (metricsSupport != IntPtr.Zero)
                {
                    AdlxInterop.ReleaseInterface(_native, ref metricsSupport);
                }
            }
            _metricsSupportCache.Clear();

            // Release cached performance monitoring services
            if (_perfServices != IntPtr.Zero)
            {
                AdlxInterop.ReleaseInterface(_native, ref _perfServices);
            }

            // Release GPU list
            AdlxInterop.ReleaseInterface(_native, ref _gpuList);

            var result = PerformCleanup(_native, _disposableNative, _initialized);
            PendingNative = result.PendingNative;
            PendingDisposable = result.PendingDisposable;
            PendingDisposeNative = result.PendingDisposeNative;
            PendingDisposeDisposable = result.PendingDisposeDisposable;
        }

        private static CleanupResult PerformCleanup(IAdlxNative? native, IDisposable? disposableNative, bool initialized)
        {
            IAdlxNative? pendingNative = null;
            IDisposable? pendingDisposable = null;
            IAdlxNative? pendingDisposeNative = null;
            IDisposable? pendingDisposeDisposable = null;

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

                if (terminateException != null)
                {
                    pendingNative = native;
                    pendingDisposable = disposableNative;
                }
                else if (terminateResult == 0 || terminateResult == ADLX_ORPHAN_OBJECTS)
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
                    Logger.LogError($"ADLX Terminate failed with HRESULT: 0x{terminateResult:X}");
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

            return new CleanupResult(pendingNative, pendingDisposable, pendingDisposeNative, pendingDisposeDisposable);
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
            AdlxInterop.ReleaseInterface(native, ref gpuList);
            system = IntPtr.Zero;

            var result = PerformCleanup(native, disposableNative, initialized);
            pendingNative = result.PendingNative;
            pendingDisposable = result.PendingDisposable;
            pendingDisposeNative = result.PendingDisposeNative;
            pendingDisposeDisposable = result.PendingDisposeDisposable;
        }
    }
}