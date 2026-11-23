using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;

namespace TouchBarLauncher
{
    static class Program
    {
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int RegisterWindowMessage(string lpString);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private static readonly IntPtr HWND_BROADCAST = new IntPtr(0xffff);

        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string rootDir = Directory.Exists(Path.Combine(baseDir, "dotnet"))
                    ? baseDir
                    : (Directory.GetParent(baseDir) != null ? Directory.GetParent(baseDir).FullName : baseDir);

                // If running elevated, grant Users FullControl to application folder
                try
                {
                    var id = WindowsIdentity.GetCurrent();
                    var p = new WindowsPrincipal(id);
                    if (p.IsInRole(WindowsBuiltInRole.Administrator))
                    {
                        var dInfo = new DirectoryInfo(rootDir);
                        var dSecurity = dInfo.GetAccessControl();
                        var sid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
                        var rule = new FileSystemAccessRule(
                            sid,
                            FileSystemRights.FullControl,
                            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                            PropagationFlags.None,
                            AccessControlType.Allow);
                        dSecurity.AddAccessRule(rule);
                        dInfo.SetAccessControl(dSecurity);
                    }
                }
                catch { }

                int showSettingsMsg = RegisterWindowMessage("TouchBar_ShowSettings");

                bool isTrayMode = false;
                if (args != null)
                {
                    foreach (var a in args)
                    {
                        if (string.Equals(a, "--tray", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(a, "-tray", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(a, "/tray", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(a, "--autostart", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(a, "-minimized", StringComparison.OrdinalIgnoreCase))
                        {
                            isTrayMode = true;
                            break;
                        }
                    }
                }

                // If system booted less than 3 minutes ago, treat as logon autostart
                if (!isTrayMode && Environment.TickCount > 0 && Environment.TickCount < 180000)
                {
                    isTrayMode = true;
                }

                // Check if TouchBarSettings is already running
                bool isRunning = false;
                try
                {
                    using (var existing = Mutex.OpenExisting("Global\\TouchBarSettings_SingleInstance"))
                    {
                        isRunning = true;
                    }
                }
                catch { }

                if (isRunning)
                {
                    if (!isTrayMode)
                    {
                        PostMessage(HWND_BROADCAST, showSettingsMsg, IntPtr.Zero, IntPtr.Zero);
                    }
                    return;
                }

                string dotnetDir = Path.Combine(rootDir, "dotnet");
                string dotnetPath = Path.Combine(dotnetDir, "dotnet.exe");
                string settingsDll = Path.Combine(rootDir, "TouchBarSettings.dll");
                string hostDll = Path.Combine(rootDir, "HostApp_v2", "DFRContentHost.dll");

                string appDll = File.Exists(settingsDll) ? settingsDll : hostDll;
                string workDir = File.Exists(settingsDll) ? rootDir : Path.Combine(rootDir, "HostApp_v2");

                if (!File.Exists(dotnetPath) || !File.Exists(appDll))
                {
                    return;
                }

                string extraArgs = args != null && args.Length > 0 ? " " + string.Join(" ", args) : "";
                if (isTrayMode && !extraArgs.Contains("--tray"))
                {
                    extraArgs = " --tray" + extraArgs;
                }

                var psi = new ProcessStartInfo
                {
                    FileName = dotnetPath,
                    Arguments = "\"" + appDll + "\"" + extraArgs,
                    WorkingDirectory = workDir,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = false
                };

                if (Directory.Exists(dotnetDir))
                {
                    psi.EnvironmentVariables["DOTNET_ROOT"] = dotnetDir;
                    string curPath = Environment.GetEnvironmentVariable("PATH") ?? "";
                    if (!curPath.Contains(dotnetDir))
                    {
                        psi.EnvironmentVariables["PATH"] = dotnetDir + ";" + curPath;
                    }
                }

                Process.Start(psi);
            }
            catch { }
        }
    }
}
