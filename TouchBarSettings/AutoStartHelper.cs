using System;
using System.Diagnostics;
using System.IO;

namespace TouchBarSettings
{
    public static class AutoStartHelper
    {
        private const string TaskName = "TouchBarAutoStart";

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
                return p != null && p.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        public static void SetAutoStart(bool enable)
        {
            try
            {
                if (enable)
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

                    string psScript = $@"
$action = New-ScheduledTaskAction -Execute '{exePath}' -WorkingDirectory '{rootDir}'
$trigger = New-ScheduledTaskTrigger -AtLogOn
$principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -LogonType Interactive -RunLevel Highest
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit (New-TimeSpan -Days 0)
Register-ScheduledTask -TaskName '{TaskName}' -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Force
";
                    var psi = new ProcessStartInfo("powershell", $"-NoProfile -ExecutionPolicy Bypass -Command \"{psScript.Replace("\r", "").Replace("\n", "; ")}\"")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    using var p = Process.Start(psi);
                    p?.WaitForExit(4000);
                }
                else
                {
                    var psi = new ProcessStartInfo("schtasks", $"/delete /tn \"{TaskName}\" /f")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    using var p = Process.Start(psi);
                    p?.WaitForExit(2000);
                }
            }
            catch { }
        }
    }
}
