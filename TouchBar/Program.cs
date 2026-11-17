using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;

namespace TouchBarLauncher
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string rootDir = Directory.Exists(Path.Combine(baseDir, "dotnet"))
                    ? baseDir
                    : (Directory.GetParent(baseDir) != null ? Directory.GetParent(baseDir).FullName : baseDir);

                string dotnetDir = Path.Combine(rootDir, "dotnet");
                string dotnetPath = Path.Combine(dotnetDir, "dotnet.exe");
                string appDll = Path.Combine(rootDir, "HostApp_v2", "DFRContentHost.dll");
                string workDir = Path.Combine(rootDir, "HostApp_v2");

                if (!File.Exists(dotnetPath) || !File.Exists(appDll))
                {
                    return;
                }

                bool isAdmin = false;
                try
                {
                    using (var identity = WindowsIdentity.GetCurrent())
                    {
                        var principal = new WindowsPrincipal(identity);
                        isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
                    }
                }
                catch { }

                var psi = new ProcessStartInfo
                {
                    FileName = dotnetPath,
                    Arguments = "\"" + appDll + "\"",
                    WorkingDirectory = workDir,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                if (isAdmin)
                {
                    psi.UseShellExecute = false;
                    psi.CreateNoWindow = true;
                    psi.EnvironmentVariables["DOTNET_ROOT"] = dotnetDir;
                    string curPath = Environment.GetEnvironmentVariable("PATH") ?? "";
                    if (!curPath.Contains(dotnetDir))
                    {
                        psi.EnvironmentVariables["PATH"] = dotnetDir + ";" + curPath;
                    }
                }
                else
                {
                    psi.UseShellExecute = true;
                    psi.Verb = "runas";
                }

                Process.Start(psi);
            }
            catch { }
        }
    }
}
