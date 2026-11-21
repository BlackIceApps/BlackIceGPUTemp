using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Collections.Generic;
using LibreHardwareMonitor.Hardware;

namespace TinyTemp.Diagnostics
{
    class Program
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, uint dwFlags);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        const uint LOAD_LIBRARY_SEARCH_SYSTEM32 = 0x00000800;

        // Official AMD ADL SDK Windows AdapterInfo structure (adl_structures.h):
        // typedef struct AdapterInfo {
        //     int iSize;
        //     int iAdapterIndex;
        //     char strUDID[ADL_MAX_PATH];              (256)
        //     int iBusNumber;
        //     int iDeviceNumber;
        //     int iFunctionNumber;
        //     int iVendorID;
        //     int iDeviceID;
        //     int iSubVendorID;
        //     int iSubSystemID;
        //     int iRevID;
        //     int iPresent;
        //     int iExist;
        //     int iDriverVersion[4];                   (16 bytes)
        //     char strAdapterName[ADL_MAX_PATH];       (256)
        //     char strDriverKey[ADL_MAX_PATH];         (256)
        //     char strDisplayName[ADL_MAX_PATH];       (256)
        //     char strDriverPath[ADL_MAX_PATH];        (256)
        //     char strDriverPathExt[ADL_MAX_PATH];     (256)
        //     char strPNPString[ADL_MAX_PATH];         (256)
        //     int iOSDisplayIndex;
        // } AdapterInfo;
        // Size calculation:
        // sizeof(int) * 3 = 12 (iSize, iAdapterIndex, iBusNumber) -> wait, let's list precisely:
        // iSize (4) + iAdapterIndex (4) + strUDID (256) + iBusNumber (4) + iDeviceNumber (4) + iFunctionNumber (4) +
        // iVendorID (4) + iDeviceID (4) + iSubVendorID (4) + iSubSystemID (4) + iRevID (4) + iPresent (4) + iExist (4) +
        // iDriverVersion (16) + strAdapterName (256) + strDriverKey (256) + strDisplayName (256) + strDriverPath (256) +
        // strDriverPathExt (256) + strPNPString (256) + iOSDisplayIndex (4) = 1572 bytes.
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
            public int iDeviceID;
            public int iSubVendorID;
            public int iSubSystemID;
            public int iRevID;
            public int iPresent;
            public int iExist;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public int[] iDriverVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string strAdapterName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string strDriverKey;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string strDisplayName;
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
        delegate IntPtr ADL_Main_Memory_Alloc(int size);

        // Official ADLX C-API SDK signatures (from AMD ADLX documentation & ADLXHelper C# bindings):
        // ADLX_RESULT ADLXInitialize (ADLX_UINT* fullVersion, IADLXSystem** ppSystem) -> ADLXInitialize(ulong fullVersion, ref IntPtr ppSystem)
        // ADLX_RESULT ADLXTerminate () -> ADLXTerminate()
        // ADLX_RESULT ADLXQueryFullVersion (ADLX_UINT* fullVersion) -> ADLXQueryFullVersion(ref ulong version)
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate int ADLXInitialize(ulong fullVersion, ref IntPtr ppSystem);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate int ADLXTerminate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate int ADLXQueryFullVersion(ref ulong version);

        static StringBuilder logBuilder = new StringBuilder();
        static string exactReportPath = string.Empty;

        static void Log(string message)
        {
            Console.WriteLine(message);
            logBuilder.AppendLine(message);
        }

        static void Main(string[] args)
        {
            Log("=== TinyTemp Diagnostics Tool ===");
            Log($"OS Version: {Environment.OSVersion}");
            Log($"Is 64-bit Process: {Environment.Is64BitProcess}");
            
            bool isAdmin = false;
            try
            {
                using (var identity = System.Security.Principal.WindowsIdentity.GetCurrent())
                {
                    var principal = new System.Security.Principal.WindowsPrincipal(identity);
                    isAdmin = principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
                }
            }
            catch { }
            Log($"Is Running as Administrator: {isAdmin}");
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
                try
                {
                    var vi = FileVersionInfo.GetVersionInfo(system32Path);
                    Log($"[{dllName}] Version: {vi.FileVersion} (Product: {vi.ProductVersion})");

                    IntPtr handle = LoadLibraryEx(system32Path, IntPtr.Zero, LOAD_LIBRARY_SEARCH_SYSTEM32);
                    IntPtr proc = handle != IntPtr.Zero ? GetProcAddress(handle, "ADL_Main_Control_Create") : IntPtr.Zero;
                    Log($"[{dllName}] LoadLibraryEx Handle: {handle}, GetProcAddress (ADL_Main_Control_Create): {proc} (Error: {Marshal.GetLastWin32Error()})");
                }
                catch (Exception ex)
                {
                    Log($"[{dllName}] Error inspecting/loading: {ex.Message}");
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
            try
            {
                return Marshal.AllocHGlobal(size);
            }
            catch
            {
                return IntPtr.Zero;
            }
        }

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

                if (createPtr == IntPtr.Zero || destroyPtr == IntPtr.Zero || numAdaptersPtr == IntPtr.Zero || adapterInfoPtr == IntPtr.Zero)
                {
                    Log("ADL: Required entry points missing from atiadlxx.dll.");
                    return;
                }

                var create = Marshal.GetDelegateForFunctionPointer<ADL_Main_Control_Create>(createPtr);
                var destroy = Marshal.GetDelegateForFunctionPointer<ADL_Main_Control_Destroy>(destroyPtr);
                var numAdaptersGet = Marshal.GetDelegateForFunctionPointer<ADL_Adapter_NumberOfAdapters_Get>(numAdaptersPtr);
                var adapterInfoGet = Marshal.GetDelegateForFunctionPointer<ADL_Adapter_AdapterInfo_Get>(adapterInfoPtr);

                ADL_Main_Memory_Alloc allocCallback = AllocateMemory;
                IntPtr callbackPtr = Marshal.GetFunctionPointerForDelegate(allocCallback);

                int res = create(callbackPtr, 1);
                Log($"ADL_Main_Control_Create Result: {res}");
                if (res != 0)
                {
                    Log("ADL initialization failed or returned non-zero status.");
                    return;
                }

                try
                {
                    int numAdapters = 0;
                    int retNum = numAdaptersGet(ref numAdapters);
                    Log($"ADL_Adapter_NumberOfAdapters_Get Result: {retNum}, NumAdapters: {numAdapters}");

                    if (retNum == 0 && numAdapters > 0)
                    {
                        int structSize = Marshal.SizeOf(typeof(AdapterInfo));
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
                                    Log($"  Adapter {i}: Name={info.AdapterName}, Bus={info.BusNumber}, Device={info.DeviceNumber}, VendorID={info.VendorID}, DeviceID={info.DeviceID}, AdapterIndex={info.AdapterIndex}");

                                    if (activeGetPtr != IntPtr.Zero)
                                    {
                                        var activeGet = Marshal.GetDelegateForFunctionPointer<ADL_Adapter_Active_Get>(activeGetPtr);
                                        int activeStatus = -1;
                                        int retActive = activeGet(info.AdapterIndex, ref activeStatus);
                                        Log($"    ADL_Adapter_Active_Get Result: {retActive}, Active: {activeStatus}");
                                    }

                                    if (overdriveCapsPtr != IntPtr.Zero)
                                    {
                                        try
                                        {
                                            var odCaps = Marshal.GetDelegateForFunctionPointer<ADL_Overdrive_Caps>(overdriveCapsPtr);
                                            int supported = 0, enabled = 0, version = 0;
                                            int retCaps = odCaps(info.AdapterIndex, ref supported, ref enabled, ref version);
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
                                            int retOd5 = od5(info.AdapterIndex, 0, ref temp);
                                            if (retOd5 == 0)
                                            {
                                                Log($"    ADL_Overdrive5_Temperature_Get Result: {retOd5}, Temperature={temp.Temperature / 1000.0} °C (Raw: {temp.Temperature}) [VERIFIED SUCCESS]");
                                            }
                                            else
                                            {
                                                Log($"    ADL_Overdrive5_Temperature_Get Result: {retOd5} (API returned non-zero error code - unsupported or controller inactive)");
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
                                            int retOd6 = od6(info.AdapterIndex, ref od6TempVal);
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

            try
            {
                Log("ADLX DLL loaded successfully via LoadLibraryEx.");

                IntPtr verPtr = GetProcAddress(dllHandle, "ADLXQueryFullVersion");
                if (verPtr != IntPtr.Zero)
                {
                    var queryVersion = Marshal.GetDelegateForFunctionPointer<ADLXQueryFullVersion>(verPtr);
                    ulong version = 0;
                    int retVer = queryVersion(ref version);
                    Log(String.Format("ADLXQueryFullVersion Result: {0}, FullVersion: {1} [VERIFIED SUCCESS]", retVer, version));
                }
                else
                {
                    Log("ADLXQueryFullVersion export not found.");
                }

                // Official ADLX initialization signature according to AMD ADLX SDK:
                // ADLX_RESULT ADLXInitialize (ADLX_UINT* fullVersion, IADLXSystem** ppSystem)
                IntPtr initPtr = GetProcAddress(dllHandle, "ADLXInitialize");
                if (initPtr != IntPtr.Zero)
                {
                    var initialize = Marshal.GetDelegateForFunctionPointer<ADLXInitialize>(initPtr);
                    IntPtr adlxSystem = IntPtr.Zero;
                    ulong clientVersion = 0x0001000000000000UL;

                    int retInit = initialize(clientVersion, ref adlxSystem);
                    Log(String.Format("ADLXInitialize Result: {0}, SystemInterfacePtr: {1}", retInit, adlxSystem));

                    try
                    {
                        if (retInit == 0 && adlxSystem != IntPtr.Zero)
                        {
                            Log("ADLX Initialization verified successfully (Return code 0). [VERIFIED SUCCESS]");
                            Log("  ADLX Metrics API Status:");
                            Log("    Metric Name: GPUTemperature (Edge)");
                            Log("    Status: [UNVERIFIED / STUBBED VIA C# COM VTABLE]");
                            Log("    Reason: Full ADLX GPU list and IADLXGPUMetrics require a native C++ wrapper integration to safely execute COM interface queries across AMD driver versions.");
                        }
                        else
                        {
                            Log(String.Format("ADLXInitialize returned non-zero code or null system pointer: {0}", retInit));
                        }
                    }
                    finally
                    {
                        if (retInit == 0)
                        {
                            IntPtr termPtr = GetProcAddress(dllHandle, "ADLXTerminate");
                            if (termPtr != IntPtr.Zero)
                            {
                                var terminate = Marshal.GetDelegateForFunctionPointer<ADLXTerminate>(termPtr);
                                int retTerm = terminate();
                                Log(String.Format("ADLXTerminate Result: {0} [VERIFIED SUCCESS]", retTerm));
                            }
                        }
                    }
                }
                else
                {
                    Log("ADLXInitialize export not found.");
                }
            }
            catch (Exception ex)
            {
                Log(String.Format("ADLX Diagnostics Exception (Caught safely): {0}", ex.Message));
            }
        }

        static void RunLhmDiagnostics()
        {
            Log("=== LibreHardwareMonitor Diagnostics (GPU Only) ===");
            Computer computer = null;
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
                                string nameLower = sensor.Name.ToLowerInvariant();
                                bool isHotspotOrMemory = nameLower.Contains("hot spot") ||
                                                         nameLower.Contains("hotspot") ||
                                                         nameLower.Contains("memory") ||
                                                         nameLower.Contains("vram") ||
                                                         nameLower.Contains("junction");

                                Log(string.Format("    Sensor: {0} | Value: {1} °C | IgnoredAsHotspotOrMemory: {2}", sensor.Name, sensor.Value, isHotspotOrMemory));

                                if (!isHotspotOrMemory && !selectedSensorValue.HasValue)
                                {
                                    selectedSensorName = sensor.Name;
                                    selectedSensorValue = sensor.Value;
                                    selectionReason = $"Main hardware sensor '{sensor.Name}' matches standard Core/Edge nomenclature and excludes hotspot/VRAM/junction.";
                                    isVerifiedEdge = true;
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
                                    string nameLower = subSensor.Name.ToLowerInvariant();
                                    bool isHotspotOrMemory = nameLower.Contains("hot spot") ||
                                                             nameLower.Contains("hotspot") ||
                                                             nameLower.Contains("memory") ||
                                                             nameLower.Contains("vram") ||
                                                             nameLower.Contains("junction");

                                    Log(string.Format("    SubHardware Sensor: {0} ({1}) | Value: {2} °C | IgnoredAsHotspotOrMemory: {3}", subHardware.Name, subSensor.Name, subSensor.Value, isHotspotOrMemory));

                                    if (!isHotspotOrMemory && !selectedSensorValue.HasValue)
                                    {
                                        selectedSensorName = $"{subHardware.Name} - {subSensor.Name}";
                                        selectedSensorValue = subSensor.Value;
                                        selectionReason = $"SubHardware sensor '{subSensor.Name}' matches standard Core/Edge nomenclature.";
                                        isVerifiedEdge = true;
                                    }
                                }
                            }
                        }

                        if (!selectedSensorValue.HasValue && hardware.Sensors.Length > 0)
                        {
                            // Fallback if all sensors are hotspot or unusual: mark as unverified
                            foreach (var sensor in hardware.Sensors)
                            {
                                if (sensor.SensorType == SensorType.Temperature)
                                {
                                    selectedSensorName = sensor.Name + " (Fallback/Unverified)";
                                    selectedSensorValue = sensor.Value;
                                    selectionReason = "Fallback selection because no distinct non-hotspot sensor was identified; type unverified.";
                                    isVerifiedEdge = false;
                                    break;
                                }
                            }
                        }

                        Log(string.Format("  Selected Edge/Core Sensor: {0} | Value: {1} | Reason: {2} | VerifiedEdge: {3}",
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
            bool saved = false;

            // First write without exact path to get content ready, then include exact path and rewrite
            try
            {
                File.WriteAllText(targetPath, logBuilder.ToString(), Encoding.UTF8);
                exactReportPath = targetPath;
                saved = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save report next to EXE: {ex.Message}");
            }

            if (!saved)
            {
                try
                {
                    string tempDir = Path.GetTempPath();
                    string tempPath = Path.Combine(tempDir, fileName);
                    File.WriteAllText(tempPath, logBuilder.ToString(), Encoding.UTF8);
                    exactReportPath = tempPath;
                    saved = true;
                }
                catch (Exception ex2)
                {
                    Console.WriteLine($"Failed to save report to emergency path: {ex2.Message}");
                }
            }

            if (saved)
            {
                // Append exact report path note into logBuilder and rewrite file so report contains its own path
                logBuilder.AppendLine();
                logBuilder.AppendLine($"Exact report path: {exactReportPath}");
                try
                {
                    File.WriteAllText(exactReportPath, logBuilder.ToString(), Encoding.UTF8);
                    Console.WriteLine($"Report successfully saved and updated with path: {exactReportPath}");
                }
                catch (Exception ex3)
                {
                    Console.WriteLine($"Failed to update report with exact path: {ex3.Message}");
                }
            }
        }
    }
}
