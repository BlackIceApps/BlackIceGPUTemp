using System;
using System.Windows.Forms;
using BlackIceGPUTemp.Tray;

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