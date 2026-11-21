@echo off
title Touch Bar Autostart Setup
chcp 65001 >nul
cd /d "%~dp0"

echo ==============================================================
echo  Configuring Touch Bar Autostart on Windows Logon...
echo ==============================================================
echo.

:: Check for Administrator privileges
net session >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo [!] Administrator privileges required. Requesting UAC elevation...
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process cmd.exe -ArgumentList '/c', 'call', '\"%~f0\"' -Verb RunAs"
    exit /b
)

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$exe = Join-Path '%~dp0' 'TouchBar.exe';" ^
  "if (-not (Test-Path $exe)) { $exe = Join-Path '%~dp0' 'HostApp_v2\DFRContentHost.exe' };" ^
  "$workDir = '%~dp0';" ^
  "$action = New-ScheduledTaskAction -Execute $exe -WorkingDirectory $workDir;" ^
  "$trigger = New-ScheduledTaskTrigger -AtLogOn;" ^
  "$loggedUser = (Get-CimInstance Win32_ComputerSystem).UserName;" ^
  "if (-not $loggedUser) { $loggedUser = \"$env:USERDOMAIN\$env:USERNAME\" };" ^
  "$principal = New-ScheduledTaskPrincipal -UserId $loggedUser -LogonType Interactive -RunLevel Highest;" ^
  "$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit (New-TimeSpan -Days 0);" ^
  "Unregister-ScheduledTask -TaskName 'TouchBarAutoStart' -Confirm:$false -ErrorAction SilentlyContinue;" ^
  "Register-ScheduledTask -TaskName 'TouchBarAutoStart' -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Force | Out-Null;" ^
  "Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'TouchBar' -ErrorAction SilentlyContinue;" ^
  "$lnk = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Startup\TouchBar.lnk';" ^
  "if (Test-Path $lnk) { Remove-Item $lnk -Force -ErrorAction SilentlyContinue };"

echo.
echo ==============================================================
echo  Success! Scheduled Task 'TouchBarAutoStart' registered:
echo  - Automatic launch at logon
echo  - Highest privileges without UAC prompt
echo  - Enabled on battery (DisallowStartIfOnBatteries=false)
echo ==============================================================
echo.
pause
