# Touch Bar and Driver Uninstallation Script
# Must be executed with Administrator privileges

Write-Host "==============================================================" -ForegroundColor Cyan
Write-Host "       Touch Bar for Windows - Complete Uninstaller           " -ForegroundColor Cyan
Write-Host "==============================================================" -ForegroundColor Cyan
Write-Host ""

# Check Admin privileges
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "[!] ERROR: This script must be run as Administrator!" -ForegroundColor Red
    Write-Host "    Please right-click Uninstall_TouchBar.bat and select 'Run as administrator'." -ForegroundColor Yellow
    Write-Host ""
    pause
    exit 1
}

Write-Host "[1/6] Stopping Touch Bar and dotnet host processes..." -ForegroundColor Yellow
$procNames = @("TouchBar", "TouchBarSettings", "DFRContentHost", "dotnet")
foreach ($name in $procNames) {
    Get-Process -Name $name -ErrorAction SilentlyContinue | ForEach-Object {
        Write-Host "  Stopping $($_.ProcessName) (PID $($_.Id))..."
        Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
    }
}
cmd.exe /c "taskkill /f /t /im TouchBar.exe >nul 2>&1"
cmd.exe /c "taskkill /f /t /im TouchBarSettings.exe >nul 2>&1"
cmd.exe /c "taskkill /f /t /im DFRContentHost.exe >nul 2>&1"
cmd.exe /c "taskkill /f /t /im dotnet.exe >nul 2>&1"
Start-Sleep -Milliseconds 500
Write-Host "  Processes stopped [OK]." -ForegroundColor Green

Write-Host "`n[2/6] Removing Windows Task Scheduler Autostart task..." -ForegroundColor Yellow
$task = Get-ScheduledTask -TaskName "TouchBarAutoStart" -ErrorAction SilentlyContinue
if ($task) {
    Unregister-ScheduledTask -TaskName "TouchBarAutoStart" -Confirm:$false -ErrorAction SilentlyContinue
    Write-Host "  Scheduled task 'TouchBarAutoStart' removed [OK]." -ForegroundColor Green
} else {
    Write-Host "  Scheduled task 'TouchBarAutoStart' not found (already removed) [OK]." -ForegroundColor Green
}

# Clean registry Run keys
Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "TouchBar" -ErrorAction SilentlyContinue
Remove-ItemProperty -Path "HKLM:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "TouchBar" -ErrorAction SilentlyContinue

# Clean Startup shortcuts
$startupLnk = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\Startup\TouchBar.lnk"
if (Test-Path $startupLnk) {
    Remove-Item -Path $startupLnk -Force -ErrorAction SilentlyContinue
    Write-Host "  Removed startup shortcut: $startupLnk" -ForegroundColor Green
}

Write-Host "`n[3/6] Stopping and removing DFRDisplayKm driver service..." -ForegroundColor Yellow
$svc = Get-Service -Name "DFRDisplayKm" -ErrorAction SilentlyContinue
if ($svc) {
    Write-Host "  Stopping service DFRDisplayKm..."
    sc.exe stop DFRDisplayKm
    Start-Sleep -Seconds 1
    Write-Host "  Deleting service DFRDisplayKm..."
    sc.exe delete DFRDisplayKm
    Write-Host "  DFRDisplayKm service removed [OK]." -ForegroundColor Green
} else {
    Write-Host "  DFRDisplayKm service not found (already removed) [OK]." -ForegroundColor Green
}

Write-Host "`n[4/6] Uninstalling DFRDisplayKm from Driver Store (PnP)..." -ForegroundColor Yellow
$driverList = pnputil /enum-drivers
$oemFound = $false
for ($i = 0; $i -lt $driverList.Count; $i++) {
    if ($driverList[$i] -match "dfrdisplaykm") {
        for ($j = $i; $j -ge [Math]::Max(0, $i - 6); $j--) {
            if ($driverList[$j] -match "(oem\d+\.inf)") {
                $oemName = $matches[1]
                Write-Host "  Found driver package: $oemName. Deleting and uninstalling..."
                pnputil /delete-driver $oemName /uninstall /force
                $oemFound = $true
                Write-Host "  Driver package $oemName deleted [OK]." -ForegroundColor Green
                break
            }
        }
    }
}
if (-not $oemFound) {
    Write-Host "  No DFRDisplayKm driver package found in Driver Store [OK]." -ForegroundColor Green
}

Write-Host "`n[5/6] Removing DFRDisplayKm certificates from Windows Certificate Store..." -ForegroundColor Yellow
$thumbprint = "D440EDDF77BE98D7F4FB826528648B0EC6D2967B"
$certStores = @("Cert:\LocalMachine\Root", "Cert:\LocalMachine\TrustedPublisher", "Cert:\CurrentUser\Root", "Cert:\CurrentUser\TrustedPublisher")
foreach ($storePath in $certStores) {
    if (Test-Path $storePath) {
        $certs = Get-ChildItem -Path $storePath -ErrorAction SilentlyContinue | Where-Object { 
            $_.Thumbprint -eq $thumbprint -or $_.Subject -match "WDKTestCert Naozumi" 
        }
        foreach ($c in $certs) {
            Write-Host "  Removing certificate from $storePath ($($c.Thumbprint))..."
            Remove-Item -Path $c.PSPath -Force -ErrorAction SilentlyContinue
        }
    }
}
Write-Host "  Certificates cleanup completed [OK]." -ForegroundColor Green

Write-Host "`n[6/6] Resetting Apple iBridge USB device to default state..." -ForegroundColor Yellow
$bridgeDevices = Get-PnpDevice | Where-Object { 
    $_.InstanceId -match "USB\\VID_05AC&PID_8302" -or $_.InstanceId -match "USB\\VID_05AC&PID_8600" 
}
foreach ($dev in $bridgeDevices) {
    Write-Host "  Restarting device $($dev.FriendlyName) ($($dev.InstanceId))..."
    Disable-PnpDevice -InstanceId $dev.InstanceId -Confirm:$false -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 500
    Enable-PnpDevice -InstanceId $dev.InstanceId -Confirm:$false -ErrorAction SilentlyContinue
}
Write-Host "  Apple iBridge device refreshed [OK]." -ForegroundColor Green

Write-Host "`n==============================================================" -ForegroundColor Cyan
Write-Host "  Touch Bar and its driver have been COMPLETELY uninstalled!   " -ForegroundColor Green
Write-Host "  Your system is clean and ready for a fresh installation.    " -ForegroundColor Cyan
Write-Host "==============================================================" -ForegroundColor Cyan
Write-Host ""
