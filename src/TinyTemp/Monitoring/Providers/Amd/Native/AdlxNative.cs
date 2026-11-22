using System;
using System.Runtime.InteropServices;

namespace TinyTemp.Monitoring.Providers.Amd.Native;

public sealed class AdlxNative : IAdlxNative, IDisposable
{
    const string DllName = "amdadlx64.dll";
    const ulong ADLX_FULL_VERSION = 0x000200000000007DUL;
    const uint LOAD_LIBRARY_SEARCH_SYSTEM32 = 0x00000800;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    private static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, uint dwFlags);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FreeLibrary(IntPtr hModule);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int ADLXInitializeDelegate(
        ulong amdxVersion,
        out IntPtr ppSystem);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int ADLXTerminateDelegate();

    private IntPtr _libHandle;
    private ADLXInitializeDelegate? _initialize;
    private ADLXTerminateDelegate? _terminate;

    private bool _isLoaded;
    private bool _isInitialized;
    private bool _disposed;

    public AdlxNative()
    {
        LoadLibraryAndGetExports();
    }

    private void LoadLibraryAndGetExports()
    {
        _libHandle = LoadLibraryEx(DllName, IntPtr.Zero, LOAD_LIBRARY_SEARCH_SYSTEM32);
        if (_libHandle == IntPtr.Zero)
        {
            throw new DllNotFoundException($"Failed to load native library '{DllName}'.");
        }

        IntPtr initPtr = GetProcAddress(_libHandle, "ADLXInitialize");
        if (initPtr == IntPtr.Zero)
        {
            FreeLibrary(_libHandle);
            _libHandle = IntPtr.Zero;
            throw new EntryPointNotFoundException("Failed to find export 'ADLXInitialize' in amdadlx64.dll.");
        }

        IntPtr termPtr = GetProcAddress(_libHandle, "ADLXTerminate");
        if (termPtr == IntPtr.Zero)
        {
            FreeLibrary(_libHandle);
            _libHandle = IntPtr.Zero;
            throw new EntryPointNotFoundException("Failed to find export 'ADLXTerminate' in amdadlx64.dll.");
        }

        _initialize = Marshal.GetDelegateForFunctionPointer<ADLXInitializeDelegate>(initPtr);
        _terminate = Marshal.GetDelegateForFunctionPointer<ADLXTerminateDelegate>(termPtr);
        _isLoaded = true;
    }

    public int Initialize(out IntPtr system)
    {
        system = IntPtr.Zero;
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(AdlxNative));
        }
        if (!_isLoaded || _initialize == null)
        {
            throw new InvalidOperationException("ADLX library is not loaded.");
        }
        if (_isInitialized)
        {
            throw new InvalidOperationException("ADLX is already initialized.");
        }

        int result = _initialize(ADLX_FULL_VERSION, out system);
        if (result == 0)
        {
            _isInitialized = true;
        }
        else
        {
            _isInitialized = false;
            system = IntPtr.Zero;
        }
        return result;
    }

    public int Terminate()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(AdlxNative));
        }
        if (!_isLoaded || _terminate == null)
        {
            throw new InvalidOperationException("ADLX library is not loaded.");
        }
        if (!_isInitialized)
        {
            throw new InvalidOperationException("ADLX is not initialized.");
        }

        int result = _terminate();
        if (result == 0)
        {
            _isInitialized = false;
        }
        return result;
    }

    public int GetGpuList(IntPtr system, out IntPtr gpuList)
    {
        gpuList = IntPtr.Zero;
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(AdlxNative));
        }
        if (!_isInitialized)
        {
            throw new InvalidOperationException("ADLX is not initialized.");
        }
        if (system == IntPtr.Zero)
        {
            throw new ArgumentException("System pointer cannot be zero.", nameof(system));
        }

        var del = GetVtableDelegate<GetGPUsDelegate>(system, 1);
        return del(system, out gpuList);
    }

public int GetGpuListSize(IntPtr gpuList, out int count)
        {
            count = 0;
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(AdlxNative));
            }
            if (!_isInitialized)
            {
                throw new InvalidOperationException("ADLX is not initialized.");
            }
            if (gpuList == IntPtr.Zero)
            {
                throw new ArgumentException("GPU list pointer cannot be zero.", nameof(gpuList));
            }

            var del = GetVtableDelegate<SizeDelegate>(gpuList, 3);
            uint uCount = del(gpuList);
            if (uCount > int.MaxValue)
            {
                throw new OverflowException("ADLX GPU list size exceeds the supported range.");
            }

            count = (int)uCount;
            return 0;
        }

    public int GetGpuAt(IntPtr gpuList, int index, out IntPtr gpu)
    {
        gpu = IntPtr.Zero;
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(AdlxNative));
        }
        if (!_isInitialized)
        {
            throw new InvalidOperationException("ADLX is not initialized.");
        }
        if (gpuList == IntPtr.Zero)
        {
            throw new ArgumentException("GPU list pointer cannot be zero.", nameof(gpuList));
        }

        var del = GetVtableDelegate<AtGpuListDelegate>(gpuList, 11);
        return del(gpuList, index, out gpu);
    }

    public int GetGpuName(IntPtr gpu, out string? name)
    {
        name = null;
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(AdlxNative));
        }
        if (!_isInitialized)
        {
            throw new InvalidOperationException("ADLX is not initialized.");
        }
        if (gpu == IntPtr.Zero)
        {
            throw new ArgumentException("GPU pointer cannot be zero.", nameof(gpu));
        }

        var del = GetVtableDelegate<GpuNameDelegate>(gpu, 7);
        int hr = del(gpu, out IntPtr cstrPtr);
        if (hr == 0 && cstrPtr != IntPtr.Zero)
        {
            name = Marshal.PtrToStringAnsi(cstrPtr);
        }
        return hr;
    }

    public int GetGpuUniqueId(IntPtr gpu, out int uniqueId)
    {
        uniqueId = 0;
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(AdlxNative));
        }
        if (!_isInitialized)
        {
            throw new InvalidOperationException("ADLX is not initialized.");
        }
        if (gpu == IntPtr.Zero)
        {
            throw new ArgumentException("GPU pointer cannot be zero.", nameof(gpu));
        }

        var del = GetVtableDelegate<GpuUniqueIdDelegate>(gpu, 18);
        return del(gpu, out uniqueId);
    }

    public int GetPerformanceMonitoringServices(IntPtr system, out IntPtr services)
    {
        services = IntPtr.Zero;
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(AdlxNative));
        }
        if (!_isInitialized)
        {
            throw new InvalidOperationException("ADLX is not initialized.");
        }
        if (system == IntPtr.Zero)
        {
            throw new ArgumentException("System pointer cannot be zero.", nameof(system));
        }

        var del = GetVtableDelegate<GetPerfServicesDelegate>(system, 9);
        return del(system, out services);
    }

    public int GetCurrentGpuMetrics(IntPtr services, IntPtr gpu, out IntPtr metrics)
    {
        metrics = IntPtr.Zero;
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(AdlxNative));
        }
        if (!_isInitialized)
        {
            throw new InvalidOperationException("ADLX is not initialized.");
        }
        if (services == IntPtr.Zero || gpu == IntPtr.Zero)
        {
            throw new ArgumentException("Services and GPU pointers cannot be zero.");
        }

        var del = GetVtableDelegate<GetCurrentMetricsDelegate>(services, 18);
        return del(services, gpu, out metrics);
    }

    public int GetSupportedGpuMetrics(IntPtr services, IntPtr gpu, out IntPtr metricsSupport)
    {
        metricsSupport = IntPtr.Zero;
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(AdlxNative));
        }
        if (!_isInitialized)
        {
            throw new InvalidOperationException("ADLX is not initialized.");
        }
        if (services == IntPtr.Zero || gpu == IntPtr.Zero)
        {
            throw new ArgumentException("Services and GPU pointers cannot be zero.");
        }

        var del = GetVtableDelegate<GetSupportedMetricsDelegate>(services, 21);
        return del(services, gpu, out metricsSupport);
    }

    public int IsSupportedGpuTemperature(IntPtr metricsSupport, out bool supported)
    {
        supported = false;
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(AdlxNative));
        }
        if (!_isInitialized)
        {
            throw new InvalidOperationException("ADLX is not initialized.");
        }
        if (metricsSupport == IntPtr.Zero)
        {
            throw new ArgumentException("Metrics support pointer cannot be zero.", nameof(metricsSupport));
        }

        var del = GetVtableDelegate<IsSupportedTempDelegate>(metricsSupport, 6);
        int hr = del(metricsSupport, out byte bSupported);
        supported = bSupported != 0;
        return hr;
    }

    public int GetGpuTemperature(IntPtr metrics, out double celsius)
    {
        celsius = 0.0;
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(AdlxNative));
        }
        if (!_isInitialized)
        {
            throw new InvalidOperationException("ADLX is not initialized.");
        }
        if (metrics == IntPtr.Zero)
        {
            throw new ArgumentException("Metrics pointer cannot be zero.", nameof(metrics));
        }

        var del = GetVtableDelegate<GpuTempDelegate>(metrics, 7);
        return del(metrics, out celsius);
    }

    public int Release(IntPtr adlxInterface)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(AdlxNative));
        }
        if (adlxInterface == IntPtr.Zero)
        {
            return 0;
        }

        var del = GetVtableDelegate<ReleaseDelegate>(adlxInterface, 1);
        return del(adlxInterface);
    }

    private static TDelegate GetVtableDelegate<TDelegate>(IntPtr instancePtr, int vtableIndex) where TDelegate : Delegate
    {
        IntPtr vtablePtr = Marshal.ReadIntPtr(instancePtr);
        IntPtr methodPtr = Marshal.ReadIntPtr(vtablePtr, vtableIndex * IntPtr.Size);
        return Marshal.GetDelegateForFunctionPointer<TDelegate>(methodPtr);
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetGPUsDelegate(IntPtr pThis, out IntPtr ppGPUList);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate uint SizeDelegate(IntPtr pThis);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int AtGpuListDelegate(IntPtr pThis, int location, out IntPtr ppGPU);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GpuNameDelegate(IntPtr pThis, out IntPtr ppName);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GpuUniqueIdDelegate(IntPtr pThis, out int pUniqueId);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetPerfServicesDelegate(IntPtr pThis, out IntPtr ppPerfMonitoringServices);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetCurrentMetricsDelegate(IntPtr pThis, IntPtr pGPU, out IntPtr ppGPUMetrics);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetSupportedMetricsDelegate(IntPtr pThis, IntPtr pGPU, out IntPtr ppGPUMetricsSupport);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int IsSupportedTempDelegate(IntPtr pThis, out byte pIsSupported);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GpuTempDelegate(IntPtr pThis, out double pTemperature);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int ReleaseDelegate(IntPtr pThis);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_isInitialized)
        {
            throw new InvalidOperationException("Cannot dispose AdlxNative while ADLX system is still initialized. Call Terminate first.");
        }

        _disposed = true;

        if (_libHandle != IntPtr.Zero)
        {
            FreeLibrary(_libHandle);
            _libHandle = IntPtr.Zero;
        }

        _initialize = null;
        _terminate = null;
        _isLoaded = false;
    }
}
