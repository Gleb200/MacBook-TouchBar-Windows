@echo off
title Touch Bar
chcp 65001 >nul
cd /d "%~dp0"

set "DOTNET_ROOT=%~dp0dotnet"
set "PATH=%DOTNET_ROOT%;%PATH%"

if exist "%~dp0TouchBar.exe" (
    start "" "%~dp0TouchBar.exe"
    exit /b
)

if exist "%~dp0dotnet\dotnet.exe" (
    start "" "%~dp0dotnet\dotnet.exe" "%~dp0HostApp_v2\DFRContentHost.dll"
    exit /b
)

echo [!] Error: Neither TouchBar.exe nor dotnet\dotnet.exe was found in:
echo     %~dp0
pause
