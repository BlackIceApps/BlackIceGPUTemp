using System;
using System.Threading;
using System.Windows.Forms;
using BlackIceGPUTemp.Logging;
using BlackIceGPUTemp.Monitoring;
using BlackIceGPUTemp.Monitoring.Abstractions;
using BlackIceGPUTemp.Monitoring.Providers.Amd;
using BlackIceGPUTemp.Tray;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("BlackIceGPUTemp.Tests")]

namespace BlackIceGPUTemp
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            bool createdNew;
            using (var mutex = new Mutex(true, @"Local\BlackIceGPUTemp.SingleInstance", out createdNew))
            {
                if (!createdNew)
                {
                    return;
                }

                try
                {
                    SetupExceptionHandlers();

                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);

                    var providerFactory = new AmdGpuProviderFactory();
                    var monitoringSessionFactory = new GpuMonitoringSessionFactory(providerFactory);
                    var context = new BlackIceGPUTempApplicationContext(monitoringSessionFactory);

                    Application.Run(context);
                }
                finally
                {
                    mutex.ReleaseMutex();
                }
            }
        }

        private static void SetupExceptionHandlers()
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                try
                {
                    if (args.ExceptionObject is Exception ex)
                    {
                        Logger.LogError("Unhandled exception", ex);
                    }
                    else
                    {
                        Logger.LogError($"Unhandled exception: {args.ExceptionObject}");
                    }
                }
                catch
                {
                }
            };

            Application.ThreadException += (sender, args) =>
            {
                try
                {
                    Logger.LogError("Unhandled thread exception", args.Exception);
                }
                catch
                {
                }
            };
        }
    }
}