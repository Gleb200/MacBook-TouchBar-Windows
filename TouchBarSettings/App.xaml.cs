using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace TouchBarSettings
{
    public partial class App : Application
    {
        private static Mutex _appMutex;
        private static readonly IntPtr HWND_BROADCAST = new IntPtr(0xffff);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int RegisterWindowMessage(string lpString);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        protected override void OnStartup(StartupEventArgs e)
        {
            int showSettingsMsg = RegisterWindowMessage("TouchBar_ShowSettings");

            bool createdNew;
            _appMutex = new Mutex(true, "Global\\TouchBarSettings_SingleInstance", out createdNew);

            if (!createdNew)
            {
                PostMessage(HWND_BROADCAST, showSettingsMsg, IntPtr.Zero, IntPtr.Zero);
                Shutdown();
                return;
            }

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _appMutex?.Dispose();
            _appMutex = null;
            base.OnExit(e);
        }
    }
}
