using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using BlackIceGPUTemp.Logging;

namespace BlackIceGPUTemp.Monitoring.Providers.Amd.Native;

public sealed class AdlxNative : IAdlxNative, IDisposable
{
    const string DllName = "amdadlx64.dll";
    const ulong ADLX_FULL_VERSION = 0x000200000000007DUL;
    const uint LOAD_LIBRARY_SEARCH_SYSTEM32 = 0x00000800;
    const int ADLX_ORPHAN_OBJECTS = 0xB;

    private enum AdlxVtableIndex : int
    {
        // IADLXInterface (ADLXDefines.h) - base interface for all ADLX objects
        // Standard COM layout: Acquire=0, Release=1, QueryInterface=2
        // Source: AMD ADLX SDK 2.0.0 build 125 - ADLXDefines.h
        IAdlxInterface_Release = 1,

        // IADLXSystem (ISystem.h) - does NOT inherit from IADLXInterface (Inherits: N/A)
        // Vtable starts directly with IADLXSystem methods; no Acquire/Release/QueryInterface at indices 0-2
        // Index 0: GetHybridGraphicsType | Index 1: GetGPUs | Index 9: GetPerformanceMonitoringServices
        // System object is managed by ADLX; do NOT call Release on it.
        // Source: AMD ADLX SDK 2.0.0 build 125 - ISystem.h (verified in diagnostics/BlackIceGPUTemp.Diagnostics/Program.cs:641-642)
        IADLXSystem_GetGPUs = 1,
        IADLXSystem_GetPerformanceMonitoringServices = 9,

        // IADLXGPUList (ICollections.h) - inherits from IADLXList which inherits from IADLXInterface
        // IADLXInterface provides indices 0-2 (Acquire, Release, QueryInterface)
        // IADLXList adds methods; Size at index 3, typed At at index 11
        // Source: AMD ADLX SDK 2.0.0 build 125 - ICollections.h (verified in diagnostics/BlackIceGPUTemp.Diagnostics/Program.cs:671-672)
        IADLXGPUList_Size = 3,
        IADLXGPUList_At = 11,

        // IADLXGPU (IGPU.h) - inherits from IADLXInterface
        // IADLXInterface provides indices 0-2 (Acquire, Release, QueryInterface)
        // Index 5: Type | Index 7: Name | Index 18: UniqueId
        // Source: AMD ADLX SDK 2.0.0 build 125 - IGPU.h (verified in diagnostics/BlackIceGPUTemp.Diagnostics/Program.cs:697 for Name; UniqueId retained from existing implementation)
        IADLXGPU_Type = 5,
        IADLXGPU_Name = 7,
        IADLXGPU_UniqueId = 18,

        // IADLXPerformanceMonitoringServices (IPerformanceMonitoringServices.h) - inherits from IADLXInterface
        // IADLXInterface provides indices 0-2 (Acquire, Release, QueryInterface)
        // Index 18: GetCurrentGPUMetrics | Index 21: GetSupportedGPUMetrics
        // Source: AMD ADLX SDK 2.0.0 build 125 - IPerformanceMonitoringServices.h (verified in diagnostics/BlackIceGPUTemp.Diagnostics/Program.cs:737-738)
        IADLXPerformanceMonitoringServices_GetCurrentGPUMetrics = 18,
        IADLXPerformanceMonitoringServices_GetSupportedGPUMetrics = 21,

        // IADLXGPUMetricsSupport (IPerformanceMonitoring.h) - inherits from IADLXInterface
        // IADLXInterface provides indices 0-2 (Acquire, Release, QueryInterface)
        // Index 6: IsSupportedGPUTemperature
        // Source: AMD ADLX SDK 2.0.0 build 125 - IPerformanceMonitoring.h (verified in diagnostics/BlackIceGPUTemp.Diagnostics/Program.cs:762)
        IADLXGPUMetricsSupport_IsSupportedGPUTemperature = 6,

        // IADLXGPUMetrics (IGPUMetrics.h) - inherits from IADLXInterface
        // IADLXInterface provides indices 0-2 (Acquire, Release, QueryInterface)
        // Index 7: GPUTemperature
        // Source: AMD ADLX SDK 2.0.0 build 125 - IGPUMetrics.h (verified in diagnostics/BlackIceGPUTemp.Diagnostics/Program.cs:793)
        IADLXGPUMetrics_GPUTemperature = 7,
    }

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
    internal delegate int ADLXTerminateDelegate();

    private IntPtr _libHandle;
    private ADLXInitializeDelegate? _initialize;
    private ADLXTerminateDelegate? _terminate;

    private bool _isLoaded;
    private bool _isInitialized;
    private bool _disposed;

    internal Func<IntPtr, bool>? FreeLibraryOverride { get; set; }

    internal bool IsDisposed => _disposed;

    internal IntPtr LibHandle => _libHandle;

    public AdlxNative()
    {
        LoadLibraryAndGetExports();
    }

    internal AdlxNative(IntPtr testLibHandle, Func<IntPtr, bool> testFreeLibrary, bool isInitialized, ADLXTerminateDelegate? testTerminate = null)
    {
        _libHandle = testLibHandle;
        _isLoaded = true;
        _isInitialized = isInitialized;
        FreeLibraryOverride = testFreeLibrary;
        if (testTerminate != null)
        {
            _terminate = testTerminate;
        }
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
        else if (result == ADLX_ORPHAN_OBJECTS)
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

        var del = GetVtableDelegate<GetGPUsDelegate>(system, AdlxVtableIndex.IADLXSystem_GetGPUs);
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

        var del = GetVtableDelegate<SizeDelegate>(gpuList, AdlxVtableIndex.IADLXGPUList_Size);
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

        var del = GetVtableDelegate<AtGpuListDelegate>(gpuList, AdlxVtableIndex.IADLXGPUList_At);
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

        var del = GetVtableDelegate<GpuNameDelegate>(gpu, AdlxVtableIndex.IADLXGPU_Name);
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

        var del = GetVtableDelegate<GpuUniqueIdDelegate>(gpu, AdlxVtableIndex.IADLXGPU_UniqueId);
        return del(gpu, out uniqueId);
    }

    public int GetGpuType(IntPtr gpu, out int gpuType)
    {
        gpuType = 0;
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

        var del = GetVtableDelegate<GpuTypeDelegate>(gpu, AdlxVtableIndex.IADLXGPU_Type);
        return del(gpu, out gpuType);
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

        var del = GetVtableDelegate<GetPerfServicesDelegate>(system, AdlxVtableIndex.IADLXSystem_GetPerformanceMonitoringServices);
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

        var del = GetVtableDelegate<GetCurrentMetricsDelegate>(services, AdlxVtableIndex.IADLXPerformanceMonitoringServices_GetCurrentGPUMetrics);
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

        var del = GetVtableDelegate<GetSupportedMetricsDelegate>(services, AdlxVtableIndex.IADLXPerformanceMonitoringServices_GetSupportedGPUMetrics);
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

        var del = GetVtableDelegate<IsSupportedTempDelegate>(metricsSupport, AdlxVtableIndex.IADLXGPUMetricsSupport_IsSupportedGPUTemperature);
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

        var del = GetVtableDelegate<GpuTempDelegate>(metrics, AdlxVtableIndex.IADLXGPUMetrics_GPUTemperature);
        return del(metrics, out celsius);
    }

    public int Release(IntPtr adlxInterface)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(AdlxNative));
        }
        if (!_isInitialized)
        {
            throw new InvalidOperationException("ADLX is not initialized. Cannot release interface.");
        }
        if (adlxInterface == IntPtr.Zero)
        {
            return 0;
        }

        var del = GetVtableDelegate<ReleaseDelegate>(adlxInterface, AdlxVtableIndex.IAdlxInterface_Release);
        int result = del(adlxInterface);

        return result;
    }

    private static TDelegate GetVtableDelegate<TDelegate>(IntPtr instancePtr, AdlxVtableIndex vtableIndex) where TDelegate : Delegate
    {
        // Safety checks: these ONLY validate that pointers are non-zero.
        // They do NOT verify:
        //   - Function signature compatibility (calling convention, parameter types, return type)
        //   - Validity of non-zero addresses (could point to arbitrary code/data)
        //   - Full vtable layout or ABI compliance
        //   - Interface identity (that instancePtr is actually the expected ADLX interface type)
        // These are defense-in-depth against null/corrupted pointers, not signature validation.

        if (instancePtr == IntPtr.Zero)
        {
            throw new ArgumentException("Instance pointer cannot be zero.", nameof(instancePtr));
        }

        IntPtr vtablePtr = Marshal.ReadIntPtr(instancePtr);
        if (vtablePtr == IntPtr.Zero)
        {
            throw new InvalidOperationException($"Vtable pointer is zero for instance 0x{instancePtr:X}. The ADLX interface may be invalid or corrupted.");
        }

        int index = (int)vtableIndex;
        IntPtr methodPtr = Marshal.ReadIntPtr(vtablePtr, index * IntPtr.Size);
        if (methodPtr == IntPtr.Zero)
        {
            throw new InvalidOperationException($"Vtable method pointer at index {index} (AdlxVtableIndex.{vtableIndex}) is zero for instance 0x{instancePtr:X}. The ADLX interface vtable may be incomplete or corrupted.");
        }

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
    private delegate int GpuTypeDelegate(IntPtr pThis, out int pGpuType);

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

    ~AdlxNative()
    {
        try
        {
            Dispose(false);
        }
        catch (Exception ex)
        {
            try
            {
                Trace.TraceError("Finalizer error: {0}", ex.Message);
            }
            catch
            {
                // Swallow any logging exceptions in finalizer
            }
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (_isInitialized)
        {
            if (disposing)
            {
                throw new InvalidOperationException("ADLX is still initialized. Call Terminate() before Dispose() to properly shut down ADLX.");
            }
            return;
        }

        if (_libHandle != IntPtr.Zero)
        {
            IntPtr handle = _libHandle;
            bool freeResult = false;

            try
            {
                if (FreeLibraryOverride != null)
                {
                    freeResult = FreeLibraryOverride(handle);
                }
                else
                {
                    freeResult = FreeLibrary(handle);
                }
            }
            catch (Exception ex)
            {
                if (disposing)
                {
                    SafeTraceError($"FreeLibrary threw exception: {ex.Message}");
                    throw;
                }
                else
                {
                    SafeTraceError($"FreeLibrary threw exception in finalizer: {ex.Message}");
                }
            }

            if (!freeResult)
            {
                int error = Marshal.GetLastWin32Error();
                if (disposing)
                {
                    SafeTraceError($"FreeLibrary failed: Win32 error {error}");
                    throw new System.ComponentModel.Win32Exception(error, $"Failed to free ADLX library. Win32 error: {error}");
                }
                else
                {
                    SafeTraceError($"FreeLibrary failed in finalizer: Win32 error {error}");
                }
            }
            else
            {
                _libHandle = IntPtr.Zero;
            }
        }

        _disposed = true;
        _initialize = null;
        _terminate = null;
        _isLoaded = false;
    }

    private static void SafeTraceError(string message)
    {
        try
        {
            Trace.TraceError(message);
        }
        catch
        {
        }
    }
}
