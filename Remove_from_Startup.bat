@echo off
title Touch Bar Autostart Removal
cd /d "%~dp0"

echo ==============================================================
echo  Removing Touch Bar from Windows Startup...
echo ==============================================================
echo.

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "Unregister-ScheduledTask -TaskName 'TouchBarAutoStart' -Confirm:$false -ErrorAction SilentlyContinue;" ^
  "Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'TouchBar' -ErrorAction SilentlyContinue;" ^
  "$lnk = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Startup\TouchBar.lnk';" ^
  "if (Test-Path $lnk) { Remove-Item $lnk -Force -ErrorAction SilentlyContinue };"

echo.
echo ==============================================================
echo  Touch Bar successfully removed from Startup.
echo ==============================================================
echo.
pause
