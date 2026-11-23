using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Threading;

namespace TouchBarSettings
{
    public static class TouchBarEngine
    {
        private static Process _dfrProcess;
        private static Timer _watchdogTimer;
        private static bool _isShuttingDown = false;
        private static readonly object _lock = new object();

        public static event Action<bool> OnHostStatusChanged;

        static TouchBarEngine()
        {
            _watchdogTimer = new Timer(WatchdogCallback, null, 3000, 3000);
        }

        private static void WatchdogCallback(object state)
        {
            if (_isShuttingDown) return;

            bool running = IsHostRunning();
            OnHostStatusChanged?.Invoke(running);

            if (!running && !_isShuttingDown)
            {
                StartHost();
            }
        }

        public static bool IsHostRunning()
        {
            try
            {
                if (_dfrProcess != null && !_dfrProcess.HasExited)
                {
                    return true;
                }
            }
            catch { }

            try
            {
                int myPid = Process.GetCurrentProcess().Id;

                var dfrProcs = Process.GetProcessesByName("DFRContentHost");
                if (dfrProcs.Length > 0)
                {
                    _dfrProcess = dfrProcs[0];
                    return true;
                }

                using var searcher = new ManagementObjectSearcher(
                    "SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name = 'dotnet.exe'");
                foreach (var obj in searcher.Get())
                {
                    int pid = Convert.ToInt32(obj["ProcessId"]);
                    if (pid == myPid) continue;

                    string cmd = obj["CommandLine"]?.ToString() ?? "";
                    if (cmd.IndexOf("DFRContentHost", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        try
                        {
                            _dfrProcess = Process.GetProcessById(pid);
                            return true;
                        }
                        catch { }
                    }
                }
            }
            catch { }

            return false;
        }

        public static void EnsureDriverServiceRunning()
        {
            try
            {
                var psi = new ProcessStartInfo("sc.exe", "start DFRDisplayKm")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                using var p = Process.Start(psi);
                p?.WaitForExit(1000);
            }
            catch { }
        }

        public static void StartHost()
        {
            lock (_lock)
            {
                if (_isShuttingDown) return;
                if (IsHostRunning()) return;

                EnsureDriverServiceRunning();

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

                    if (!File.Exists(appDll))
                    {
                        return;
                    }

                    string fileName = File.Exists(dotnetPath) ? dotnetPath : "dotnet";

                    var psi = new ProcessStartInfo
                    {
                        FileName = fileName,
                        Arguments = "\"" + appDll + "\"",
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

                    _dfrProcess = Process.Start(psi);
                }
                catch { }
            }
        }

        public static void StopHost()
        {
            lock (_lock)
            {
                try
                {
                    if (_dfrProcess != null && !_dfrProcess.HasExited)
                    {
                        _dfrProcess.Kill();
                        _dfrProcess.WaitForExit(1000);
                    }
                }
                catch { }

                try
                {
                    int myPid = Process.GetCurrentProcess().Id;
                    using var searcher = new ManagementObjectSearcher(
                        "SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name = 'dotnet.exe'");
                    foreach (var obj in searcher.Get())
                    {
                        int pid = Convert.ToInt32(obj["ProcessId"]);
                        if (pid == myPid) continue;

                        string cmd = obj["CommandLine"]?.ToString() ?? "";
                        if (cmd.IndexOf("DFRContentHost", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            try { Process.GetProcessById(pid)?.Kill(); } catch { }
                        }
                    }

                    foreach (var p in Process.GetProcessesByName("DFRContentHost"))
                    {
                        try { p.Kill(); } catch { }
                    }
                }
                catch { }

                _dfrProcess = null;
            }
        }

        public static void Shutdown()
        {
            _isShuttingDown = true;
            _watchdogTimer?.Dispose();
            _watchdogTimer = null;
            StopHost();
        }

        public static void RestartHost()
        {
            StopHost();
            Thread.Sleep(500);
            StartHost();
        }
    }
}
