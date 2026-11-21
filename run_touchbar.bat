@echo off
set "DOTNET_ROOT=%~dp0dotnet"
set "PATH=%DOTNET_ROOT%;%PATH%"
cd /d "%~dp0HostApp_v2"
start "" "%~dp0dotnet\dotnet.exe" "%~dp0HostApp_v2\DFRContentHost.dll"
