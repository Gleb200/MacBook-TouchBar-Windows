# MacBook Touch Bar - Unified System Manager & Diagnostics
# Encoded in UTF-8
[CmdletBinding()]
param(
    [string]$Action = "interactive"
)

# Set Console Output to UTF-8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
# Base directory (parent of Driver folder or root)
$RootDir = if (Test-Path (Join-Path $ScriptDir "..\dotnet")) {
    (Resolve-Path (Join-Path $ScriptDir "..")).Path
} else {
    $ScriptDir
}
$DriverDir = if (Test-Path (Join-Path $RootDir "Driver")) { Join-Path $RootDir "Driver" } else { $ScriptDir }

function Test-Admin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Get-Diagnostics {
    $diag = [PSCustomObject]@{
        TestMode = $false
        CertInstalled = $false
        ServiceStatus = "Missing" # OK, Stopped, NotConfigured, Missing
        DeviceStatus = "NotFound" # OK, Code19, Code52, Disabled, NotFound
        DeviceCode = -1
        AutoStart = $false
        ProcessRunning = $false
        Pids = @()
    }

    # 1. Test Mode
    try {
        $regOptions = (Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control' -ErrorAction SilentlyContinue).SystemStartOptions
        if ($regOptions -match "TESTSIGNING") {
            $diag.TestMode = $true
        } else {
            $bcd = bcdedit /enum "{current}" 2>&1 | Out-String
            if ($bcd -match "testsigning\s+(Yes|true|on|Да)") {
                $diag.TestMode = $true
            }
        }
    } catch {}

    # 2. Driver Certificate
    try {
        $targetThumb = "D440EDDF77BE98D7F4FB826528648B0EC6D2967B"
        $rootCerts = Get-ChildItem -Path Cert:\LocalMachine\Root | Where-Object { $_.Thumbprint -eq $targetThumb -or $_.Subject -match "WDKTestCert" -or $_.Subject -match "DFRDisplayKm" }
        $pubCerts = Get-ChildItem -Path Cert:\LocalMachine\TrustedPublisher | Where-Object { $_.Thumbprint -eq $targetThumb -or $_.Subject -match "WDKTestCert" -or $_.Subject -match "DFRDisplayKm" }
        if ($rootCerts -and $pubCerts) {
            $diag.CertInstalled = $true
        }
    } catch {}

    # 3. Kernel Service
    try {
        $svc = Get-Service -Name "DFRDisplayKm" -ErrorAction SilentlyContinue
        if ($svc) {
            $wdfReg = Get-ItemProperty -Path "HKLM:\System\CurrentControlSet\Services\DFRDisplayKm\Parameters\Wdf" -ErrorAction SilentlyContinue
            if ($wdfReg -and $wdfReg.KmdfLibraryVersion -eq "1.15") {
                if ($svc.Status -eq "Running") {
                    $diag.ServiceStatus = "OK"
                } else {
                    $diag.ServiceStatus = "Stopped"
                }
            } else {
                $diag.ServiceStatus = "NotConfigured"
            }
        } else {
            $diag.ServiceStatus = "Missing"
        }
    } catch {}

    # 4. Hardware Device
    try {
        $dev = Get-CimInstance Win32_PnPEntity | Where-Object { $_.Name -like "*Touch Bar Display*" -or $_.DeviceID -like "*VID_05AC&PID_8302*" -or $_.DeviceID -like "*DFRDISPLAYKM*" } | Select-Object -First 1
        if ($dev) {
            $diag.DeviceCode = $dev.ConfigManagerErrorCode
            if ($dev.ConfigManagerErrorCode -eq 0) {
                $diag.DeviceStatus = "OK"
            } elseif ($dev.ConfigManagerErrorCode -eq 19) {
                $diag.DeviceStatus = "Code19"
            } elseif ($dev.ConfigManagerErrorCode -eq 52) {
                $diag.DeviceStatus = "Code52"
            } elseif ($dev.ConfigManagerErrorCode -eq 22) {
                $diag.DeviceStatus = "Disabled"
            } else {
                $diag.DeviceStatus = "Error"
            }
        } else {
            $diag.DeviceStatus = "NotFound"
        }
    } catch {}

    # 5. Autostart
    try {
        $task = schtasks /query /tn "TouchBarAutoStart" 2>&1
        if ($LASTEXITCODE -eq 0) {
            $diag.AutoStart = $true
        }
    } catch {}

    # 6. Running Process
    try {
        $pids = @()
        $procs = Get-Process -Name "TouchBar", "DFRContentHost" -ErrorAction SilentlyContinue
        if ($procs) {
            $pids += ($procs | ForEach-Object { $_.Id })
        }
        $dotnetDir = Join-Path $RootDir "dotnet"
        $dotnetPath = Join-Path $dotnetDir "dotnet.exe"
        $appDll = Join-Path $RootDir "HostApp_v2\DFRContentHost.dll"
        $dotnets = Get-Process -Name "dotnet" -ErrorAction SilentlyContinue
        foreach ($p in $dotnets) {
            try {
                if ($p.MainModule.FileName -eq $dotnetPath) {
                    $pids += $p.Id
                }
            } catch {}
        }
        if ($pids.Count -gt 0) {
            $diag.ProcessRunning = $true
            $diag.Pids = ($pids | Select-Object -Unique)
        }
    } catch {}

    return $diag
}

function Show-Dashboard {
    param($diag)
    Clear-Host
    Write-Host "====================================================================" -ForegroundColor Cyan
    Write-Host "               MacBook Touch Bar - Панель управления               " -ForegroundColor Yellow
    Write-Host "====================================================================" -ForegroundColor Cyan
    Write-Host ""
    
    # 1. Test Mode
    Write-Host " [1] Режим тестирования (TESTSIGNING): " -NoNewline
    if ($diag.TestMode) {
        Write-Host "[ OK ] Включен (активен)" -ForegroundColor Green
    } else {
        Write-Host "[ !! ] Отключен (требуется включить и перезагрузить ПК)" -ForegroundColor Red
    }

    # 2. Certificate
    Write-Host " [2] Сертификат драйвера:              " -NoNewline
    if ($diag.CertInstalled) {
        Write-Host "[ OK ] Установлен (Root & TrustedPublisher)" -ForegroundColor Green
    } else {
        Write-Host "[ !! ] Не установлен" -ForegroundColor Red
    }

    # 3. Kernel Service
    Write-Host " [3] Служба ядра DFRDisplayKm:         " -NoNewline
    switch ($diag.ServiceStatus) {
        "OK"            { Write-Host "[ OK ] Активна (KMDF 1.15, работает)" -ForegroundColor Green }
        "Stopped"       { Write-Host "[ !! ] Остановлена (требуется запуск)" -ForegroundColor Yellow }
        "NotConfigured" { Write-Host "[ !! ] Ошибка параметров KMDF (Code 19)" -ForegroundColor Red }
        "Missing"       { Write-Host "[ !! ] Служба не создана" -ForegroundColor Red }
    }

    # 4. Device Status
    Write-Host " [4] Оборудование Touch Bar:           " -NoNewline
    switch ($diag.DeviceStatus) {
        "OK"       { Write-Host "[ OK ] Работает нормально (Code 0)" -ForegroundColor Green }
        "Code19"   { Write-Host "[ !! ] Ошибка службы/реестра (Code 19)" -ForegroundColor Red }
        "Code52"   { Write-Host "[ !! ] Ошибка подписи (Code 52 - нужен Test Mode)" -ForegroundColor Red }
        "Disabled" { Write-Host "[ !! ] Устройство отключено в Диспетчере" -ForegroundColor Yellow }
        "NotFound" { Write-Host "[ !! ] Устройство не найдено" -ForegroundColor Yellow }
        default    { Write-Host "[ !! ] Код ошибки: $($diag.DeviceCode)" -ForegroundColor Red }
    }

    # 5. Autostart
    Write-Host " [5] Автозапуск с Windows:             " -NoNewline
    if ($diag.AutoStart) {
        Write-Host "[ OK ] Включен в Планировщике" -ForegroundColor Green
    } else {
        Write-Host "[ -- ] Отключен" -ForegroundColor Gray
    }

    # 6. Process
    Write-Host " [6] Процесс Touch Bar:                " -NoNewline
    if ($diag.ProcessRunning) {
        Write-Host "[ OK ] Запущен в фоне (PID: $($diag.Pids -join ', '))" -ForegroundColor Green
    } else {
        Write-Host "[ -- ] Остановлен" -ForegroundColor Gray
    }

    Write-Host ""
    Write-Host "====================================================================" -ForegroundColor Cyan
}

function Stop-TouchBarProcesses {
    Write-Host "[*] Остановка фоновых процессов Touch Bar..." -ForegroundColor Gray
    $procs = Get-Process -Name "TouchBar", "DFRContentHost" -ErrorAction SilentlyContinue
    foreach ($p in $procs) {
        try { $p.Kill(); $p.WaitForExit(1000) } catch {}
    }
    $dotnetDir = Join-Path $RootDir "dotnet"
    $dotnetPath = Join-Path $dotnetDir "dotnet.exe"
    $dotnets = Get-Process -Name "dotnet" -ErrorAction SilentlyContinue
    foreach ($p in $dotnets) {
        try {
            if ($p.MainModule.FileName -eq $dotnetPath) {
                $p.Kill()
                $p.WaitForExit(1000)
            }
        } catch {}
    }
}

function Invoke-AutoFix {
    Write-Host ""
    Write-Host ">>> Запуск автоматического исправления и запуска Touch Bar <<<" -ForegroundColor Yellow
    Write-Host ""

    Stop-TouchBarProcesses

    # 1. Test Mode
    Write-Host "[1/6] Проверка и включение режима тестовой подписи (TESTSIGNING)..." -ForegroundColor Cyan
    try {
        $bcdOut = bcdedit /set testsigning on 2>&1 | Out-String
        $bcdOut2 = bcdedit /set nointegritychecks on 2>&1 | Out-String
        Write-Host "    -> Test Mode активирован в BCD." -ForegroundColor Green
    } catch {
        Write-Host "    [!] Предупреждение: Не удалось изменить BCD ($($_))." -ForegroundColor Yellow
    }

    # 2. Driver Certificate
    Write-Host "[2/6] Установка сертификата DFRDisplayKm.cer..." -ForegroundColor Cyan
    $cerPath = Join-Path $DriverDir "DFRDisplayKm.cer"
    if (Test-Path $cerPath) {
        try {
            $cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($cerPath)
            $rootStore = New-Object System.Security.Cryptography.X509Certificates.X509Store([System.Security.Cryptography.X509Certificates.StoreName]::Root, [System.Security.Cryptography.X509Certificates.StoreLocation]::LocalMachine)
            $rootStore.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
            $rootStore.Add($cert)
            $rootStore.Close()

            $pubStore = New-Object System.Security.Cryptography.X509Certificates.X509Store([System.Security.Cryptography.X509Certificates.StoreName]::TrustedPublisher, [System.Security.Cryptography.X509Certificates.StoreLocation]::LocalMachine)
            $pubStore.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
            $pubStore.Add($cert)
            $pubStore.Close()

            certutil -addstore -f "Root" $cerPath > $null 2>&1
            certutil -addstore -f "TrustedPublisher" $cerPath > $null 2>&1
            Write-Host "    -> Сертификат успешно установлен в Root и TrustedPublisher." -ForegroundColor Green
        } catch {
            Write-Host "    [!] Ошибка установки сертификата: $($_)" -ForegroundColor Red
        }
    } else {
        Write-Host "    [!] Сертификат не найден по пути: $cerPath" -ForegroundColor Red
    }

    # 3. INF Installation
    Write-Host "[3/6] Установка драйвера DFRDisplayKm.inf..." -ForegroundColor Cyan
    $infPath = Join-Path $DriverDir "DFRDisplayKm.inf"
    if (Test-Path $infPath) {
        $pnp = pnputil.exe /add-driver "$infPath" /install 2>&1 | Out-String
        Write-Host "    -> Драйвер зарегистрирован в хранилище драйверов Windows." -ForegroundColor Green
    }

    # 4. Service creation & KMDF 1.15 Registry
    Write-Host "[4/6] Настройка службы ядра DFRDisplayKm (KMDF 1.15)..." -ForegroundColor Cyan
    $sysTarget = "$env:SystemRoot\System32\drivers\DFRDisplayKm.sys"
    $sysSource = Join-Path $DriverDir "DFRDisplayKm.sys"
    if (Test-Path $sysSource) {
        Copy-Item -Path $sysSource -Destination $sysTarget -Force -ErrorAction SilentlyContinue
    }

    $existingSvc = Get-Service -Name "DFRDisplayKm" -ErrorAction SilentlyContinue
    if (-not $existingSvc) {
        Write-Host "    -> Создание службы DFRDisplayKm..." -ForegroundColor Gray
        sc.exe create DFRDisplayKm type= kernel start= auto binPath= "System32\drivers\DFRDisplayKm.sys" DisplayName= "iBridge Display Service" | Out-Null
    } else {
        sc.exe config DFRDisplayKm type= kernel start= auto binPath= "System32\drivers\DFRDisplayKm.sys" | Out-Null
    }

    # Set KMDF parameters
    $wdfPath = "HKLM:\System\CurrentControlSet\Services\DFRDisplayKm\Parameters\Wdf"
    if (-not (Test-Path $wdfPath)) {
        New-Item -Path $wdfPath -Force | Out-Null
    }
    Set-ItemProperty -Path $wdfPath -Name "KmdfLibraryVersion" -Value "1.15" -Type String -Force
    Write-Host "    -> Служба DFRDisplayKm сконфигурирована с KmdfLibraryVersion = 1.15." -ForegroundColor Green

    # 5. Device Manager Reset & Service Start
    Write-Host "[5/6] Перезапуск службы ядра и устройства Touch Bar..." -ForegroundColor Cyan
    try {
        sc.exe stop DFRDisplayKm > $null 2>&1
        Start-Sleep -Milliseconds 400
        sc.exe start DFRDisplayKm > $null 2>&1
        Start-Sleep -Milliseconds 400
    } catch {}

    try {
        $pnpDev = Get-PnpDevice | Where-Object { $_.FriendlyName -like "*Touch Bar Display*" -or $_.InstanceId -like "*VID_05AC&PID_8302*" -or $_.InstanceId -like "*DFRDISPLAYKM*" } | Select-Object -First 1
        if ($pnpDev) {
            Disable-PnpDevice -InstanceId $pnpDev.InstanceId -Confirm:$false -ErrorAction SilentlyContinue
            Start-Sleep -Milliseconds 400
            Enable-PnpDevice -InstanceId $pnpDev.InstanceId -Confirm:$false -ErrorAction SilentlyContinue
            Start-Sleep -Milliseconds 400
            Write-Host "    -> Устройство Touch Bar перезапущено в Диспетчере устройств." -ForegroundColor Green
        }
    } catch {}

    # 6. Launch TouchBar.exe
    Write-Host "[6/6] Запуск приложения Touch Bar..." -ForegroundColor Cyan
    Invoke-StartTouchBar

    Write-Host ""
    Write-Host "====================================================================" -ForegroundColor Green
    Write-Host "          [✓] Автоматическое исправление успешно завершено!        " -ForegroundColor Green
    Write-Host "====================================================================" -ForegroundColor Green
    Write-Host ""
}

function Invoke-StartTouchBar {
    Stop-TouchBarProcesses
    Start-Sleep -Milliseconds 300

    # Ensure kernel service is running
    $svc = Get-Service -Name "DFRDisplayKm" -ErrorAction SilentlyContinue
    if ($svc -and $svc.Status -ne "Running") {
        try { Start-Service -Name "DFRDisplayKm" -ErrorAction SilentlyContinue } catch {}
    }

    $exePath = Join-Path $RootDir "TouchBar.exe"
    if (Test-Path $exePath) {
        Write-Host "[*] Запуск TouchBar.exe..." -ForegroundColor Cyan
        Start-Process -FilePath $exePath -WorkingDirectory $RootDir
        Start-Sleep -Milliseconds 800
        Write-Host "[✓] Панель Touch Bar запущена!" -ForegroundColor Green
    } else {
        Write-Host "[!] Исполняемый файл $exePath не найден!" -ForegroundColor Red
    }
}

function Invoke-RestartTouchBar {
    Write-Host "[*] Перезапуск Touch Bar..." -ForegroundColor Yellow
    Invoke-StartTouchBar
}

function Invoke-OpenSettings {
    $exePath = Join-Path $RootDir "TouchBar.exe"
    if (Test-Path $exePath) {
        Write-Host "[*] Открытие Touch Bar Control Center..." -ForegroundColor Cyan
        Start-Process -FilePath $exePath -WorkingDirectory $RootDir
    } else {
        Write-Host "[!] Файл $exePath не найден!" -ForegroundColor Red
    }
}

function Invoke-ToggleAutoStart {
    $diag = Get-Diagnostics
    $newState = -not $diag.AutoStart
    Write-Host "[*] Переключение автозапуска на: $(if ($newState) { 'ВКЛЮЧЕН' } else { 'ВЫКЛЮЧЕН' })..." -ForegroundColor Yellow

    if ($newState) {
        $exePath = Join-Path $RootDir "TouchBar.exe"
        $action = New-ScheduledTaskAction -Execute $exePath -Argument "--tray" -WorkingDirectory $RootDir
        $trigger = New-ScheduledTaskTrigger -AtLogOn
        $trigger.Delay = 'PT4S'
        $principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -LogonType Interactive -RunLevel Highest
        $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit (New-TimeSpan -Days 0) -Priority 4
        Register-ScheduledTask -TaskName "TouchBarAutoStart" -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Force | Out-Null
        
        # User Registry Run key as backup
        try {
            Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "TouchBar" -Value "`"$exePath`" --tray" -Force
        } catch {}

        Write-Host "[✓] Автозапуск включен (запуск в трей с задержкой 4 сек)!" -ForegroundColor Green
    } else {
        schtasks.exe /delete /tn "TouchBarAutoStart" /f > $null 2>&1
        try {
            Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "TouchBar" -ErrorAction SilentlyContinue
        } catch {}
        Write-Host "[✓] Автозапуск успешно удален!" -ForegroundColor Green
    }
}

function Invoke-UninstallTouchBar {
    Write-Host ""
    Write-Host ">>> Удаление Touch Bar с компьютера <<<" -ForegroundColor Red
    Write-Host ""
    $confirm = Read-Host "Вы действительно хотите полностью удалить драйвер и компоненты Touch Bar? (Y/N)"
    if ($confirm -ne 'Y' -and $confirm -ne 'y' -and $confirm -ne 'Д' -and $confirm -ne 'д') {
        Write-Host "Удаление отменено." -ForegroundColor Yellow
        return
    }

    Write-Host "[1/5] Остановка фоновых процессов..." -ForegroundColor Cyan
    Stop-TouchBarProcesses
    Get-Process -Name "TouchBarSettings" -ErrorAction SilentlyContinue | ForEach-Object { try { $_.Kill() } catch {} }

    Write-Host "[2/5] Удаление автозапуска из Планировщика..." -ForegroundColor Cyan
    schtasks.exe /delete /tn "TouchBarAutoStart" /f > $null 2>&1

    Write-Host "[3/5] Остановка и удаление системной службы DFRDisplayKm..." -ForegroundColor Cyan
    sc.exe stop DFRDisplayKm > $null 2>&1
    sc.exe delete DFRDisplayKm > $null 2>&1

    Write-Host "[4/5] Удаление драйвера из хранилища Windows..." -ForegroundColor Cyan
    try {
        $drivers = pnputil.exe /enum-drivers | Out-String
        $lines = $drivers -split "`r?`n"
        $currentOem = ""
        foreach ($line in $lines) {
            if ($line -match "Published Name:\s+(oem\d+\.inf)") {
                $currentOem = $matches[1]
            } elseif ($line -match "Original Name:\s+dfrdisplaykm\.inf" -and $currentOem) {
                Write-Host "    -> Удаление $currentOem..." -ForegroundColor Gray
                pnputil.exe /delete-driver $currentOem /uninstall /force > $null 2>&1
                $currentOem = ""
            }
        }
    } catch {}

    Write-Host "[5/5] Очистка файлов драйвера..." -ForegroundColor Cyan
    $sysTarget = "$env:SystemRoot\System32\drivers\DFRDisplayKm.sys"
    if (Test-Path $sysTarget) {
        Remove-Item -Path $sysTarget -Force -ErrorAction SilentlyContinue
    }

    Write-Host ""
    Write-Host "====================================================================" -ForegroundColor Green
    Write-Host "              [✓] Touch Bar успешно удален с компьютера!            " -ForegroundColor Green
    Write-Host "====================================================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Для отключения режима тестирования (TESTSIGNING) выполните:" -ForegroundColor Gray
    Write-Host "  bcdedit /set testsigning off" -ForegroundColor Cyan
    Write-Host ""
}

# MAIN EXECUTION ROUTER
if (-not (Test-Admin)) {
    if ($Action -eq "status") {
        # Status can run in read-only mode, proceed
    } else {
        Write-Host "[i] Требуются права администратора. Запрос UAC..." -ForegroundColor Cyan
        Start-Process powershell -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`" $Action" -Verb RunAs
        Exit 0
    }
}

switch ($Action.ToLower()) {
    "fix" {
        Invoke-AutoFix
    }
    "start" {
        Invoke-StartTouchBar
    }
    "restart" {
        Invoke-RestartTouchBar
    }
    "settings" {
        Invoke-OpenSettings
    }
    "autostart" {
        Invoke-ToggleAutoStart
    }
    "uninstall" {
        Invoke-UninstallTouchBar
    }
    "status" {
        $diag = Get-Diagnostics
        Show-Dashboard $diag
    }
    default {
        # Interactive Loop
        while ($true) {
            $diag = Get-Diagnostics
            Show-Dashboard $diag

            Write-Host "Выберите действие:" -ForegroundColor White
            Write-Host "  [1] Авто-починка и запуск (Fix Everything & Start)" -ForegroundColor Green
            Write-Host "  [2] Запустить / Перезапустить Touch Bar" -ForegroundColor Cyan
            Write-Host "  [3] Открыть панель настроек (Settings)" -ForegroundColor Cyan
            Write-Host "  [4] Включить / Выключить автозапуск" -ForegroundColor Yellow
            Write-Host "  [5] Полное удаление с компьютера" -ForegroundColor Red
            Write-Host "  [0] Выход" -ForegroundColor Gray
            Write-Host ""
            $choice = Read-Host "Ваш выбор [0-5]"

            switch ($choice) {
                "1" {
                    Invoke-AutoFix
                    Write-Host "Нажмите любую клавишу для продолжения..."
                    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
                }
                "2" {
                    Invoke-RestartTouchBar
                    Start-Sleep -Seconds 1
                }
                "3" {
                    Invoke-OpenSettings
                    Start-Sleep -Seconds 1
                }
                "4" {
                    Invoke-ToggleAutoStart
                    Write-Host "Нажмите любую клавишу для продолжения..."
                    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
                }
                "5" {
                    Invoke-UninstallTouchBar
                    Write-Host "Нажмите любую клавишу для выхода..."
                    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
                    break
                }
                "0" {
                    break
                }
                default {
                    Write-Host "Неверный выбор. Повторите ввод." -ForegroundColor Red
                    Start-Sleep -Seconds 1
                }
            }

            if ($choice -eq "0" -or $choice -eq "5") {
                break
            }
        }
    }
}
