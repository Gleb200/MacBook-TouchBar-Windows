@echo off
title DFRDisplayKm Driver Installer for MacBook Pro
chcp 65001 >nul
cd /d "%~dp0"

:: Check for Administrator privileges and elevate cleanly
net session >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo [!] Administrator privileges required. Requesting UAC elevation...
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process cmd.exe -ArgumentList '/c', 'call', '\"%~f0\"' -Verb RunAs"
    exit /b
)

:: Run the PowerShell installer script
if exist "%~dp0Install_Driver.ps1" (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install_Driver.ps1"
    goto :end
)
if exist "%~dp0Driver\Install_Driver.ps1" (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Driver\Install_Driver.ps1"
    goto :end
)

:: Fallback if ps1 is missing
set "DRIVER_DIR=%~dp0Driver"
if not exist "%DRIVER_DIR%\DFRDisplayKm.inf" set "DRIVER_DIR=%~dp0"

echo [1/4] Checking Windows Test Mode...
bcdedit /set testsigning on >nul 2>&1

echo [2/4] Importing Driver Certificate...
if exist "%DRIVER_DIR%\DFRDisplayKm.cer" (
    certutil -addstore -f "Root" "%DRIVER_DIR%\DFRDisplayKm.cer" >nul
    certutil -addstore -f "TrustedPublisher" "%DRIVER_DIR%\DFRDisplayKm.cer" >nul
)

echo [3/4] Installing Driver with PnPUtil...
pnputil /add-driver "%DRIVER_DIR%\DFRDisplayKm.inf" /install

echo [4/4] Starting services...
sc config DFRDisplayKm start= auto >nul 2>&1
sc start DFRDisplayKm >nul 2>&1
sc config SensrSvc start= auto >nul 2>&1
sc start SensrSvc >nul 2>&1

echo.
echo Driver installation finished!
pause

:end
