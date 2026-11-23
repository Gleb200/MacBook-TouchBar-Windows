using System;
using System.Diagnostics;
using System.IO;
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
                p?.WaitForExit(1500);
                if (p != null && p.ExitCode == 0) return true;
            }
            catch { }

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
                // 1. Task Scheduler with 4-second logon delay
                try
                {
                    string psScript = $@"
$action = New-ScheduledTaskAction -Execute '{exePath}' -Argument '--tray' -WorkingDirectory '{rootDir}'
$trigger = New-ScheduledTaskTrigger -AtLogOn
$trigger.Delay = 'PT4S'
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit (New-TimeSpan -Days 0) -Priority 4
try {{
    $principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -LogonType Interactive -RunLevel Highest
    Register-ScheduledTask -TaskName '{TaskName}' -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Force
}} catch {{
    Register-ScheduledTask -TaskName '{TaskName}' -Action $action -Trigger $trigger -Settings $settings -Force
}}
";
                    var psi = new ProcessStartInfo("powershell", $"-NoProfile -ExecutionPolicy Bypass -Command \"{psScript.Replace("\r", "").Replace("\n", "; ")}\"")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    using var p = Process.Start(psi);
                    p?.WaitForExit(4000);
                }
                catch { }

                // 2. Also ensure User Registry Run key as backup
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(RegKeyPath, true);
                    key?.SetValue(RegValueName, $"\"{exePath}\" --tray");
                }
                catch { }
            }
            else
            {
                // 1. Delete scheduled task
                try
                {
                    var psi = new ProcessStartInfo("schtasks", $"/delete /tn \"{TaskName}\" /f")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    using var p = Process.Start(psi);
                    p?.WaitForExit(2000);
                }
                catch { }

                // 2. Delete registry run key
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(RegKeyPath, true);
                    key?.DeleteValue(RegValueName, false);
                }
                catch { }
            }
        }
    }
}
