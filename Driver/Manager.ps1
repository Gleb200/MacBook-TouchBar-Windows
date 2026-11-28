# MacBook Touch Bar - Unified System Manager & Diagnostics
# v1.0.2 - Robust driver installation, safe .sys replacement and manual-install guidance.
# Encoded in UTF-8 (with BOM for Windows PowerShell 5.1)
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
$script:NeedsReboot = $false
$script:DriverFilePending = $false

function Test-Admin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Get-Diagnostics {
    $diag = [PSCustomObject]@{
        TestMode = $false
        CertInstalled = $false
        DriverStoreInstalled = $false
        ServiceStatus = "Missing" # OK, Stopped, NotConfigured, Missing
        DeviceStatus = "NotFound" # OK, Code19, Code52, Disabled, NotConfigured, Error, NotFound
        DeviceCode = -1
        ParentPresent = $false
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
        $rootCerts = Get-ChildItem -Path Cert:\LocalMachine\Root -ErrorAction SilentlyContinue | Where-Object { $_.Thumbprint -eq $targetThumb -or $_.Subject -match "WDKTestCert" -or $_.Subject -match "DFRDisplayKm" }
        $pubCerts = Get-ChildItem -Path Cert:\LocalMachine\TrustedPublisher -ErrorAction SilentlyContinue | Where-Object { $_.Thumbprint -eq $targetThumb -or $_.Subject -match "WDKTestCert" -or $_.Subject -match "DFRDisplayKm" }
        if ($rootCerts -and $pubCerts) {
            $diag.CertInstalled = $true
        }
    } catch {}

    # 3. Driver package in the Windows DriverStore
    try {
        $drvOut = pnputil.exe /enum-drivers 2>&1 | Out-String
        if ($drvOut -match "dfrdisplaykm\.inf") {
            $diag.DriverStoreInstalled = $true
        }
    } catch {}

    # 4. Kernel Service
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

    # 5. Hardware Device (prefer the &MI_01 display interface)
    try {
        $allDevices = Get-PnpDevice -ErrorAction SilentlyContinue
        $parent = $allDevices | Where-Object { $_.InstanceId -match '^USB\\VID_05AC&PID_(8302|8600)(\\|$)' } | Select-Object -First 1
        if ($parent) { $diag.ParentPresent = $true }

        $dev = $allDevices | Where-Object { $_.InstanceId -match 'VID_05AC&PID_(8302|8600)&MI_01' } | Select-Object -First 1
        if (-not $dev) {
            $dev = $allDevices | Where-Object { $_.FriendlyName -like "*Touch Bar Display*" -or $_.InstanceId -like "*DFRDISPLAYKM*" } | Select-Object -First 1
        }

        if ($dev) {
            $diag.DeviceCode = $dev.Problem
            if ($dev.Status -eq "OK" -and ($dev.Problem -eq 0 -or $null -eq $dev.Problem)) {
                $diag.DeviceStatus = "OK"
            } elseif ($dev.Problem -eq 19) {
                $diag.DeviceStatus = "Code19"
            } elseif ($dev.Problem -eq 52) {
                $diag.DeviceStatus = "Code52"
            } elseif ($dev.Problem -eq 22 -or $dev.Status -eq "Disabled") {
                $diag.DeviceStatus = "Disabled"
            } else {
                $diag.DeviceStatus = "Error"
            }
        } elseif ($diag.ParentPresent) {
            # Composite controller is present but the display interface was not created.
            $diag.DeviceStatus = "NotConfigured"
        } else {
            $diag.DeviceStatus = "NotFound"
        }
    } catch {}

    # 6. Autostart (Task Scheduler or HKCU Run fallback)
    try {
        schtasks /query /tn "TouchBarAutoStart" 2>&1 | Out-Null
        if ($LASTEXITCODE -eq 0) {
            $diag.AutoStart = $true
        } else {
            $runValue = (Get-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "TouchBar" -ErrorAction SilentlyContinue).TouchBar
            if ($runValue) { $diag.AutoStart = $true }
        }
    } catch {}

    # 7. Running Process
    try {
        $pids = @()
        $procs = Get-Process -Name "TouchBar", "DFRContentHost" -ErrorAction SilentlyContinue
        if ($procs) {
            $pids += ($procs | ForEach-Object { $_.Id })
        }
        $dotnetDir = Join-Path $RootDir "dotnet"
        $dotnetPath = Join-Path $dotnetDir "dotnet.exe"
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

    # 3. DriverStore
    Write-Host " [3] Пакет драйвера в DriverStore:     " -NoNewline
    if ($diag.DriverStoreInstalled) {
        Write-Host "[ OK ] Зарегистрирован (dfrdisplaykm.inf)" -ForegroundColor Green
    } else {
        Write-Host "[ !! ] Не найден" -ForegroundColor Red
    }

    # 4. Kernel Service
    Write-Host " [4] Служба ядра DFRDisplayKm:         " -NoNewline
    switch ($diag.ServiceStatus) {
        "OK"            { Write-Host "[ OK ] Активна (KMDF 1.15, работает)" -ForegroundColor Green }
        "Stopped"       { Write-Host "[ !! ] Остановлена (требуется запуск)" -ForegroundColor Yellow }
        "NotConfigured" { Write-Host "[ !! ] Ошибка параметров KMDF (Code 19)" -ForegroundColor Red }
        "Missing"       { Write-Host "[ !! ] Служба не создана" -ForegroundColor Red }
    }

    # 5. Device Status
    Write-Host " [5] Оборудование Touch Bar:           " -NoNewline
    switch ($diag.DeviceStatus) {
        "OK"            { Write-Host "[ OK ] Работает нормально (Code 0)" -ForegroundColor Green }
        "Code19"        { Write-Host "[ !! ] Ошибка службы/реестра (Code 19)" -ForegroundColor Red }
        "Code52"        { Write-Host "[ !! ] Ошибка подписи (Code 52 - нужен Test Mode и перезагрузка)" -ForegroundColor Red }
        "Disabled"      { Write-Host "[ !! ] Устройство отключено в Диспетчере" -ForegroundColor Yellow }
        "NotConfigured" { Write-Host "[ !! ] iBridge найден, но дисплей не настроен (нужен драйвер)" -ForegroundColor Yellow }
        "NotFound"      { Write-Host "[ !! ] Устройство не найдено" -ForegroundColor Yellow }
        default         { Write-Host "[ !! ] Код ошибки: $($diag.DeviceCode)" -ForegroundColor Red }
    }

    # 6. Autostart
    Write-Host " [6] Автозапуск с Windows:             " -NoNewline
    if ($diag.AutoStart) {
        Write-Host "[ OK ] Включен" -ForegroundColor Green
    } else {
        Write-Host "[ -- ] Отключен" -ForegroundColor Gray
    }

    # 7. Process
    Write-Host " [7] Процесс Touch Bar:                " -NoNewline
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
    $procs = Get-Process -Name "TouchBar", "TouchBarSettings", "DFRContentHost" -ErrorAction SilentlyContinue
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

function Stop-DriverService {
    $svc = Get-Service -Name "DFRDisplayKm" -ErrorAction SilentlyContinue
    if ($svc -and $svc.Status -ne "Stopped") {
        Write-Host "    -> Остановка службы DFRDisplayKm (освобождение DFRDisplayKm.sys)..." -ForegroundColor Gray
        sc.exe stop DFRDisplayKm | Out-Null
        for ($i = 0; $i -lt 20; $i++) {
            Start-Sleep -Milliseconds 300
            try { $svc.Refresh() } catch { break }
            if ($svc.Status -eq "Stopped") { break }
        }
    }
}

function Copy-DriverFileSafe {
    param([string]$Source, [string]$Destination)

    if (-not (Test-Path $Source)) {
        Write-Host "    [!] Файл драйвера не найден: $Source" -ForegroundColor Red
        return $false
    }

    try {
        if ((Test-Path $Destination) -and ((Get-FileHash $Source -ErrorAction SilentlyContinue).Hash -eq (Get-FileHash $Destination -ErrorAction SilentlyContinue).Hash)) {
            Write-Host "    -> DFRDisplayKm.sys уже актуален." -ForegroundColor Green
            return $true
        }
    } catch {}

    for ($i = 1; $i -le 6; $i++) {
        try {
            Copy-Item -Path $Source -Destination $Destination -Force -ErrorAction Stop
            Write-Host "    -> DFRDisplayKm.sys скопирован (попытка $i)." -ForegroundColor Green
            return $true
        } catch {
            Write-Host "    [!] Попытка $i не удалась: $($_.Exception.Message)" -ForegroundColor Yellow
            Start-Sleep -Milliseconds 600
        }
    }

    # The file can still be mapped by a previously loaded driver. Schedule the
    # replacement for the next boot instead of silently ignoring the error.
    try {
        $tempDir = Join-Path $env:SystemRoot "Temp"
        if (-not (Test-Path $tempDir)) { $tempDir = $env:TEMP }
        $tempFile = Join-Path $tempDir "DFRDisplayKm.sys.new"
        Copy-Item -Path $Source -Destination $tempFile -Force -ErrorAction Stop

        if (-not ("Win32.NativeMethods" -as [type])) {
            Add-Type -Namespace Win32 -Name NativeMethods -MemberDefinition @'
[DllImport("kernel32.dll", SetLastError=true, CharSet=CharSet.Unicode)]
public static extern bool MoveFileEx(string lpExistingFileName, string lpNewFileName, int dwFlags);
'@
        }

        $flags = 0x4 -bor 0x1  # MOVEFILE_DELAY_UNTIL_REBOOT | MOVEFILE_REPLACE_EXISTING
        if ([Win32.NativeMethods]::MoveFileEx($tempFile, $Destination, $flags)) {
            Write-Host "    [!] Файл заблокирован загруженным драйвером. Замена запланирована на следующую перезагрузку." -ForegroundColor Yellow
            return $false
        }
    } catch {
        Write-Host "    [!] Не удалось запланировать замену файла: $($_.Exception.Message)" -ForegroundColor Yellow
    }

    return $false
}

function Install-DriverCertificate {
    $cerPath = Join-Path $DriverDir "DFRDisplayKm.cer"
    if (-not (Test-Path $cerPath)) {
        Write-Host "    [!] Сертификат не найден по пути: $cerPath" -ForegroundColor Red
        return
    }

    try {
        $cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($cerPath)

        foreach ($storeName in @([System.Security.Cryptography.X509Certificates.StoreName]::Root,
                                 [System.Security.Cryptography.X509Certificates.StoreName]::TrustedPublisher)) {
            $store = New-Object System.Security.Cryptography.X509Certificates.X509Store($storeName, [System.Security.Cryptography.X509Certificates.StoreLocation]::LocalMachine)
            $store.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
            $store.Add($cert)
            $store.Close()
        }

        certutil -addstore -f "Root" $cerPath > $null 2>&1
        certutil -addstore -f "TrustedPublisher" $cerPath > $null 2>&1
        Write-Host "    -> Сертификат установлен в Root и TrustedPublisher." -ForegroundColor Green
    } catch {
        Write-Host "    [!] Ошибка установки сертификата: $($_.Exception.Message)" -ForegroundColor Red
    }
}

function Restart-TouchBarDevice {
    $parents = Get-PnpDevice -ErrorAction SilentlyContinue | Where-Object { $_.InstanceId -match '^USB\\VID_05AC&PID_(8302|8600)(\\|$)' }
    if (-not $parents) {
        Write-Host "    [!] Контроллер iBridge (USB\VID_05AC&PID_8302/8600) не найден." -ForegroundColor Yellow
        return
    }

    foreach ($d in $parents) {
        Write-Host "    -> Перезапуск $($d.FriendlyName) [$($d.InstanceId)]..." -ForegroundColor Gray
        try {
            Disable-PnpDevice -InstanceId $d.InstanceId -Confirm:$false -ErrorAction Stop
        } catch {
            Write-Host "       disable: $($_.Exception.Message)" -ForegroundColor DarkYellow
        }
        Start-Sleep -Milliseconds 500
        try {
            Enable-PnpDevice -InstanceId $d.InstanceId -Confirm:$false -ErrorAction Stop
        } catch {
            Write-Host "       enable: $($_.Exception.Message)" -ForegroundColor DarkYellow
        }
    }

    Start-Sleep -Seconds 1
    pnputil.exe /scan-devices 2>&1 | Out-Null
    Start-Sleep -Seconds 1
}

function Show-ManualDriverHelp {
    Write-Host ""
    Write-Host "--------------------------------------------------------------------" -ForegroundColor Yellow
    Write-Host " Ручная установка драйвера (если автоматическая не сработала)" -ForegroundColor Yellow
    Write-Host "--------------------------------------------------------------------" -ForegroundColor Yellow
    Write-Host " 1. Откройте Диспетчер устройств (devmgmt.msc) от администратора."
    Write-Host " 2. Разверните 'Универсальные устройства USB' (Universal Serial Bus devices)."
    Write-Host "    Включите 'Вид -> Показать скрытые устройства', если устройства не видно."
    Write-Host " 3. Найдите РОДИТЕЛЬСКОЕ устройство 'iBridge Display Composition Device'"
    Write-Host "    (USB\VID_05AC&PID_8302 или PID_8600)."
    Write-Host "    ВАЖНО: обновляйте драйвер у родительского устройства, а НЕ у дочернего"
    Write-Host "    элемента с суффиксом &MI_01."
    Write-Host " 4. ПКМ -> 'Обновить драйвер' -> 'Выполнить поиск драйверов на этом компьютере'"
    Write-Host "    -> 'Выбрать драйвер из списка доступных драйверов' -> 'Установить с диска...'"
    Write-Host "    -> укажите файл: $DriverDir\DFRDisplayKm.inf"
    Write-Host "    -> выберите 'iBridge Display Composition Device'."
    Write-Host " 5. Перезагрузите MacBook."
    Write-Host ""
    Write-Host " НЕ выбирайте 'USB Composite Device' или другой универсальный драйвер:"
    Write-Host " именно установка INF на родительское устройство переключает iBridge"
    Write-Host " в конфигурацию Touch Bar. Установка драйвера только на &MI_01 не работает."
    Write-Host "--------------------------------------------------------------------" -ForegroundColor Yellow
    Write-Host ""
}

function Invoke-AutoFix {
    Write-Host ""
    Write-Host ">>> Установка / автоматическое исправление Touch Bar <<<" -ForegroundColor Yellow
    Write-Host ""

    $script:NeedsReboot = $false
    $script:DriverFilePending = $false

    Stop-TouchBarProcesses
    Stop-DriverService

    # 1. Test Mode
    Write-Host "[1/7] Проверка режима тестовой подписи (TESTSIGNING)..." -ForegroundColor Cyan
    $diag = Get-Diagnostics
    if (-not $diag.TestMode) {
        try {
            bcdedit /set testsigning on 2>&1 | Out-Null
            Write-Host "    -> Test Mode включен в BCD. Требуется перезагрузка." -ForegroundColor Yellow
            $script:NeedsReboot = $true
        } catch {
            Write-Host "    [!] Не удалось изменить BCD: $($_.Exception.Message)" -ForegroundColor Red
        }
    } else {
        Write-Host "    -> Test Mode уже активен." -ForegroundColor Green
    }

    # 2. Certificate
    Write-Host "[2/7] Установка сертификата DFRDisplayKm.cer..." -ForegroundColor Cyan
    Install-DriverCertificate

    # 3. Driver package registration
    Write-Host "[3/7] Регистрация пакета драйвера DFRDisplayKm.inf..." -ForegroundColor Cyan
    $infPath = Join-Path $DriverDir "DFRDisplayKm.inf"
    if (Test-Path $infPath) {
        $pnpOut = & pnputil.exe /add-driver "$infPath" /install 2>&1 | Out-String
        Write-Host $pnpOut
        if ($LASTEXITCODE -eq 0 -or $pnpOut -match "successfully|успешно|already|уже") {
            Write-Host "    -> Драйвер зарегистрирован в DriverStore." -ForegroundColor Green
        } else {
            Write-Host "    [!] pnputil вернул ошибку. Смотрите вывод выше." -ForegroundColor Yellow
        }
    } else {
        Write-Host "    [!] INF не найден: $infPath" -ForegroundColor Red
    }

    # 4. Safe .sys replacement (service was stopped above)
    Write-Host "[4/7] Обновление DFRDisplayKm.sys..." -ForegroundColor Cyan
    $sysTarget = "$env:SystemRoot\System32\drivers\DFRDisplayKm.sys"
    $sysSource = Join-Path $DriverDir "DFRDisplayKm.sys"
    $copied = Copy-DriverFileSafe -Source $sysSource -Destination $sysTarget
    if (-not $copied) {
        $script:DriverFilePending = $true
        $script:NeedsReboot = $true
    }

    # 5. Kernel service + KMDF parameters
    Write-Host "[5/7] Настройка службы ядра DFRDisplayKm (KMDF 1.15)..." -ForegroundColor Cyan
    $existingSvc = Get-Service -Name "DFRDisplayKm" -ErrorAction SilentlyContinue
    if (-not $existingSvc) {
        Write-Host "    -> Создание службы DFRDisplayKm..." -ForegroundColor Gray
        sc.exe create DFRDisplayKm type= kernel start= auto binPath= "System32\drivers\DFRDisplayKm.sys" DisplayName= "iBridge Display Service" | Out-Null
    } else {
        sc.exe config DFRDisplayKm type= kernel start= auto binPath= "System32\drivers\DFRDisplayKm.sys" | Out-Null
    }

    $wdfPath = "HKLM:\System\CurrentControlSet\Services\DFRDisplayKm\Parameters\Wdf"
    if (-not (Test-Path $wdfPath)) {
        New-Item -Path $wdfPath -Force | Out-Null
    }
    Set-ItemProperty -Path $wdfPath -Name "KmdfLibraryVersion" -Value "1.15" -Type String -Force
    Write-Host "    -> Служба настроена (KmdfLibraryVersion = 1.15)." -ForegroundColor Green

    # 6. Device reset / re-enumeration
    Write-Host "[6/7] Перезапуск контроллера iBridge и устройства Touch Bar..." -ForegroundColor Cyan
    Restart-TouchBarDevice

    # 7. Start service and verify
    Write-Host "[7/7] Запуск службы и проверка состояния..." -ForegroundColor Cyan
    try {
        sc.exe start DFRDisplayKm 2>&1 | Out-Null
        Start-Sleep -Milliseconds 800
    } catch {}

    $diag = Get-Diagnostics
    Write-Host ""
    Show-Dashboard $diag

    if ($script:NeedsReboot -or $diag.DeviceStatus -eq "Code52") {
        Write-Host ""
        Write-Host "[!] Для применения изменений нужна ПЕРЕЗАГРУЗКА Windows." -ForegroundColor Yellow
        if ($script:DriverFilePending) {
            Write-Host "    Файл драйвера будет заменен автоматически при следующей загрузке." -ForegroundColor Yellow
        }
        $answer = Read-Host "Перезагрузить компьютер сейчас? (Y/N)"
        if ($answer -eq 'Y' -or $answer -eq 'y' -or $answer -eq 'Д' -or $answer -eq 'д') {
            Restart-Computer -Force
        } else {
            Write-Host "    Перезагрузите компьютер вручную, затем запустите TouchBar.bat start." -ForegroundColor Cyan
        }
        return
    }

    if ($diag.DeviceStatus -eq "OK") {
        Write-Host ""
        Write-Host "[✓] Драйвер и устройство Touch Bar работают нормально!" -ForegroundColor Green
        Invoke-StartTouchBar
    } else {
        Write-Host ""
        Write-Host "[!] Драйвер установлен, но дисплей Touch Bar пока не активен." -ForegroundColor Yellow
        Write-Host "    Код состояния: $($diag.DeviceStatus)." -ForegroundColor Yellow
        Show-ManualDriverHelp
        Write-Host "    После ручной установки драйвера перезагрузите MacBook и запустите TouchBar.bat start." -ForegroundColor Cyan
    }
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
        $principal = New-ScheduledTaskPrincipal -UserId "$env:USERDOMAIN\$env:USERNAME" -LogonType Interactive -RunLevel Highest
        $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit (New-TimeSpan -Days 0) -Priority 4
        $taskOk = $true
        try {
            Register-ScheduledTask -TaskName "TouchBarAutoStart" -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Force | Out-Null
        } catch {
            $taskOk = $false
            try {
                Register-ScheduledTask -TaskName "TouchBarAutoStart" -Action $action -Trigger $trigger -Settings $settings -Force | Out-Null
                $taskOk = $true
            } catch {
                Write-Host "    [!] Не удалось создать задачу: $($_.Exception.Message)" -ForegroundColor Yellow
            }
        }

        if ($taskOk) {
            # Avoid a second, non-elevated logon entry racing the scheduled task.
            Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "TouchBar" -ErrorAction SilentlyContinue
            Write-Host "[✓] Автозапуск включен (Планировщик Windows, без задержки)." -ForegroundColor Green
            Write-Host "    Задержка запуска настраивается в окне настроек Touch Bar." -ForegroundColor Gray
        } else {
            try {
                Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "TouchBar" -Value "`"$exePath`" --tray" -Force
                Write-Host "[✓] Автозапуск включен через реестр (HKCU Run)." -ForegroundColor Green
            } catch {}
        }
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

    Write-Host "[1/6] Остановка фоновых процессов..." -ForegroundColor Cyan
    Stop-TouchBarProcesses
    Get-Process -Name "TouchBarSettings" -ErrorAction SilentlyContinue | ForEach-Object { try { $_.Kill() } catch {} }

    Write-Host "[2/6] Удаление автозапуска..." -ForegroundColor Cyan
    schtasks.exe /delete /tn "TouchBarAutoStart" /f > $null 2>&1
    try {
        Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "TouchBar" -ErrorAction SilentlyContinue
    } catch {}

    Write-Host "[3/6] Остановка и удаление системной службы DFRDisplayKm..." -ForegroundColor Cyan
    sc.exe stop DFRDisplayKm > $null 2>&1
    Start-Sleep -Milliseconds 500
    sc.exe delete DFRDisplayKm > $null 2>&1

    Write-Host "[4/6] Удаление драйвера из хранилища Windows..." -ForegroundColor Cyan
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

    Write-Host "[5/6] Очистка файлов драйвера..." -ForegroundColor Cyan
    $sysTarget = "$env:SystemRoot\System32\drivers\DFRDisplayKm.sys"
    if (Test-Path $sysTarget) {
        Remove-Item -Path $sysTarget -Force -ErrorAction SilentlyContinue
    }

    Write-Host "[6/6] Удаление сертификатов драйвера..." -ForegroundColor Cyan
    foreach ($store in @("Cert:\LocalMachine\Root", "Cert:\LocalMachine\TrustedPublisher")) {
        try {
            Get-ChildItem -Path $store -ErrorAction SilentlyContinue | Where-Object { $_.Subject -match "WDKTestCert|DFRDisplayKm" } | ForEach-Object {
                Remove-Item -Path $_.PSPath -Force -ErrorAction SilentlyContinue
            }
        } catch {}
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
    "install" {
        Invoke-AutoFix
    }
    "repair" {
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
            Write-Host "  [1] Установить / Авто-починка и запуск (Fix Everything & Start)" -ForegroundColor Green
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
