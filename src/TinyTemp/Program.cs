using System;
using System.Windows.Forms;
using TinyTemp.Tray;

namespace TinyTemp
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TinyTempApplicationContext());
        }
    }
}