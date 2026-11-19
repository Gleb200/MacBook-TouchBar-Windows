@echo off
title DFRDisplayKm Driver Installer for MacBook Pro
chcp 65001 >nul
cd /d "%~dp0"

echo ==============================================================
echo   Apple Touch Bar Driver Installer (DFRDisplayKm)
echo ==============================================================
echo.

:: Check for Administrator privileges
net session >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo [!] Administrator privileges required. Requesting UAC elevation...
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process cmd.exe -ArgumentList '/c \"\"%~f0\"\"' -Verb RunAs"
    exit /b
)

:: Locate Driver directory
set "DRIVER_DIR=%~dp0Driver"
if not exist "%DRIVER_DIR%\DFRDisplayKm.inf" (
    set "DRIVER_DIR=%~dp0"
)

echo [1/5] Checking Windows Test Mode (TESTSIGNING)...
bcdedit | findstr /i "testsigning" | findstr /i "Yes" >nul
if %ERRORLEVEL% neq 0 (
    echo [!] WARNING: Windows Test Mode is NOT enabled!
    echo     Enabling test signing now (bcdedit -set TESTSIGNING ON)...
    bcdedit -set TESTSIGNING ON >nul 2>&1
    echo [!] Test mode enabled! NOTE: You MUST reboot Windows for this to take effect!
    echo.
) else (
    echo     Windows Test Mode is ACTIVE [OK].
)

echo [2/5] Importing Driver Certificate to Windows Certificate Store...
if exist "%DRIVER_DIR%\DFRDisplayKm.cer" (
    certutil -addstore -f "Root" "%DRIVER_DIR%\DFRDisplayKm.cer" >nul
    certutil -addstore -f "TrustedPublisher" "%DRIVER_DIR%\DFRDisplayKm.cer" >nul
    echo     Certificate installed to Trusted Root and Trusted Publishers [OK].
) else (
    echo     [!] DFRDisplayKm.cer not found in %DRIVER_DIR%, skipping certificate import.
)

echo [3/5] Installing Driver with PnPUtil...
pnputil /add-driver "%DRIVER_DIR%\DFRDisplayKm.inf" /install
echo.

echo [4/5] Configuring Driver and Sensor Services...
sc.exe config DFRDisplayKm start= auto >nul 2>&1
sc.exe start DFRDisplayKm >nul 2>&1
sc.exe config SensrSvc start= auto >nul 2>&1
sc.exe start SensrSvc >nul 2>&1
echo     Services configured to auto-start [OK].

echo [5/5] Switching Apple T2 iBridge to Touch Bar Display configuration...
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$devs = Get-PnpDevice | Where-Object { $_.InstanceId -like 'USB\VID_05AC&PID_8302*' -or $_.InstanceId -like 'USB\VID_05AC&PID_8600*' };" ^
  "foreach ($d in $devs) {" ^
  "  Disable-PnpDevice -InstanceId $d.InstanceId -Confirm:$false -ErrorAction SilentlyContinue;" ^
  "  Start-Sleep -Milliseconds 400;" ^
  "  Enable-PnpDevice -InstanceId $d.InstanceId -Confirm:$false -ErrorAction SilentlyContinue;" ^
  "}"
echo     iBridge USB controller refreshed [OK].

echo.
echo ==============================================================
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$disp = Get-PnpDevice | Where-Object { $_.FriendlyName -like '*Touch Bar*' -or $_.InstanceId -like '*MI_01*' };" ^
  "if ($disp -and $disp.Status -eq 'OK') {" ^
  "  Write-Host '  SUCCESS! Apple Touch Bar Display is active and ready!' -ForegroundColor Green;" ^
  "} else {" ^
  "  Write-Host '  Driver installed! If Touch Bar display does not light up,' -ForegroundColor Yellow;" ^
  "  Write-Host '  please REBOOT your MacBook to apply test mode and settings.' -ForegroundColor Yellow;" ^
  "}"
echo ==============================================================
echo.
pause
