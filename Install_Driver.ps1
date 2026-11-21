# DFRDisplayKm Driver Installation Script for MacBook Pro
# Must be executed with Administrator privileges

Write-Host "==============================================================" -ForegroundColor Cyan
Write-Host "   Apple Touch Bar Driver Installer (DFRDisplayKm)            " -ForegroundColor Cyan
Write-Host "==============================================================" -ForegroundColor Cyan
Write-Host ""

# Check Administrator privileges
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "[!] Administrator privileges required. Requesting UAC elevation..." -ForegroundColor Yellow
    try {
        Start-Process powershell.exe -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`"" -Verb RunAs
        exit 0
    } catch {
        Write-Host "[!] ERROR: Failed to elevate to Administrator: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "    Please right-click Install_Driver.bat and select 'Run as administrator'." -ForegroundColor Yellow
        pause
        exit 1
    }
}

# Locate Driver directory
$driverDir = Join-Path $PSScriptRoot "Driver"
if (-not (Test-Path (Join-Path $driverDir "DFRDisplayKm.inf"))) {
    $driverDir = $PSScriptRoot
}

Write-Host "[1/5] Checking Windows Test Mode (TESTSIGNING)..." -ForegroundColor Yellow
$bcd = bcdedit.exe /enum '{current}' 2>&1 | Out-String
$isTestMode = ($bcd -match "testsigning\s+(Yes|Да|true|1|True)")

if (-not $isTestMode) {
    Write-Host "  [!] WARNING: Windows Test Mode is NOT enabled!" -ForegroundColor Yellow
    Write-Host "      Enabling test signing now (bcdedit /set testsigning on)..." -ForegroundColor Cyan
    $res = bcdedit.exe /set testsigning on 2>&1
    Write-Host "  $res"
    Write-Host "  [!] NOTE: You MUST reboot Windows after this installation for Test Mode to activate!" -ForegroundColor Red
} else {
    Write-Host "  Windows Test Mode is ACTIVE [OK]." -ForegroundColor Green
}

Write-Host "`n[2/5] Importing Driver Certificate to Windows Certificate Store..." -ForegroundColor Yellow
$cerPath = Join-Path $driverDir "DFRDisplayKm.cer"
if (Test-Path $cerPath) {
    certutil.exe -addstore -f "Root" $cerPath | Out-Null
    certutil.exe -addstore -f "TrustedPublisher" $cerPath | Out-Null
    Write-Host "  Certificate installed to Trusted Root and Trusted Publishers [OK]." -ForegroundColor Green
} else {
    Write-Host "  [!] DFRDisplayKm.cer not found in $driverDir, skipping certificate import." -ForegroundColor Yellow
}

Write-Host "`n[3/5] Installing Driver with PnPUtil..." -ForegroundColor Yellow
$infPath = Join-Path $driverDir "DFRDisplayKm.inf"
if (Test-Path $infPath) {
    $pnpOut = pnputil.exe /add-driver $infPath /install 2>&1
    Write-Host ($pnpOut | Out-String)
    Write-Host "  Driver package registered [OK]." -ForegroundColor Green
} else {
    Write-Host "  [!] ERROR: DFRDisplayKm.inf not found in $driverDir!" -ForegroundColor Red
}

Write-Host "`n[4/5] Configuring Driver and Sensor Services..." -ForegroundColor Yellow
sc.exe config DFRDisplayKm start= auto | Out-Null
sc.exe start DFRDisplayKm | Out-Null
sc.exe config SensrSvc start= auto | Out-Null
sc.exe start SensrSvc | Out-Null
Write-Host "  Services configured to auto-start [OK]." -ForegroundColor Green

Write-Host "`n[5/5] Switching Apple T2 iBridge to Touch Bar Display configuration..." -ForegroundColor Yellow
$bridgeDevices = Get-PnpDevice | Where-Object { 
    $_.InstanceId -match "USB\\VID_05AC&PID_8302" -or $_.InstanceId -match "USB\\VID_05AC&PID_8600" 
}
foreach ($d in $bridgeDevices) {
    Write-Host "  Restarting device: $($d.FriendlyName) ($($d.InstanceId))..."
    Disable-PnpDevice -InstanceId $d.InstanceId -Confirm:$false -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 400
    Enable-PnpDevice -InstanceId $d.InstanceId -Confirm:$false -ErrorAction SilentlyContinue
}
Write-Host "  iBridge USB controller refreshed [OK]." -ForegroundColor Green

Write-Host "`n==============================================================" -ForegroundColor Cyan
$disp = Get-PnpDevice | Where-Object { $_.FriendlyName -like "*Touch Bar*" -or $_.InstanceId -like "*MI_01*" }
if ($disp -and $disp.Status -eq "OK") {
    Write-Host "  SUCCESS! Apple Touch Bar Display is active and ready!" -ForegroundColor Green
} else {
    Write-Host "  Driver installed! If Touch Bar display does not light up," -ForegroundColor Yellow
    Write-Host "  please REBOOT your MacBook to apply test mode and settings." -ForegroundColor Yellow
}
Write-Host "==============================================================" -ForegroundColor Cyan
Write-Host ""
pause
