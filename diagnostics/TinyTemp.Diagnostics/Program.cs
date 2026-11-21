using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace TinyTemp.Diagnostics
{
    class Program
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, uint dwFlags);

        const uint LOAD_LIBRARY_SEARCH_SYSTEM32 = 0x00000800;

        static void Main(string[] args)
        {
            Console.WriteLine("=== TinyTemp Diagnostics Tool ===");
            Console.WriteLine($"OS Version: {Environment.OSVersion}");
            Console.WriteLine($"Is 64-bit Process: {Environment.Is64BitProcess}");
            
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
            Console.WriteLine($"Is Running as Administrator: {isAdmin}");
            Console.WriteLine();

            // Check DLL presence and versions in System32
            CheckDll("atiadlxx.dll");
            CheckDll("amdadlx64.dll");

            Console.WriteLine();
            Console.WriteLine("Press Enter to exit...");
            Console.ReadLine();
        }

        static void CheckDll(string dllName)
        {
            string system32Path = Path.Combine(Environment.SystemDirectory, dllName);
            bool exists = File.Exists(system32Path);
            Console.WriteLine($"[{dllName}] Path: {system32Path} | Exists: {exists}");

            if (exists)
            {
                try
                {
                    var vi = FileVersionInfo.GetVersionInfo(system32Path);
                    Console.WriteLine($"[{dllName}] Version: {vi.FileVersion} (Product: {vi.ProductVersion})");

                    IntPtr handle = LoadLibraryEx(system32Path, IntPtr.Zero, LOAD_LIBRARY_SEARCH_SYSTEM32);
                    Console.WriteLine($"[{dllName}] LoadLibraryEx (SYSTEM32) Handle: {handle} (Error: {Marshal.GetLastWin32Error()})");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{dllName}] Error inspecting/loading: {ex.Message}");
                }
            }
        }
    }
}
