@echo off
title Touch Bar Settings
cd /d "%~dp0"

if exist "%~dp0TouchBarSettings.exe" (
    start "" "%~dp0TouchBarSettings.exe"
) else if exist "%~dp0HostApp_v2\TouchBarSettings.exe" (
    start "" "%~dp0HostApp_v2\TouchBarSettings.exe"
) else (
    echo TouchBarSettings.exe not found. Run Restart_TouchBar.bat first.
    pause
)
