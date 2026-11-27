using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace BlackIceGPUTemp.Tray
{
    internal sealed class TrayMenu : IDisposable
    {
        private const int MenuExitId = 1001;
        private const uint TPM_RIGHTALIGN = 0x0008;
        private const uint TPM_BOTTOMALIGN = 0x0020;
        private const uint TPM_RIGHTBUTTON = 0x0002;
        private const uint TPM_RETURNCMD = 0x0100;
        private const uint WM_NULL = 0x0000;

        [DllImport("user32.dll")]
        private static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll")]
        private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, uint uIDNewItem, string lpNewItem);

        [DllImport("user32.dll")]
        private static extern int TrackPopupMenuEx(IntPtr hMenu, uint uFlags, int x, int y, IntPtr hwnd, IntPtr lptpm);

        [DllImport("user32.dll")]
        private static extern bool DestroyMenu(IntPtr hMenu);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private readonly IntPtr _dispatcherHandle;

        public TrayMenu(IntPtr dispatcherHandle)
        {
            _dispatcherHandle = dispatcherHandle;
        }

        public bool ShowContextMenu(Point cursorPos)
        {
            IntPtr hMenu = CreatePopupMenu();
            if (hMenu == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                if (!AppendMenu(hMenu, 0x00000000, (uint)MenuExitId, "Exit"))
                {
                    return false;
                }

                uint flags = TPM_RIGHTALIGN | TPM_BOTTOMALIGN | TPM_RIGHTBUTTON | TPM_RETURNCMD;
                int x = cursorPos.X;
                int y = cursorPos.Y;

                SetForegroundWindow(_dispatcherHandle);
                int result = TrackPopupMenuEx(hMenu, flags, x, y, _dispatcherHandle, IntPtr.Zero);
                PostMessage(_dispatcherHandle, WM_NULL, IntPtr.Zero, IntPtr.Zero);

                return result == MenuExitId;
            }
            finally
            {
                DestroyMenu(hMenu);
            }
        }

        public void Dispose()
        {
        }
    }
}