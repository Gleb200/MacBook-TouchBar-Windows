using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Threading;

namespace TouchBarSettings
{
    /// <summary>
    /// Supervises the Avalonia display host (DFRContentHost).
    ///
    /// Besides simply keeping the process alive, it verifies that the host actually
    /// reached the Touch Bar hardware (a heartbeat file is written after the first
    /// successful frame transmission). Without this check the process could stay
    /// alive but render nothing after reboot, and the only workaround was the manual
    /// "Restart Touch Bar" tray action. The watchdog now performs that restart itself.
    /// </summary>
    public static class TouchBarEngine
    {
        private static Process _dfrProcess;
        private static Timer _watchdogTimer;
        private static bool _isShuttingDown = false;
        private static readonly object _lock = new object();

        private static DateTime _hostStartedUtc = DateTime.MinValue;
        private static DateTime _autoStartNotBeforeUtc = DateTime.MinValue;
        private static DateTime _nextStartAttemptUtc = DateTime.MinValue;
        private static int _failedStartAttempts = 0;

        private static readonly TimeSpan HeartbeatTimeout = TimeSpan.FromSeconds(8);
        private static readonly TimeSpan FirstFrameGrace = TimeSpan.FromSeconds(22);

        public static event Action<bool> OnHostStatusChanged;

        public static string HeartbeatPath { get; } = BuildHeartbeatPath();

        static TouchBarEngine()
        {
            _watchdogTimer = new Timer(WatchdogCallback, null, 2000, 3000);
        }

        private static string BuildHeartbeatPath()
        {
            try
            {
                string dir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (string.IsNullOrEmpty(dir))
                {
                    dir = Path.GetTempPath();
                }
                return Path.Combine(dir, "TouchBar", "host.heartbeat");
            }
            catch
            {
                return Path.Combine(Path.GetTempPath(), "TouchBar", "host.heartbeat");
            }
        }

        /// <summary>
        /// Defers the automatic host start after Windows logon by the configured
        /// number of seconds. Called by the Settings window when launched with --tray.
        /// </summary>
        public static void SetStartupDelay(int seconds)
        {
            seconds = Math.Max(0, Math.Min(300, seconds));
            if (seconds > 0)
            {
                _autoStartNotBeforeUtc = DateTime.UtcNow.AddSeconds(seconds);
                Log($"Autostart delay set to {seconds}s (host will start at {_autoStartNotBeforeUtc:HH:mm:ss}).");
            }
            else
            {
                _autoStartNotBeforeUtc = DateTime.MinValue;
                Log("Autostart delay disabled (host will start immediately).");
            }
        }

        private static void WatchdogCallback(object state)
        {
            if (_isShuttingDown) return;

            try
            {
                bool running = IsHostRunning();
                bool ready = running && IsHeartbeatFresh();

                try { OnHostStatusChanged?.Invoke(ready); } catch { }

                if (!running)
                {
                    if (DateTime.UtcNow >= _autoStartNotBeforeUtc && DateTime.UtcNow >= _nextStartAttemptUtc)
                    {
                        StartHost();
                    }
                    return;
                }

                if (ready)
                {
                    _failedStartAttempts = 0;
                    return;
                }

                // The process is alive but has not produced a single frame yet.
                // After the grace period, do the same clean restart that used to be
                // performed manually from the tray menu.
                DateTime started = _hostStartedUtc;
                if (started == DateTime.MinValue) started = DateTime.UtcNow;

                if (DateTime.UtcNow - started > FirstFrameGrace)
                {
                    RestartHostInternal("host is alive but never sent a frame", true);
                }
            }
            catch { }
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
                    if (_hostStartedUtc == DateTime.MinValue) _hostStartedUtc = DateTime.UtcNow;
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
                            if (_hostStartedUtc == DateTime.MinValue) _hostStartedUtc = DateTime.UtcNow;
                            return true;
                        }
                        catch { }
                    }
                }
            }
            catch { }

            return false;
        }

        public static bool IsHeartbeatFresh()
        {
            try
            {
                if (!File.Exists(HeartbeatPath)) return false;
                var age = DateTime.UtcNow - File.GetLastWriteTimeUtc(HeartbeatPath);
                return age >= TimeSpan.Zero && age <= HeartbeatTimeout;
            }
            catch
            {
                return false;
            }
        }

        private static void DeleteHeartbeat()
        {
            try
            {
                if (File.Exists(HeartbeatPath)) File.Delete(HeartbeatPath);
            }
            catch { }
        }

        public static void EnsureDriverServiceRunning()
        {
            try
            {
                var psi = new ProcessStartInfo("sc.exe", "start DFRDisplayKm")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using var p = Process.Start(psi);
                p?.WaitForExit(1500);
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
                DeleteHeartbeat();

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
                        Log("Cannot start host: HostApp_v2\\DFRContentHost.dll not found.");
                        return;
                    }

                    string fileName = File.Exists(dotnetPath) ? dotnetPath : "dotnet";

                    // On logon the Apple T2 iBridge can take much longer to expose the
                    // DFR interface than during a manual start. Tell the host to retry
                    // device discovery for longer in that case.
                    bool slowStart = _autoStartNotBeforeUtc != DateTime.MinValue;
                    try
                    {
                        long uptimeMs = Environment.TickCount64;
                        if (uptimeMs >= 0 && uptimeMs < 300000) slowStart = true;
                    }
                    catch { }

                    var psi = new ProcessStartInfo
                    {
                        FileName = fileName,
                        Arguments = "\"" + appDll + "\"",
                        WorkingDirectory = workDir,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };

                    if (slowStart)
                    {
                        psi.EnvironmentVariables["TOUCHBAR_SLOW_START"] = "1";
                        Log("Starting host with extended device-discovery window (post-boot/autostart).");
                    }

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
                    _hostStartedUtc = DateTime.UtcNow;
                    Log($"Host started (pid {_dfrProcess?.Id}).");
                }
                catch (Exception ex)
                {
                    Log("Failed to start host: " + ex.Message);
                    _failedStartAttempts++;
                    _nextStartAttemptUtc = DateTime.UtcNow.AddSeconds(Math.Min(30, 3 * _failedStartAttempts));
                }
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
                        _dfrProcess.WaitForExit(1500);
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
                _hostStartedUtc = DateTime.MinValue;
                DeleteHeartbeat();
            }
        }

        public static void Shutdown()
        {
            _isShuttingDown = true;
            _watchdogTimer?.Dispose();
            _watchdogTimer = null;
            StopHost();
        }

        /// <summary>Manual restart (used by the tray menu and Settings window).</summary>
        public static void RestartHost()
        {
            RestartHostInternal("manual restart requested", false);
        }

        private static void RestartHostInternal(string reason, bool backoff)
        {
            Log("Restarting host: " + reason);

            StopHost();
            Thread.Sleep(700);

            if (backoff)
            {
                _failedStartAttempts++;
                _nextStartAttemptUtc = DateTime.UtcNow.AddSeconds(Math.Min(30, 3 * _failedStartAttempts));
                // The watchdog will start the host again once the backoff expires.
                return;
            }

            StartHost();
        }

        private static void Log(string message)
        {
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "supervisor.log");
                if (File.Exists(logPath))
                {
                    var fi = new FileInfo(logPath);
                    if (fi.Length > 512 * 1024)
                    {
                        try { File.Delete(logPath); } catch { }
                    }
                }
                File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\r\n");
            }
            catch { }
        }
    }
}
