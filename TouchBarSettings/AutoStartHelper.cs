using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using Microsoft.Win32;

namespace TouchBarSettings
{
    public static class AutoStartHelper
    {
        private const string TaskName = "TouchBarAutoStart";
        private const string RegKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RegValueName = "TouchBar";

        public static bool IsAutoStartEnabled()
        {
            if (IsTaskRegistered()) return true;

            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegKeyPath, false);
                if (key != null && key.GetValue(RegValueName) != null)
                {
                    return true;
                }
            }
            catch { }

            return false;
        }

        public static bool IsTaskRegistered()
        {
            try
            {
                var psi = new ProcessStartInfo("schtasks", $"/query /tn \"{TaskName}\"")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using var p = Process.Start(psi);
                p?.WaitForExit(2000);
                return p != null && p.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Registers/removes the logon autostart entry.
        /// The start delay is intentionally NOT baked into the scheduled task anymore:
        /// TouchBarSettings applies it itself (AutoStartDelaySeconds in config.json), so
        /// the user can change it live from the GUI without re-registering the task.
        /// </summary>
        public static void SetAutoStart(bool enable)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string rootDir = Directory.Exists(Path.Combine(baseDir, "dotnet"))
                ? baseDir
                : (Directory.GetParent(baseDir) != null ? Directory.GetParent(baseDir).FullName : baseDir);

            string exePath = Path.Combine(rootDir, "TouchBar.exe");
            if (!File.Exists(exePath))
            {
                exePath = Path.Combine(baseDir, "TouchBar.exe");
            }

            if (enable)
            {
                bool taskCreated = false;

                // 1. Preferred: Task Scheduler (elevated, silent, no UAC prompt).
                try
                {
                    string user = WindowsIdentity.GetCurrent()?.Name ?? Environment.UserName;
                    string psScript = $@"
$action = New-ScheduledTaskAction -Execute '{exePath}' -Argument '--tray' -WorkingDirectory '{rootDir}'
$trigger = New-ScheduledTaskTrigger -AtLogOn
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit (New-TimeSpan -Days 0) -Priority 4
try {{
    $principal = New-ScheduledTaskPrincipal -UserId '{user}' -LogonType Interactive -RunLevel Highest
    Register-ScheduledTask -TaskName '{TaskName}' -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Force | Out-Null
}} catch {{
    Register-ScheduledTask -TaskName '{TaskName}' -Action $action -Trigger $trigger -Settings $settings -Force | Out-Null
}}
";
                    var psi = new ProcessStartInfo("powershell", $"-NoProfile -ExecutionPolicy Bypass -Command \"{psScript.Replace("\r", "").Replace("\n", "; ")}\"")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    using var p = Process.Start(psi);
                    p?.WaitForExit(8000);
                    taskCreated = p != null && p.ExitCode == 0;
                }
                catch { }

                // Verify registration (PowerShell may return 0 even when Register-ScheduledTask failed).
                if (!taskCreated) taskCreated = IsTaskRegistered();

                if (taskCreated)
                {
                    // Do NOT also add the HKCU Run entry: two logon entries race each other
                    // (the non-elevated one can win the single-instance mutex before the
                    // elevated task starts), which was one of the causes of the black
                    // Touch Bar after reboot. Clean up any stale fallback entry instead.
                    RemoveRunValue();
                }
                else
                {
                    // 2. Fallback: per-user Run key (works even when Task Scheduler is blocked).
                    try
                    {
                        using var key = Registry.CurrentUser.OpenSubKey(RegKeyPath, true);
                        key?.SetValue(RegValueName, $"\"{exePath}\" --tray");
                    }
                    catch { }
                }
            }
            else
            {
                try
                {
                    var psi = new ProcessStartInfo("schtasks", $"/delete /tn \"{TaskName}\" /f")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    using var p = Process.Start(psi);
                    p?.WaitForExit(3000);
                }
                catch { }

                RemoveRunValue();
            }
        }

        private static void RemoveRunValue()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegKeyPath, true);
                key?.DeleteValue(RegValueName, false);
            }
            catch { }
        }
    }
}
