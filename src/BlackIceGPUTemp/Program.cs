using System;
using System.Windows.Forms;
using BlackIceGPUTemp.Logging;
using BlackIceGPUTemp.Tray;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("BlackIceGPUTemp.Tests")]

namespace BlackIceGPUTemp
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Logger.Initialize();
            SetupExceptionHandlers();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new BlackIceGPUTempApplicationContext());
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