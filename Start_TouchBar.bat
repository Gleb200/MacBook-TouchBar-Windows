@echo off
title DFR Touch Bar Host
cd /d "%~dp0"

set "DOTNET_ROOT=%~dp0dotnet"
set "PATH=%DOTNET_ROOT%;%PATH%"

if exist "%~dp0TouchBar.exe" (
    start "" "%~dp0TouchBar.exe"
) else (
    start "" "%~dp0dotnet\dotnet.exe" "%~dp0HostApp_v2\DFRContentHost.dll"
)
