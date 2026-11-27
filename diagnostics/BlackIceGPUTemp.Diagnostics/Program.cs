using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Collections.Generic;
using LibreHardwareMonitor.Hardware;

namespace BlackIceGPUTemp.Diagnostics
{
    class Program
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct RTL_OSVERSIONINFOEX
        {
            public uint dwOSVersionInfoSize;
            public uint dwMajorVersion;
            public uint dwMinorVersion;
            public uint dwBuildNumber;
            public uint dwPlatformId;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szCSDVersion;
        }

        [DllImport("ntdll.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        static extern int RtlGetVersion(ref RTL_OSVERSIONINFOEX lpVersionInformation);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, uint dwFlags);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool FreeLibrary(IntPtr hModule);

        const uint LOAD_LIBRARY_SEARCH_SYSTEM32 = 0x00000800;

        // AMD ADL AdapterInfo structure (1572 bytes) matching the C# declaration below.
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct AdapterInfo
        {
            public int iSize;
            public int iAdapterIndex;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string strUDID;
            public int iBusNumber;
            public int iDeviceNumber;
            public int iFunctionNumber;
            public int iVendorID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string strAdapterName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string strDisplayName;
            public int iPresent;
            public int iExist;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string strDriverPath;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string strDriverPathExt;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string strPNPString;
            public int iOSDisplayIndex;
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate int ADL_Main_Control_Create(IntPtr callback, int enumConnectedAdapters);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate int ADL_Main_Control_Destroy();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate int ADL_Adapter_NumberOfAdapters_Get(ref int numAdapters);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate int ADL_Adapter_AdapterInfo_Get(IntPtr info, int size);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate int ADL_Adapter_Active_Get(int adapterIndex, ref int status);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate int ADL_Overdrive_Caps(int adapterIndex, ref int supported, ref int enabled, ref int version);

        [StructLayout(LayoutKind.Sequential)]
        public struct ADLTemperature
        {
            public int Size;
            public int Temperature;
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate int ADL_Overdrive5_Temperature_Get(int adapterIndex, int thermalControllerIndex, ref ADLTemperature temperature);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate int ADL_Overdrive6_Temperature_Get(int adapterIndex, ref int temperature);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate int ADL2_Main_Control_Create(IntPtr callback, int enumConnectedAdapters, ref IntPtr context);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate int ADL2_Main_Control_Destroy(IntPtr context);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate int ADL2_OverdriveN_Temperature_Get(IntPtr context, int adapterIndex, int temperatureType, ref int temperature);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate IntPtr ADL_Main_Memory_Alloc(int size);

        // Official ADLX C-API SDK signatures (from AMD ADLX documentation & ADLXHelper C# bindings):
        // ADLX_RESULT ADLXInitialize (adlx_uint64 version, IADLXSystem** ppSystem) -> ADLXInitialize(ulong version, ref IntPtr ppSystem)
        // ADLX_RESULT ADLXTerminate () -> ADLXTerminate()
        // ADLX_RESULT ADLXQueryFullVersion (adlx_uint64* version) -> ADLXQueryFullVersion(ref ulong version)
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate int ADLXInitialize(ulong version, ref IntPtr ppSystem);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate int ADLXTerminate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate int ADLXQueryFullVersion(ref ulong version);

        // ADLX COM Interface Vtable Delegates for GPU Enumeration (Step 1) and Performance Monitoring (Step 2)
        // Based on official AMD ADLX C-API SDK headers (ISystem.h, ICollections.h, IGPU.h, IPerformanceMonitoringServices.h, IPerformanceMonitoring.h, IGPUMetrics.h, IInterface.h):
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int ADLX_IADLXSystem_GetGPUs(IntPtr adlxSystem, ref IntPtr ppGPUList);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate uint ADLX_IADLXGPUList_Size(IntPtr adlxGPUList);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int ADLX_IADLXGPUList_At(IntPtr adlxGPUList, uint location, ref IntPtr ppGPU);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int ADLX_IADLXGPU_Name(IntPtr adlxGPU, ref IntPtr ppName);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int ADLX_IADLXSystem_GetPerformanceMonitoringServices(IntPtr adlxSystem, ref IntPtr ppPerfMonitoringServices);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int ADLX_IADLXPerformanceMonitoringServices_GetCurrentGPUMetrics(IntPtr adlxPerfServices, IntPtr pGPU, ref IntPtr ppGPUMetrics);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int ADLX_IADLXPerformanceMonitoringServices_GetSupportedGPUMetrics(IntPtr adlxPerfServices, IntPtr pGPU, ref IntPtr ppMetricsSupport);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int ADLX_IADLXGPUMetricsSupport_IsSupportedGPUTemperature(IntPtr adlxMetricsSupport, ref byte supported);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int ADLX_IADLXGPUMetrics_GPUTemperature(IntPtr adlxGPUMetrics, ref double temperature);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int ADLX_IADLXInterface_Release(IntPtr adlxInterface);

        static StringBuilder logBuilder = new StringBuilder();
        static string exactReportPath = string.Empty;

        // Helper to safely read a function pointer from a COM vtable
        static bool TryGetVtableFunction(IntPtr instancePtr, int vtableIndex, out IntPtr functionPtr, out string error)
        {
            functionPtr = IntPtr.Zero;
            error = null;

            if (instancePtr == IntPtr.Zero)
            {
                error = "Instance pointer is zero";
                return false;
            }

            IntPtr vtablePtr;
            try
            {
                vtablePtr = Marshal.ReadIntPtr(instancePtr);
            }
            catch (Exception ex)
            {
                error = $"Failed to read vtable pointer: {ex.Message}";
                return false;
            }

            if (vtablePtr == IntPtr.Zero)
            {
                error = "Vtable pointer is zero";
                return false;
            }

            try
            {
                functionPtr = Marshal.ReadIntPtr(vtablePtr, vtableIndex * IntPtr.Size);
            }
            catch (Exception ex)
            {
                error = $"Failed to read vtable slot {vtableIndex}: {ex.Message}";
                return false;
            }

            if (functionPtr == IntPtr.Zero)
            {
                error = $"Function pointer at vtable index {vtableIndex} is zero";
                return false;
            }

            return true;
        }

        // Helper to safely release an ADLX interface
        static bool TryReleaseAdlxInterface(IntPtr interfacePtr, string interfaceName, out int refCount, out string error)
        {
            refCount = -1;
            error = null;

            if (interfacePtr == IntPtr.Zero)
            {
                error = $"{interfaceName} pointer is zero, nothing to release";
                return false;
            }

            if (!TryGetVtableFunction(interfacePtr, 1, out IntPtr releasePtr, out error))
            {
                error = $"{interfaceName} release failed: {error}";
                return false;
            }

            try
            {
                var release = Marshal.GetDelegateForFunctionPointer<ADLX_IADLXInterface_Release>(releasePtr);
                refCount = release(interfacePtr);
                return true;
            }
            catch (Exception ex)
            {
                error = $"{interfaceName} release exception: {ex.Message}";
                return false;
            }
        }

        static void Log(string message)
        {
            Console.WriteLine(message);
            logBuilder.AppendLine(message);
        }

        static void Main(string[] args)
        {
            Log("=== BlackIceGPUTemp Diagnostics Tool ===");
            
            RTL_OSVERSIONINFOEX osvi = new RTL_OSVERSIONINFOEX();
            osvi.dwOSVersionInfoSize = (uint)Marshal.SizeOf(typeof(RTL_OSVERSIONINFOEX));
            osvi.szCSDVersion = new string('\0', 128);
            int ntStatus = RtlGetVersion(ref osvi);
            if (ntStatus == 0)
            {
                Log($"OS Version: Microsoft Windows NT {osvi.dwMajorVersion}.{osvi.dwMinorVersion}.{osvi.dwBuildNumber} ({osvi.szCSDVersion})");
}
                                            }
                                    else
            {
                Log("OS Version: Niedostępny odczyt wersji systemu (RtlGetVersion nie powiódł się).");
            }

            Log($"Is 64-bit Process: {Environment.Is64BitProcess}");
            
            string adminStatus = "Unknown";
            try
            {
                using (var identity = System.Security.Principal.WindowsIdentity.GetCurrent())
                {
                    var principal = new System.Security.Principal.WindowsPrincipal(identity);
                    adminStatus = principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator) ? "Yes" : "No";
                }
            }
            catch (Exception ex)
            {
                adminStatus = $"Unknown (Błąd: {ex.Message})";
            }
            Log($"Administrator: {adminStatus}");
            Log("");

            // 1. DLL checks
            CheckDll("atiadlxx.dll");
            CheckDll("amdadlx64.dll");
            Log("");

            // 2. Full ADL Diagnostics
            RunAdlDiagnostics();
            Log("");

            // 3. Full ADLX Diagnostics
            RunAdlxDiagnostics();
            Log("");

            // 4. LibreHardwareMonitor Diagnostics
            RunLhmDiagnostics();
            Log("");

            // Save dump and record exact path
            SaveDump();

            Log("");
            Log($"Exact report path: {exactReportPath}");
            Log("Press Enter to exit...");
            Console.ReadLine();
        }

        static void CheckDll(string dllName)
        {
            string system32Path = Path.Combine(Environment.SystemDirectory, dllName);
            bool exists = File.Exists(system32Path);
            Log($"[{dllName}] Path: {system32Path} | Exists: {exists}");

            if (exists)
            {
                IntPtr handle = IntPtr.Zero;
                try
                {
                    var vi = FileVersionInfo.GetVersionInfo(system32Path);
                    Log($"[{dllName}] Version: {vi.FileVersion} (Product: {vi.ProductVersion})");

                    handle = LoadLibraryEx(system32Path, IntPtr.Zero, LOAD_LIBRARY_SEARCH_SYSTEM32);
                    if (handle == IntPtr.Zero)
                    {
                        Log($"[{dllName}] LoadLibraryEx failed with Error: {Marshal.GetLastWin32Error()}");
                        return;
                    }

                    if (dllName.Equals("atiadlxx.dll", StringComparison.OrdinalIgnoreCase))
                    {
                        IntPtr proc = GetProcAddress(handle, "ADL_Main_Control_Create");
                        if (proc == IntPtr.Zero)
                        {
                            Log($"[{dllName}] LoadLibraryEx Handle: {handle}, GetProcAddress (ADL_Main_Control_Create) failed with Error: {Marshal.GetLastWin32Error()}");
                        }
                        else
                        {
                            Log($"[{dllName}] LoadLibraryEx Handle: {handle}, GetProcAddress (ADL_Main_Control_Create): {proc} (Found successfully)");
                        }
                    }
                    else if (dllName.Equals("amdadlx64.dll", StringComparison.OrdinalIgnoreCase))
                    {
                        IntPtr procVer = GetProcAddress(handle, "ADLXQueryFullVersion");
                        string verStatus = procVer == IntPtr.Zero 
                            ? $"failed with Error: {Marshal.GetLastWin32Error()}" 
                            : $"{procVer} (Found successfully)";

                        IntPtr procInit = GetProcAddress(handle, "ADLXInitialize");
                        string initStatus = procInit == IntPtr.Zero 
                            ? $"failed with Error: {Marshal.GetLastWin32Error()}" 
                            : $"{procInit} (Found successfully)";

                        Log($"[{dllName}] LoadLibraryEx Handle: {handle}, GetProcAddress (ADLXQueryFullVersion): {verStatus}, (ADLXInitialize): {initStatus}");
                    }
                }
                catch (Exception ex)
                {
                    Log($"[{dllName}] Error inspecting/loading: {ex.Message}");
                }
                finally
                {
                    if (handle != IntPtr.Zero)
                    {
                        FreeLibrary(handle);
                    }
                }
            }
        }

        static IntPtr GetAdlDllHandle()
        {
            string system32Path = Path.Combine(Environment.SystemDirectory, "atiadlxx.dll");
            if (!File.Exists(system32Path)) return IntPtr.Zero;
            return LoadLibraryEx(system32Path, IntPtr.Zero, LOAD_LIBRARY_SEARCH_SYSTEM32);
        }

        static IntPtr GetAdlxDllHandle()
        {
            string system32Path = Path.Combine(Environment.SystemDirectory, "amdadlx64.dll");
            if (!File.Exists(system32Path)) return IntPtr.Zero;
            return LoadLibraryEx(system32Path, IntPtr.Zero, LOAD_LIBRARY_SEARCH_SYSTEM32);
        }

        static IntPtr AllocateMemory(int size)
        {
            if (size <= 0)
            {
                return IntPtr.Zero;
            }

            try
            {
                return Marshal.AllocHGlobal(size);
            }
            catch
            {
                return IntPtr.Zero;
            }
        }

        static readonly ADL_Main_Memory_Alloc allocCallback = AllocateMemory;

        static void RunAdlDiagnostics()
        {
            Log("=== ADL Diagnostics ===");
            IntPtr dllHandle = GetAdlDllHandle();
            if (dllHandle == IntPtr.Zero)
            {
                Log("ADL: atiadlxx.dll not available or failed to load.");
                return;
            }

            try
            {
                IntPtr createPtr = GetProcAddress(dllHandle, "ADL_Main_Control_Create");
                IntPtr destroyPtr = GetProcAddress(dllHandle, "ADL_Main_Control_Destroy");
                IntPtr numAdaptersPtr = GetProcAddress(dllHandle, "ADL_Adapter_NumberOfAdapters_Get");
                IntPtr adapterInfoPtr = GetProcAddress(dllHandle, "ADL_Adapter_AdapterInfo_Get");
                IntPtr activeGetPtr = GetProcAddress(dllHandle, "ADL_Adapter_Active_Get");
                IntPtr overdriveCapsPtr = GetProcAddress(dllHandle, "ADL_Overdrive_Caps");
                IntPtr od5TempPtr = GetProcAddress(dllHandle, "ADL_Overdrive5_Temperature_Get");
                IntPtr od6TempPtr = GetProcAddress(dllHandle, "ADL_Overdrive6_Temperature_Get");
                IntPtr adl2CreatePtr = GetProcAddress(dllHandle, "ADL2_Main_Control_Create");
                IntPtr adl2DestroyPtr = GetProcAddress(dllHandle, "ADL2_Main_Control_Destroy");
                IntPtr odnTempPtr = GetProcAddress(dllHandle, "ADL2_OverdriveN_Temperature_Get");

                if (createPtr == IntPtr.Zero || destroyPtr == IntPtr.Zero || numAdaptersPtr == IntPtr.Zero || adapterInfoPtr == IntPtr.Zero)
                {
                    Log("ADL: Required entry points missing from atiadlxx.dll.");
                    return;
                }

                var create = Marshal.GetDelegateForFunctionPointer<ADL_Main_Control_Create>(createPtr);
                var destroy = Marshal.GetDelegateForFunctionPointer<ADL_Main_Control_Destroy>(destroyPtr);
                var numAdaptersGet = Marshal.GetDelegateForFunctionPointer<ADL_Adapter_NumberOfAdapters_Get>(numAdaptersPtr);
                var adapterInfoGet = Marshal.GetDelegateForFunctionPointer<ADL_Adapter_AdapterInfo_Get>(adapterInfoPtr);

                IntPtr callbackPtr = Marshal.GetFunctionPointerForDelegate(allocCallback);

                int res = create(callbackPtr, 1);
                if (res != 0)
                {
                    Log($"ADL_Main_Control_Create Result: {res}");
                    Log("ADL initialization failed or returned non-zero status.");
                    return;
                }

                try
                {
                    Log($"ADL_Main_Control_Create Result: {res}");

                    int numAdapters = 0;
                    int retNum = numAdaptersGet(ref numAdapters);
                    Log($"ADL_Adapter_NumberOfAdapters_Get Result: {retNum}, NumAdapters: {numAdapters}");

                    if (retNum == 0 && numAdapters > 0)
                    {
                        int adapterInfoSize = Marshal.SizeOf(typeof(AdapterInfo));
                        Log($"[ADL] AdapterInfo struct size: {adapterInfoSize} bytes");

                        if (adapterInfoSize != 1572)
                        {
                            Log("[ADL] ERROR: Unexpected AdapterInfo struct size. Skipping ADL adapter enumeration.");
                            return;
                        }

                        int structSize = adapterInfoSize;
                        int totalSize = structSize * numAdapters;
                        IntPtr ptr = Marshal.AllocHGlobal(totalSize);
                        try
                        {
                            // Initialize each AdapterInfo struct slot with its size before calling AdapterInfo_Get
                            for (int i = 0; i < numAdapters; i++)
                            {
                                IntPtr currentSlot = IntPtr.Add(ptr, i * structSize);
                                Marshal.WriteInt32(currentSlot, structSize);
                            }

                            int retInfo = adapterInfoGet(ptr, totalSize);
                            Log($"ADL_Adapter_AdapterInfo_Get Result: {retInfo}");
                            if (retInfo == 0)
                            {
                                for (int i = 0; i < numAdapters; i++)
                                {
                                    IntPtr currentPtr = IntPtr.Add(ptr, i * structSize);
                                    AdapterInfo info = Marshal.PtrToStructure<AdapterInfo>(currentPtr);
                                    Log($"  Adapter {i}: Name={info.strAdapterName}, Bus={info.iBusNumber}, Device={info.iDeviceNumber}, Function={info.iFunctionNumber}, VendorID={info.iVendorID}, AdapterIndex={info.iAdapterIndex}, Present={info.iPresent}, Exist={info.iExist}, OSDisplayIndex={info.iOSDisplayIndex}, UDID={info.strUDID}, DisplayName={info.strDisplayName}");

                                     int activeStatus = -1;
                                     if (activeGetPtr != IntPtr.Zero)
                                     {
                                         var activeGet = Marshal.GetDelegateForFunctionPointer<ADL_Adapter_Active_Get>(activeGetPtr);
                                         int retActive = activeGet(info.iAdapterIndex, ref activeStatus);
                                         Log($"    ADL_Adapter_Active_Get Result: {retActive}, Active: {activeStatus}");
                                     }

                                    if (overdriveCapsPtr != IntPtr.Zero)
                                    {
                                        try
                                        {
                                            var odCaps = Marshal.GetDelegateForFunctionPointer<ADL_Overdrive_Caps>(overdriveCapsPtr);
                                            int supported = 0, enabled = 0, version = 0;
                                            int retCaps = odCaps(info.iAdapterIndex, ref supported, ref enabled, ref version);
                                            Log($"    ADL_Overdrive_Caps Result: {retCaps}, Supported={supported}, Enabled={enabled}, Version={version}");
                                        }
                                        catch (Exception ex)
                                        {
                                            Log($"    ADL_Overdrive_Caps Error: {ex.Message}");
                                        }
                                    }

                                    if (od5TempPtr != IntPtr.Zero)
                                    {
                                        try
                                        {
                                            var od5 = Marshal.GetDelegateForFunctionPointer<ADL_Overdrive5_Temperature_Get>(od5TempPtr);
                                            ADLTemperature temp = new ADLTemperature { Size = Marshal.SizeOf(typeof(ADLTemperature)) };
                                            int retOd5 = od5(info.iAdapterIndex, 0, ref temp);
                                            if (retOd5 == 0)
                                            {
                                                Log($"    ADL_Overdrive5_Temperature_Get (Diagnostic check with index 0) Result: {retOd5}, Temperature={temp.Temperature / 1000.0} °C (Raw: {temp.Temperature}) [VERIFIED SUCCESS]");
                                            }
                                            else
                                            {
                                                Log($"    ADL_Overdrive5_Temperature_Get (Diagnostic check with index 0) Result: {retOd5} (API returned non-zero error code - unsupported or controller inactive)");
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Log($"    ADL_Overdrive5_Temperature_Get Error: {ex.Message}");
                                        }
                                    }

                                    if (od6TempPtr != IntPtr.Zero)
                                    {
                                        try
                                        {
                                            var od6 = Marshal.GetDelegateForFunctionPointer<ADL_Overdrive6_Temperature_Get>(od6TempPtr);
                                            int od6TempVal = 0;
                                            int retOd6 = od6(info.iAdapterIndex, ref od6TempVal);
                                            if (retOd6 == 0)
                                            {
                                                Log($"    ADL_Overdrive6_Temperature_Get Result: {retOd6}, Temperature={od6TempVal / 1000.0} °C (Raw: {od6TempVal}) [VERIFIED SUCCESS]");
                                            }
                                            else
                                            {
                                                Log($"    ADL_Overdrive6_Temperature_Get Result: {retOd6} (API returned non-zero error code - unsupported or controller inactive)");
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Log($"    ADL_Overdrive6_Temperature_Get Error: {ex.Message}");
                                        }
                                    }

                                    if (info.iVendorID == 1002 && activeStatus == 1)
                                    {
                                        if (adl2CreatePtr == IntPtr.Zero || adl2DestroyPtr == IntPtr.Zero || odnTempPtr == IntPtr.Zero)
                                        {
                                            Log("    ADL2_OverdriveN_Temperature_Get: Eksport funkcji ADL2 Create/Destroy lub OverdriveN nie istnieje w atiadlxx.dll.");
                                        }
                                        else
                                        {
                                            IntPtr adl2Context = IntPtr.Zero;
                                            bool adl2Created = false;
                                            try
                                            {
                                                var adl2Create = Marshal.GetDelegateForFunctionPointer<ADL2_Main_Control_Create>(adl2CreatePtr);
                                                int resAdl2 = adl2Create(callbackPtr, 1, ref adl2Context);
                                                if (resAdl2 != 0)
                                                {
                                                    Log($"    ADL2_Main_Control_Create Result: {resAdl2} (Kontekst ADL2 nie został utworzony)");
                                                }
                                                else if (adl2Context == IntPtr.Zero)
                                                {
                                                    Log("    ADL2_Main_Control_Create zwrócił sukces, ale wskaźnik kontekstu jest pusty (IntPtr.Zero).");
                                                }
                                                else
                                                {
                                                    adl2Created = true;
                                                    try
                                                    {
                                                        var odn = Marshal.GetDelegateForFunctionPointer<ADL2_OverdriveN_Temperature_Get>(odnTempPtr);
                                                        int rawTemp = 0;
                                                        int retOdn = odn(adl2Context, info.iAdapterIndex, 1, ref rawTemp);
                                                        if (retOdn == 0)
                                                        {
                                                            double tempC = rawTemp / 1000.0;
                                                            Log($"    ADL2_OverdriveN_Temperature_Get (AdapterIndex: {info.iAdapterIndex}, GPU: {info.strAdapterName}, ExportFound: True, ResultCode: {retOdn}, RawValue: {rawTemp}, TemperatureC: {tempC} °C, Status: SUCCESS)");
                                                        }
                                                        else
                                                        {
                                                            Log($"    ADL2_OverdriveN_Temperature_Get (AdapterIndex: {info.iAdapterIndex}, GPU: {info.strAdapterName}, ExportFound: True, ResultCode: {retOdn}, RawValue: {rawTemp}, TemperatureC: N/A, Status: ERROR)");
                                                        }
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        Log($"    ADL2_OverdriveN_Temperature_Get Error: {ex.Message}");
                                                    }
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                Log($"    ADL2_OverdriveN_Temperature_Get / Context Error: {ex.Message}");
                                            }
                                            finally
                                            {
                                                if (adl2Created && adl2Context != IntPtr.Zero)
                                                {
                                                    try
                                                    {
                                                        var adl2Destroy = Marshal.GetDelegateForFunctionPointer<ADL2_Main_Control_Destroy>(adl2DestroyPtr);
                                                        int destroyRes2 = adl2Destroy(adl2Context);
                                                        Log($"    ADL2_Main_Control_Destroy Result: {destroyRes2}");
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        Log(String.Format("    ADL2_Main_Control_Destroy Exception: {0}", ex.Message));
                                                    }
                                                }
                                            }
                                        }
}
                                                        }
finally
                        {
                            Marshal.FreeHGlobal(ptr);
                        }
                    }
                }
                finally
                {
                    int destroyRes = destroy();
                    Log($"ADL_Main_Control_Destroy Result: {destroyRes}");
                }
            }
            catch (Exception ex)
            {
                Log($"ADL Diagnostics Exception: {ex.Message}");
            }
            finally
            {
                bool freed = FreeLibrary(dllHandle);
                Log($"[ADL] FreeLibrary Result: {freed}");
            }
        }

        static void RunAdlxDiagnostics()
        {
            Log("=== ADLX Diagnostics ===");
            IntPtr dllHandle = GetAdlxDllHandle();
            if (dllHandle == IntPtr.Zero)
            {
                Log("ADLX: amdadlx64.dll not found in System32 or failed to load.");
                Log("  [UNAVAILABLE] Actual ADLX GPU enumeration requires a native C++ wrapper DLL due to strict ABI/COM layout differences across driver versions.");
                return;
            }

            ADLXTerminate? terminate = null;
            bool initialized = false;

            try
            {
                Log("ADLX DLL loaded successfully via LoadLibraryEx.");

                IntPtr verPtr = GetProcAddress(dllHandle, "ADLXQueryFullVersion");
                if (verPtr != IntPtr.Zero)
                {
                    var queryVersion = Marshal.GetDelegateForFunctionPointer<ADLXQueryFullVersion>(verPtr);
                    ulong version = 0;
                    int retVer = queryVersion(ref version);
                    if (retVer == 0)
                    {
                        Log(String.Format("ADLXQueryFullVersion Result: {0}, FullVersion: {1} [VERIFIED SUCCESS]", retVer, version));
                    }
                    else
                    {
                        Log(String.Format("ADLXQueryFullVersion Result: {0}, FullVersion: {1} (API returned non-zero error code)", retVer, version));
                    }
                }
                else
                {
                    Log("ADLXQueryFullVersion export not found.");
                }

                IntPtr termPtr = GetProcAddress(dllHandle, "ADLXTerminate");
                if (termPtr == IntPtr.Zero)
                {
                    Log("ADLXTerminate export not found. Skipping ADLXInitialize.");
                }
                else
                {
                    terminate = Marshal.GetDelegateForFunctionPointer<ADLXTerminate>(termPtr);

                    IntPtr initPtr = GetProcAddress(dllHandle, "ADLXInitialize");
                    if (initPtr != IntPtr.Zero)
                    {
                        var initialize = Marshal.GetDelegateForFunctionPointer<ADLXInitialize>(initPtr);
                        IntPtr adlxSystem = IntPtr.Zero;
                        ulong clientVersion = 0x000200000000007DUL; // ADLX_FULL_VERSION calculated via ADLX_MAKE_FULL_VER(2, 0, 0, 125) from AMD ADLX SDK version 2.0.0.125 (ADLXVersion.h)

                        int retInit = initialize(clientVersion, ref adlxSystem);
                        if (retInit == 0)
                        {
                            initialized = true;
                        }
                        Log(String.Format("ADLXInitialize Result: {0}, SystemInterfacePtr: {1}", retInit, adlxSystem));

                        IntPtr adlxGPUList = IntPtr.Zero;
                        IntPtr adlxPerfServices = IntPtr.Zero;
                        int retGetGPUs = -1;
                        int retGetPerfServices = -1;
                        uint gpuCount = 0;

try
                            {
                                if (retInit == 0 && adlxSystem != IntPtr.Zero)
                                {
                                     // 1. Pobierz IADLXPerformanceMonitoringServices z IADLXSystem
                                     // IADLXSystem::GetPerformanceMonitoringServices is at vtable index 9 (ISystem.h)
                                     IntPtr getGPUsPtr, getPerfServicesPtr;

                                     // GetGPUs (slot 1) - independent
                                     if (!TryGetVtableFunction(adlxSystem, 1, out getGPUsPtr, out string errGetGPUs))
                                     {
                                         Log($"ADLX IADLXSystem::GetGPUs vtable read failed: {errGetGPUs}");
                                         retGetGPUs = -1;
                                     }
                                     else
                                     {
                                         var getGPUs = Marshal.GetDelegateForFunctionPointer<ADLX_IADLXSystem_GetGPUs>(getGPUsPtr);
                                         retGetGPUs = getGPUs(adlxSystem, ref adlxGPUList);
                                         Log(String.Format("ADLX IADLXSystem::GetGPUs Result: {0}, GPUListPtr: {1}", retGetGPUs, adlxGPUList));
                                     }

                                     // GetPerformanceMonitoringServices (slot 9) - independent
                                     if (!TryGetVtableFunction(adlxSystem, 9, out getPerfServicesPtr, out string errGetPerfServices))
                                     {
                                         Log($"ADLX IADLXSystem::GetPerformanceMonitoringServices vtable read failed: {errGetPerfServices}");
                                         retGetPerfServices = -1;
                                     }
                                     else if (getPerfServicesPtr != IntPtr.Zero)
                                     {
                                         var getPerfServices = Marshal.GetDelegateForFunctionPointer<ADLX_IADLXSystem_GetPerformanceMonitoringServices>(getPerfServicesPtr);
                                         retGetPerfServices = getPerfServices(adlxSystem, ref adlxPerfServices);
                                         Log(String.Format("ADLX IADLXSystem::GetPerformanceMonitoringServices Result: {0}, PerfServicesPtr: {1}", retGetPerfServices, adlxPerfServices));

                                         if (retGetPerfServices != 0)
                                         {
                                             Log(String.Format("    ADLX GetPerformanceMonitoringServices failed with error code: {0}", retGetPerfServices));
                                         }
                                         else if (adlxPerfServices == IntPtr.Zero)
                                         {
                                             Log("    ADLX GetPerformanceMonitoringServices returned success but performance monitoring services interface pointer is null (IntPtr.Zero).");
                                         }
                                     }
                                     else
                                     {
                                         Log("ADLX IADLXSystem::GetPerformanceMonitoringServices function pointer is IntPtr.Zero");
                                     }

if (retGetGPUs == 0 && adlxGPUList != IntPtr.Zero)
                                  {
                                      IntPtr sizePtr, atPtr;
                                      if (!TryGetVtableFunction(adlxGPUList, 3, out sizePtr, out string errSize))
                                      {
                                          Log($"ADLX IADLXGPUList::Size vtable read failed: {errSize}");
                                          gpuCount = 0;
                                      }
                                      else if (!TryGetVtableFunction(adlxGPUList, 11, out atPtr, out string errAt))
                                      {
                                          Log($"ADLX IADLXGPUList::At vtable read failed: {errAt}");
                                          gpuCount = 0;
                                      }
                                      else
                                      {
                                          var getListSize = Marshal.GetDelegateForFunctionPointer<ADLX_IADLXGPUList_Size>(sizePtr);
                                          gpuCount = getListSize(adlxGPUList);
                                          Log(String.Format("ADLX IADLXGPUList::Size Count: {0}", gpuCount));
                                          Log(String.Format("Liczba znalezionych GPU przez ADLX: {0}", gpuCount));

                                           var getAt = Marshal.GetDelegateForFunctionPointer<ADLX_IADLXGPUList_At>(atPtr);
                                      for (uint i = 0; i < gpuCount; i++)
                                      {
                                          IntPtr adlxGPU = IntPtr.Zero;
                                          int retAt = getAt(adlxGPUList, i, ref adlxGPU);
                                          Log(String.Format("  ADLX IADLXGPUList::At[{0}] Result: {1}, GPUPtr: {2}", i, retAt, adlxGPU));

if (adlxGPU != IntPtr.Zero)
                                           {
                                               try
                                               {
                                                   if (retAt == 0)
                                                   {
                                                       string gpuName = "Unknown GPU";
                                                       int retName = -1;
                                                       try
                                                       {
                                                           IntPtr namePtrFunc;
                                                           if (!TryGetVtableFunction(adlxGPU, 7, out namePtrFunc, out string errName))
                                                           {
                                                               Log($"    ADLX IADLXGPU::Name vtable read failed: {errName}");
                                                           }
                                                           else if (namePtrFunc != IntPtr.Zero)
                                                           {
                                                               var getName = Marshal.GetDelegateForFunctionPointer<ADLX_IADLXGPU_Name>(namePtrFunc);
                                                               IntPtr ansiStrPtr = IntPtr.Zero;
                                                               retName = getName(adlxGPU, ref ansiStrPtr);
                                                               Log(String.Format("    ADLX IADLXGPU::Name Result: {0}, StringPtr: {1}", retName, ansiStrPtr));

                                                               if (retName == 0 && ansiStrPtr != IntPtr.Zero)
                                                               {
                                                                   gpuName = Marshal.PtrToStringAnsi(ansiStrPtr) ?? "Unknown GPU";
                                                               }
                                                           }
                                                       }
                                                      catch (Exception exName)
                                                      {
                                                          gpuName = $"Error reading name: {exName.Message}";
                                                          Log($"    ADLX IADLXGPU::Name Exception: {exName.Message}");
                                                      }
                                                      finally
                                                      {
                                                          Log($"  Karta GPU {i}: {gpuName}");
                                                      }

                                                      // 2. Sprawdź obsługę metryk, pobierz metryki i odczytaj GPUTemperature dla tej karty GPU
                                                      IntPtr adlxMetricsSupport = IntPtr.Zero;
                                                      IntPtr adlxGPUMetrics = IntPtr.Zero;
                                                      int retGetMetricsSupport = -1;
                                                      int retGetMetrics = -1;
                                                      int retIsSupported = -1;
                                                      byte isSupportedTemp = 0;

try
                                                       {
                                                           if (retGetPerfServices == 0 && adlxPerfServices != IntPtr.Zero)
                                                           {
                                                               IntPtr getSupportedGPUMetricsPtr, getCurrentGPUMetricsPtr;
                                                               if (!TryGetVtableFunction(adlxPerfServices, 21, out getSupportedGPUMetricsPtr, out string errGetSupported))
                                                               {
                                                                   Log($"    ADLX IADLXPerformanceMonitoringServices::GetSupportedGPUMetrics vtable read failed: {errGetSupported}");
                                                                   getSupportedGPUMetricsPtr = IntPtr.Zero;
                                                               }
                                                               if (!TryGetVtableFunction(adlxPerfServices, 18, out getCurrentGPUMetricsPtr, out string errGetCurrent))
                                                               {
                                                                   Log($"    ADLX IADLXPerformanceMonitoringServices::GetCurrentGPUMetrics vtable read failed: {errGetCurrent}");
                                                                   getCurrentGPUMetricsPtr = IntPtr.Zero;
                                                               }

                                                               // A. Sprawdź obsługę metryki GPUTemperature
                                                              if (getSupportedGPUMetricsPtr == IntPtr.Zero)
                                                              {
                                                                  Log("    ADLX IADLXPerformanceMonitoringServices::GetSupportedGPUMetrics function pointer is IntPtr.Zero (brak wskaźnika GetSupportedGPUMetrics)");
                                                              }
                                                              else
                                                              {
                                                                  var getSupportedGPUMetrics = Marshal.GetDelegateForFunctionPointer<ADLX_IADLXPerformanceMonitoringServices_GetSupportedGPUMetrics>(getSupportedGPUMetricsPtr);
                                                                  retGetMetricsSupport = getSupportedGPUMetrics(adlxPerfServices, adlxGPU, ref adlxMetricsSupport);
                                                                  Log(String.Format("    ADLX IADLXPerformanceMonitoringServices::GetSupportedGPUMetrics Result: {0}, MetricsSupportPtr: {1}", retGetMetricsSupport, adlxMetricsSupport));

                                                                  if (retGetMetricsSupport != 0)
                                                                  {
                                                                      Log(String.Format("    ADLX GetSupportedGPUMetrics failed with error code: {0}", retGetMetricsSupport));
                                                                  }
                                                                  else if (adlxMetricsSupport == IntPtr.Zero)
                                                                  {
                                                                      Log("    ADLX GetSupportedGPUMetrics returned success but metrics support interface pointer is null (IntPtr.Zero).");
                                                                  }
else
                                                                   {
                                                                       IntPtr isSupportedTempPtr;
                                                                       if (!TryGetVtableFunction(adlxMetricsSupport, 6, out isSupportedTempPtr, out string errIsSupported))
                                                                       {
                                                                           Log($"    ADLX IADLXGPUMetricsSupport::IsSupportedGPUTemperature vtable read failed: {errIsSupported}");
                                                                       }
                                                                       else if (isSupportedTempPtr == IntPtr.Zero)
                                                                       {
                                                                           Log("    ADLX IADLXGPUMetricsSupport::IsSupportedGPUTemperature function pointer is IntPtr.Zero (brak wskaźnika funkcji IsSupportedGPUTemperature)");
                                                                       }
                                                                       else
                                                                       {
                                                                           var isSupportedGpuTemp = Marshal.GetDelegateForFunctionPointer<ADLX_IADLXGPUMetricsSupport_IsSupportedGPUTemperature>(isSupportedTempPtr);
                                                                           retIsSupported = isSupportedGpuTemp(adlxMetricsSupport, ref isSupportedTemp);
                                                                           Log(String.Format("    ADLX IADLXGPUMetricsSupport::IsSupportedGPUTemperature Result: {0}, Supported: {1}", retIsSupported, isSupportedTemp));

                                                                           if (retIsSupported != 0)
                                                                           {
                                                                               Log(String.Format("    ADLX IsSupportedGPUTemperature failed with error code: {0}", retIsSupported));
                                                                           }
                                                                       }
                                                                   }
                                                              }

                                                              // B. Pobierz bieżące metryki i odczytaj temperaturę, jeśli obsługiwana i sprawdzona pomyślnie
                                                              if (retGetMetricsSupport == 0 && adlxMetricsSupport != IntPtr.Zero && retIsSupported == 0 && isSupportedTemp != 0 && getCurrentGPUMetricsPtr != IntPtr.Zero)
                                                              {
                                                                  var getCurrentGPUMetrics = Marshal.GetDelegateForFunctionPointer<ADLX_IADLXPerformanceMonitoringServices_GetCurrentGPUMetrics>(getCurrentGPUMetricsPtr);
                                                                  retGetMetrics = getCurrentGPUMetrics(adlxPerfServices, adlxGPU, ref adlxGPUMetrics);
                                                                  Log(String.Format("    ADLX IADLXPerformanceMonitoringServices::GetCurrentGPUMetrics Result: {0}, GPUMetricsPtr: {1}", retGetMetrics, adlxGPUMetrics));

if (retGetMetrics == 0 && adlxGPUMetrics != IntPtr.Zero)
                                                                   {
                                                                       IntPtr gpuTempPtr;
                                                                       if (!TryGetVtableFunction(adlxGPUMetrics, 7, out gpuTempPtr, out string errGpuTemp))
                                                                       {
                                                                           Log($"    ADLX IADLXGPUMetrics::GPUTemperature vtable read failed: {errGpuTemp}");
                                                                       }
                                                                       else if (gpuTempPtr != IntPtr.Zero)
                                                                       {
                                                                           var getGpuTemp = Marshal.GetDelegateForFunctionPointer<ADLX_IADLXGPUMetrics_GPUTemperature>(gpuTempPtr);
                                                                           double temperature = 0.0;
                                                                           int retTemp = getGpuTemp(adlxGPUMetrics, ref temperature);
                                                                           Log(String.Format("    ADLX IADLXGPUMetrics::GPUTemperature Result: {0}", retTemp));

                                                                           if (retTemp == 0)
                                                                           {
                                                                               Log(string.Format("    Metric: GPUTemperature\n    Semantic: Edge Temperature\n    Unit: Celsius\n    Value: {0} °C", temperature));
                                                                           }
                                                                           else
                                                                           {
                                                                               Log(string.Format("    Metric: GPUTemperature\n    Semantic: Edge Temperature\n    Unit: Celsius\n    Status: Błąd odczytu (kod: {0})", retTemp));
                                                                           }
                                                                       }
                                                                       else
                                                                       {
                                                                           Log("    ADLX IADLXGPUMetrics::GPUTemperature function pointer is IntPtr.Zero");
                                                                       }
                                                                   }
                                                                  else
                                                                  {
                                                                      Log(string.Format("    ADLX GetCurrentGPUMetrics failed or returned null (Result: {0}, Ptr: {1})", retGetMetrics, adlxGPUMetrics));
                                                                  }
                                                              }
                                                              else
                                                              {
                                                                  if (getSupportedGPUMetricsPtr == IntPtr.Zero)
                                                                  {
                                                                      Log("    Metric: GPUTemperature\n    Semantic: Edge Temperature\n    Unit: Celsius\n    Status: Brak wskaźnika GetSupportedGPUMetrics");
                                                                  }
                                                                  else if (retGetMetricsSupport != 0)
                                                                  {
                                                                      Log(string.Format("    Metric: GPUTemperature\n    Semantic: Edge Temperature\n    Unit: Celsius\n    Status: Błąd GetSupportedGPUMetrics (kod: {0})", retGetMetricsSupport));
                                                                  }
                                                                  else if (adlxMetricsSupport == IntPtr.Zero)
                                                                  {
                                                                      Log("    Metric: GPUTemperature\n    Semantic: Edge Temperature\n    Unit: Celsius\n    Status: Brak interfejsu IADLXGPUMetricsSupport");
                                                                  }
                                                                  else if (retIsSupported != 0)
                                                                  {
                                                                      Log(string.Format("    Metric: GPUTemperature\n    Semantic: Edge Temperature\n    Unit: Celsius\n    Status: Błąd IsSupportedGPUTemperature (kod: {0})", retIsSupported));
                                                                  }
                                                                  else if (isSupportedTemp == 0)
                                                                  {
                                                                      Log("    Metric: GPUTemperature\n    Semantic: Edge Temperature\n    Unit: Celsius\n    Status: Metryka nie jest obsługiwana");
                                                                  }
                                                                  else if (getCurrentGPUMetricsPtr == IntPtr.Zero)
                                                                  {
                                                                      Log("    Metric: GPUTemperature\n    Semantic: Edge Temperature\n    Unit: Celsius\n    Status: Brak wskaźnika GetCurrentGPUMetrics");
                                                                  }
                                                                  else
                                                                  {
                                                                      Log("    Metric: GPUTemperature\n    Semantic: Edge Temperature\n    Unit: Celsius\n    Status: Nieznany błąd lub niespełniony warunek odczytu");
                                                                  }
                                                              }
                                                          }
                                                          else
                                                          {
                                                              Log("    ADLX PerformanceMonitoringServices is not available (Pointer is Zero or GetPerformanceMonitoringServices failed).");
                                                          }
                                                      }
                                                      catch (Exception exMetrics)
                                                      {
                                                          Log(String.Format("    ADLX Metrics Exception: {0}", exMetrics.Message));
                                                      }
finally
                                                       {
                                                           // Zwalnianie interfejsu IADLXGPUMetricsSupport
                                                           if (adlxMetricsSupport != IntPtr.Zero)
                                                           {
                                                               if (TryReleaseAdlxInterface(adlxMetricsSupport, "IADLXGPUMetricsSupport", out int refCountSupport, out string errSupportRel))
                                                               {
                                                                   Log(String.Format("    ADLX IADLXGPUMetricsSupport Released. Remaining RefCount: {0}", refCountSupport));
                                                               }
                                                               else
                                                               {
                                                                   Log($"    ADLX IADLXGPUMetricsSupport Release failed: {errSupportRel}");
                                                               }
                                                           }

                                                           // Zwalnianie interfejsu IADLXGPUMetrics
                                                           if (adlxGPUMetrics != IntPtr.Zero)
                                                           {
                                                               if (TryReleaseAdlxInterface(adlxGPUMetrics, "IADLXGPUMetrics", out int refCountMetrics, out string errMetricsRel))
                                                               {
                                                                   Log(String.Format("    ADLX IADLXGPUMetrics Released. Remaining RefCount: {0}", refCountMetrics));
                                                               }
                                                               else
                                                               {
                                                                   Log($"    ADLX IADLXGPUMetrics Release failed: {errMetricsRel}");
                                                               }
                                                           }
                                                       }
                                                  }
                                              }
finally
                                               {
                                                   // Zwalnianie interfejsu IADLXGPU
                                                   if (adlxGPU != IntPtr.Zero)
                                                   {
                                                       if (TryReleaseAdlxInterface(adlxGPU, "IADLXGPU", out int refCountGpu, out string errGpuRel))
                                                       {
                                                           Log(String.Format("  ADLX IADLXGPU[{0}] Released. Remaining RefCount: {1}", i, refCountGpu));
                                                       }
                                                       else
                                                       {
                                                           Log($"  ADLX IADLXGPU[{i}] Release failed: {errGpuRel}");
                                                       }
                                                   }
                                               }
                                          }
                    else
                    {
                        Log("ADLXInitialize export not found.");
                    }
                }
                        catch (Exception exEnum)
                        {
                            Log(String.Format("ADLX GPU Enumeration / Metrics Exception: {0}", exEnum.Message));
                        }
finally
                            {
                                // 3. Zwalnianie IADLXGPUList oraz IADLXPerformanceMonitoringServices przed zakończeniem ADLX
                                if (adlxGPUList != IntPtr.Zero)
                                {
                                    if (TryReleaseAdlxInterface(adlxGPUList, "IADLXGPUList", out int refCountList, out string errListRel))
                                    {
                                        Log(String.Format("ADLX IADLXGPUList Released. Remaining RefCount: {0}", refCountList));
                                    }
                                    else
                                    {
                                        Log($"ADLX IADLXGPUList Release failed: {errListRel}");
                                    }
                                }

                                if (adlxPerfServices != IntPtr.Zero)
                                {
                                    if (TryReleaseAdlxInterface(adlxPerfServices, "IADLXPerformanceMonitoringServices", out int refCountPerf, out string errPerfRel))
                                    {
                                        Log(String.Format("ADLX IADLXPerformanceMonitoringServices Released. Remaining RefCount: {0}", refCountPerf));
                                    }
                                    else
                                    {
                                        Log($"ADLX IADLXPerformanceMonitoringServices Release failed: {errPerfRel}");
                                    }
                                }
                            }
                    }
                    else
                    {
                        Log("ADLXInitialize export not found.");
                    }
                }
            }
            catch (Exception ex)
            {
                Log(String.Format("ADLX Diagnostics Exception (Caught safely): {0}", ex.Message));
            }
            finally
            {
                if (initialized && terminate != null)
                {
                    try
                    {
                        int retTerm = terminate();
                        if (retTerm == 0)
                        {
                            Log(String.Format("ADLXTerminate Result: {0} [VERIFIED SUCCESS]", retTerm));
                        }
                        else
                        {
                            Log(String.Format("ADLXTerminate Result: {0} (API returned non-zero error code)", retTerm));
                        }
                    }
                    catch (Exception ex)
                    {
                        Log(String.Format("ADLXTerminate Exception: {0}", ex.Message));
                    }
                }

                bool freed = FreeLibrary(dllHandle);
                Log($"[ADLX] FreeLibrary Result: {freed}");
            }
        }

        static void RunLhmDiagnostics()
        {
            Log("=== LibreHardwareMonitor Diagnostics (GPU Only) ===");
            Computer? computer = null;
            try
            {
                computer = new Computer
                {
                    IsGpuEnabled = true,
                    IsCpuEnabled = false,
                    IsMemoryEnabled = false,
                    IsMotherboardEnabled = false,
                    IsControllerEnabled = false,
                    IsNetworkEnabled = false,
                    IsStorageEnabled = false
                };

                computer.Open();
                Log("LHM Computer opened successfully for GPU.");

                bool foundGpu = false;
                foreach (var hardware in computer.Hardware)
                {
                    if (hardware.HardwareType == HardwareType.GpuAmd ||
                        hardware.HardwareType == HardwareType.GpuNvidia ||
                        hardware.HardwareType == HardwareType.GpuIntel)
                    {
                        foundGpu = true;
                        hardware.Update();
                        Log($"  GPU Found: {hardware.Name} ({hardware.HardwareType})");

                        string selectedSensorName = "None";
                        float? selectedSensorValue = null;
                        string selectionReason = "Not found";
                        bool isVerifiedEdge = false;

                        // Collect and evaluate main hardware sensors
                        foreach (var sensor in hardware.Sensors)
                        {
                            if (sensor.SensorType == SensorType.Temperature)
                            {
                                if (string.IsNullOrEmpty(sensor.Name))
                                {
                                    Log("    Sensor: [Brak nazwy] | Pominięto czujnik z powodu braku nazwy.");
                                    continue;
                                }

                                string nameLower = sensor.Name.ToLowerInvariant();
                                bool isHotspotOrMemory = nameLower.Contains("hot spot") ||
                                                         nameLower.Contains("hotspot") ||
                                                         nameLower.Contains("memory") ||
                                                         nameLower.Contains("vram") ||
                                                         nameLower.Contains("junction");

                                Log(string.Format("    Sensor: {0} | Value: {1} °C | IgnoredAsHotspotOrMemory: {2}", sensor.Name, sensor.Value, isHotspotOrMemory));

                                    if (!sensor.Value.HasValue)
                                    {
                                        Log(string.Format("    Sensor: {0} | Pominięto czujnik z powodu braku odczytu (Value == null).", sensor.Name));
                                        continue;
                                    }

                                    if (!isHotspotOrMemory && !selectedSensorValue.HasValue)
                                    {
                                        selectedSensorName = sensor.Name + " (Candidate/Unverified)";
                                        selectedSensorValue = sensor.Value;
                                         selectionReason = $"Main hardware sensor '{sensor.Name}' was selected because it lacks excluded terms, but its Core/Edge status is unverified.";
                                        isVerifiedEdge = false;
                                    }
                            }
                        }

                        // Also evaluate subhardware sensors if main didn't yield an Edge sensor
                        foreach (var subHardware in hardware.SubHardware)
                        {
                            subHardware.Update();
                            foreach (var subSensor in subHardware.Sensors)
                            {
                                if (subSensor.SensorType == SensorType.Temperature)
                                {
                                    if (string.IsNullOrEmpty(subSensor.Name))
                                    {
                                        Log($"    SubHardware Sensor: [{subHardware.Name} - Brak nazwy] | Pominięto czujnik z powodu braku nazwy.");
                                        continue;
                                    }

                                    string nameLower = subSensor.Name.ToLowerInvariant();
                                    bool isHotspotOrMemory = nameLower.Contains("hot spot") ||
                                                             nameLower.Contains("hotspot") ||
                                                             nameLower.Contains("memory") ||
                                                             nameLower.Contains("vram") ||
                                                             nameLower.Contains("junction");

                                    Log(string.Format("    SubHardware Sensor: {0} ({1}) | Value: {2} °C | IgnoredAsHotspotOrMemory: {3}", subHardware.Name, subSensor.Name, subSensor.Value, isHotspotOrMemory));

                                    if (!subSensor.Value.HasValue)
                                    {
                                        Log(string.Format("    SubHardware Sensor: {0} ({1}) | Pominięto czujnik z powodu braku odczytu (Value == null).", subHardware.Name, subSensor.Name));
                                        continue;
                                    }

                                    if (!isHotspotOrMemory && !selectedSensorValue.HasValue)
                                    {
                                        selectedSensorName = $"{subHardware.Name} - {subSensor.Name} (Candidate/Unverified)";
                                        selectedSensorValue = subSensor.Value;
                                         selectionReason = $"SubHardware sensor '{subSensor.Name}' was selected because it lacks excluded terms, but its Core/Edge status is unverified.";
                                        isVerifiedEdge = false;
                                    }
                                }
                            }
                        }

                        if (!selectedSensorValue.HasValue)
                        {
                            // Fallback if all sensors are hotspot or unusual: do not select any arbitrary sensor
                            selectedSensorName = "Nie znaleziono porównywalnego czujnika temperatury GPU";
                            selectionReason = "No eligible temperature sensor found: sensors may have excluded names, missing names, or missing readings.";
                            isVerifiedEdge = false;
                        }

                        Log(string.Format("  Selected Temperature Sensor: {0} | Value: {1} | Reason: {2} | VerifiedEdge: {3}",
                            selectedSensorName,
                            selectedSensorValue.HasValue ? selectedSensorValue.Value.ToString() + " °C" : "N/A",
                            selectionReason,
                            isVerifiedEdge));
                    }
                }

                if (!foundGpu)
                {
                    Log("  No GPU detected via LibreHardwareMonitor.");
                }
            }
            catch (Exception ex)
            {
                Log($"LHM Diagnostics Exception: {ex.Message}");
            }
            finally
            {
                if (computer != null)
                {
                    try
                    {
                        computer.Close();
                        Log("LHM Computer closed successfully.");
                    }
                    catch (Exception ex)
                    {
                        Log($"LHM Computer Close Error: {ex.Message}");
                    }
                }
            }
        }

        static void SaveDump()
        {
            string fileName = "sensors-dump.txt";
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string targetPath = Path.Combine(baseDir, fileName);
            string locationInfo = "EXE directory";
            string finalPath = targetPath;

            logBuilder.AppendLine();
            logBuilder.AppendLine($"Report location: Saved in {locationInfo}.");

            try
            {
                File.WriteAllText(targetPath, logBuilder.ToString(), Encoding.UTF8);
                exactReportPath = targetPath;
                Console.WriteLine($"Report successfully saved: {targetPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save report next to EXE: {ex.Message}");
                try
                {
                    string tempDir = Path.GetTempPath();
                    finalPath = Path.Combine(tempDir, fileName);
                    
                    // Replace EXE directory info with %TEMP% info in the log content before writing to temp
                    string content = logBuilder.ToString().Replace("Saved in EXE directory.", "Saved in %TEMP%.");

                    File.WriteAllText(finalPath, content, Encoding.UTF8);
                    exactReportPath = finalPath;
                    Console.WriteLine($"Report successfully saved: {finalPath}");
                }
                catch (Exception ex2)
                {
                    Console.WriteLine($"Failed to save report to emergency path: {ex2.Message}");
                }
            }
        }
    }
}
