using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using BlackIceGPUTemp.Monitoring.Abstractions;
using BlackIceGPUTemp.Monitoring.Providers.Amd.Native;

namespace BlackIceGPUTemp.Monitoring.Providers.Amd
{
    public sealed class AmdAdlxProvider : IGpuProvider
    {
        private readonly Func<IAdlxNative> _nativeFactory;

        public string Id => "amd-adlx";
        public string DisplayName => "AMD ADLX";

        public AmdAdlxProvider(Func<IAdlxNative>? nativeFactory = null)
        {
            _nativeFactory = nativeFactory ?? (() => new AdlxNative());
        }

        public ProviderProbeResult Probe()
        {
            IAdlxNative? native = null;
            IDisposable? disposableNative = null;
            IntPtr system = IntPtr.Zero;
            IntPtr gpuList = IntPtr.Zero;
            bool initialized = false;

            try
            {
                native = _nativeFactory();
                disposableNative = native as IDisposable;

                int initResult = native.Initialize(out system);
                if (initResult == 0)
                {
                    initialized = true;
                }
                if (system == IntPtr.Zero || initResult != 0)
                {
                    return new ProviderProbeResult(false, $"Failed to initialize ADLX. Result code: {initResult}");
                }

                int gpuListResult = native.GetGpuList(system, out gpuList);
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
                return new ProviderProbeResult(false, $"ADLX probe failed with exception: {ex.Message}");
            }
            finally
            {
                AdlxInterop.ReleaseInterface(native, ref gpuList);
                AdlxInterop.ReleaseInterface(native, ref system);
                if (initialized && native != null)
                {
                    try
                    {
                        native.Terminate();
                    }
                    catch
                    {
                    }
                }
                disposableNative?.Dispose();
            }
        }

        public IReadOnlyList<IGpuDevice> EnumerateDevices()
        {
            var devices = new List<IGpuDevice>();
            var seenUniqueIds = new HashSet<int>();

            IAdlxNative? native = null;
            IDisposable? disposableNative = null;
            IntPtr system = IntPtr.Zero;
            IntPtr gpuList = IntPtr.Zero;
            bool initialized = false;

            try
            {
                native = _nativeFactory();
                disposableNative = native as IDisposable;

                int initResult = native.Initialize(out system);
                if (initResult == 0)
                {
                    initialized = true;
                }
                if (system == IntPtr.Zero || initResult != 0)
                {
                    return devices;
                }

                int gpuListResult = native.GetGpuList(system, out gpuList);
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
            catch
            {
                return devices;
            }
            finally
            {
                AdlxInterop.ReleaseInterface(native, ref gpuList);
                AdlxInterop.ReleaseInterface(native, ref system);
                if (initialized && native != null)
                {
                    try
                    {
                        native.Terminate();
                    }
                    catch
                    {
                    }
                }
                disposableNative?.Dispose();
            }
        }

        internal bool TryReadTemperatureInternal(int targetUniqueId, out TemperatureReading? reading, out string? error)
        {
            reading = null;
            error = null;

            IAdlxNative? native = null;
            IDisposable? disposableNative = null;
            IntPtr system = IntPtr.Zero;
            IntPtr gpuList = IntPtr.Zero;
            bool initialized = false;

            try
            {
                native = _nativeFactory();
                disposableNative = native as IDisposable;

                int initResult = native.Initialize(out system);
                if (initResult == 0)
                {
                    initialized = true;
                }
                if (system == IntPtr.Zero || initResult != 0)
                {
                    error = $"Failed to initialize ADLX. Result code: {initResult}";
                    return false;
                }

                int gpuListResult = native.GetGpuList(system, out gpuList);
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
                error = $"Exception during temperature read: {ex.Message}";
                return false;
            }
            finally
            {
                AdlxInterop.ReleaseInterface(native, ref gpuList);
                AdlxInterop.ReleaseInterface(native, ref system);
                if (initialized && native != null)
                {
                    try
                    {
                        native.Terminate();
                    }
                    catch
                    {
                    }
                }
                disposableNative?.Dispose();
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
                    if (result != 0)
                    {
                        Trace.TraceError("ADLX Release failed: interface=0x{0:X}, HRESULT=0x{1:X}", currentPtr, result);
                    }
                }
                catch (Exception ex)
                {
                    Trace.TraceError("ADLX Release exception: interface=0x{0:X}, error={1}", currentPtr, ex.Message);
                }
                finally
                {
                    ptr = IntPtr.Zero;
                }
            }
        }
    }
}