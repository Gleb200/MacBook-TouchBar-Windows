using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DFRContentHost.Interop
{
    static class NativeMethods
    {
        [DllImport("user32.dll")]
        public static extern void LockWorkStation();

        [DllImport("powrprof.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS lpSystemPowerStatus);

        [StructLayout(LayoutKind.Sequential)]
        public struct SYSTEM_POWER_STATUS
        {
            public byte ACLineStatus;
            public byte BatteryFlag;
            public byte BatteryLifePercent;
            public byte SystemStatusFlag;
            public uint BatteryLifeTime;
            public uint BatteryFullLifeTime;
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool ExitWindowsEx(uint uFlags, uint dwReason);

        public static void SleepComputer()
        {
            try
            {
                SetSuspendState(false, true, false);
            }
            catch { }
        }

        public static void RestartComputer()
        {
            try
            {
                string shutdownExe = System.IO.Path.Combine(Environment.SystemDirectory, "shutdown.exe");
                if (!System.IO.File.Exists(shutdownExe))
                {
                    shutdownExe = "shutdown.exe";
                }

                var psi = new ProcessStartInfo(shutdownExe, "/r /f /t 0")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                Process.Start(psi);
            }
            catch
            {
                try
                {
                    // EWX_REBOOT (0x00000002) | EWX_FORCE (0x00000004)
                    ExitWindowsEx(0x00000002 | 0x00000004, 0);
                }
                catch { }
            }
        }

        public static void ShutdownComputer()
        {
            try
            {
                string shutdownExe = System.IO.Path.Combine(Environment.SystemDirectory, "shutdown.exe");
                if (!System.IO.File.Exists(shutdownExe))
                {
                    shutdownExe = "shutdown.exe";
                }

                var psi = new ProcessStartInfo(shutdownExe, "/s /f /t 0")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                Process.Start(psi);
            }
            catch
            {
                try
                {
                    // EWX_SHUTDOWN (0x00000001) | EWX_FORCE (0x00000004)
                    ExitWindowsEx(0x00000001 | 0x00000004, 0);
                }
                catch { }
            }
        }
    }
}
