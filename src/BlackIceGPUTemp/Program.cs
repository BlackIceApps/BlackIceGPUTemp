using System;
using System.Windows.Forms;
using BlackIceGPUTemp.Tray;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("BlackIceGPUTemp.Tests")]

namespace BlackIceGPUTemp
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new BlackIceGPUTempApplicationContext());
        }
    }
}