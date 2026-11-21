@echo off
title Touch Bar Complete Uninstaller
chcp 65001 >nul
cd /d "%~dp0"

:: Check for Administrator privileges
net session >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo [!] Administrator privileges required. Requesting UAC elevation...
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process cmd.exe -ArgumentList '/c \"\"%~f0\"\"' -Verb RunAs"
    exit /b
)

:: Terminate all Touch Bar processes and dotnet runtimes immediately
taskkill /f /t /im TouchBar.exe >nul 2>&1
taskkill /f /t /im TouchBarSettings.exe >nul 2>&1
taskkill /f /t /im DFRContentHost.exe >nul 2>&1
taskkill /f /t /im dotnet.exe >nul 2>&1

:: Run the uninstallation PowerShell script
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Uninstall_TouchBar.ps1"
pause
