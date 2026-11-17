# Fix_TouchBar.ps1 - Скрипт восстановления службы, устройства, обновления и автозапуска Touch Bar
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$Host.UI.RawUI.WindowTitle = "Настройка Touch Bar и службы DFRDisplayKm"

Write-Host "=======================================================" -ForegroundColor Cyan
Write-Host "   Настройка Touch Bar и службы DFRDisplayKm (Admin)   " -ForegroundColor Cyan
Write-Host "=======================================================" -ForegroundColor Cyan
Write-Host ""

# Проверка административных привилегий
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "[ОШИБКА] Скрипт запущен без прав администратора!" -ForegroundColor Red
    Write-Host "Пожалуйста, запустите Restart_TouchBar.bat от имени администратора." -ForegroundColor Yellow
    Read-Host "Нажмите Enter для выхода..."
    exit 1
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$dotnetDir = Join-Path $scriptDir "dotnet"
$hostAppDir = Join-Path $scriptDir "HostApp_v2"
$exePath = Join-Path $scriptDir "TouchBar.exe"
$releaseBinDir = Join-Path $scriptDir "Source\DFRContentHost-master\DFRContentHost\bin\Release\netcoreapp3.0"

# 1. Системные переменные окружения
Write-Host "[1/6] Проверка системных переменных окружения..." -ForegroundColor Yellow
try {
    [Environment]::SetEnvironmentVariable("DOTNET_ROOT", $dotnetDir, "Machine")
    $machinePath = [Environment]::GetEnvironmentVariable("Path", "Machine")
    if ($machinePath -notlike "*$dotnetDir*") {
        [Environment]::SetEnvironmentVariable("Path", "$dotnetDir;$machinePath", "Machine")
    }
    Write-Host "      DOTNET_ROOT настроен: $dotnetDir" -ForegroundColor Green
} catch {
    Write-Host "      Предупреждение: $_" -ForegroundColor DarkYellow
}

# 2. Настройка службы DFRDisplayKm
Write-Host "[2/6] Настройка службы драйвера DFRDisplayKm..." -ForegroundColor Yellow
try {
    sc.exe config DFRDisplayKm start= auto | Out-Null
    sc.exe start DFRDisplayKm 2>&1 | Out-Null
    sc.exe config SensrSvc start= auto | Out-Null
    sc.exe start SensrSvc 2>&1 | Out-Null
    Start-Sleep -Milliseconds 500
    $svc = Get-Service -Name DFRDisplayKm -ErrorAction SilentlyContinue
    Write-Host "      Служба DFRDisplayKm: $($svc.Status) (тип запуска: $($svc.StartType))" -ForegroundColor $(if ($svc.Status -eq 'Running') { "Green" } else { "Yellow" })
    $sensr = Get-Service -Name SensrSvc -ErrorAction SilentlyContinue
    Write-Host "      Служба датчиков (SensrSvc): $($sensr.Status) (тип запуска: $($sensr.StartType))" -ForegroundColor $(if ($sensr.Status -eq 'Running') { "Green" } else { "Yellow" })
} catch {
    Write-Host "      Ошибка при настройке службы: $_" -ForegroundColor Red
}

# 3. Перезапуск составного устройства Touch Bar (переключение конфигурации T2 на дисплей)
Write-Host "[3/6] Проверка состояния USB-контроллера Touch Bar..." -ForegroundColor Yellow
try {
    $disp = Get-PnpDevice -InstanceId 'USB\VID_05AC&PID_8302&MI_01*' -ErrorAction SilentlyContinue
    if ($disp -and $disp.Status -ne "OK") {
        $parent = Get-PnpDevice -InstanceId 'USB\VID_05AC&PID_8302\0000000000000000' -ErrorAction SilentlyContinue
        if ($parent) {
            Write-Host "      Перезапуск iBridge Composition Device..." -ForegroundColor Gray
            Disable-PnpDevice -InstanceId $parent.InstanceId -Confirm:$false -ErrorAction SilentlyContinue
            Start-Sleep -Seconds 1
            Enable-PnpDevice -InstanceId $parent.InstanceId -Confirm:$false -ErrorAction SilentlyContinue
            Start-Sleep -Seconds 2
        }
    }
    $dispAfter = Get-PnpDevice -InstanceId 'USB\VID_05AC&PID_8302&MI_01*' -ErrorAction SilentlyContinue
    Write-Host "      Состояние Touch Bar Display: $($dispAfter.Status)" -ForegroundColor $(if ($dispAfter.Status -eq 'OK') { "Green" } else { "Yellow" })
} catch {
    Write-Host "      Ошибка при проверке устройств: $_" -ForegroundColor Red
}

# 4. Регистрация задачи автозапуска в Планировщике Windows
Write-Host "[4/6] Регистрация задачи автозапуска в Планировщике Windows..." -ForegroundColor Yellow
try {
    Unregister-ScheduledTask -TaskName "TouchBarAutoStart" -Confirm:$false -ErrorAction SilentlyContinue
    
    $action = New-ScheduledTaskAction -Execute $exePath -WorkingDirectory $scriptDir
    $trigger = New-ScheduledTaskTrigger -AtLogOn
    
    $loggedUser = (Get-CimInstance Win32_ComputerSystem).UserName
    if (-not $loggedUser) { $loggedUser = "$env:USERDOMAIN\$env:USERNAME" }
    
    $principal = New-ScheduledTaskPrincipal -UserId $loggedUser -LogonType Interactive -RunLevel Highest
    $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit (New-TimeSpan -Days 0)
    
    Register-ScheduledTask -TaskName "TouchBarAutoStart" -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Force | Out-Null
    Write-Host "      Задача 'TouchBarAutoStart' активна (запуск при входе)!" -ForegroundColor Green
} catch {
    Write-Host "      Ошибка при регистрации задачи: $_" -ForegroundColor Red
}

# 5. Очистка устаревших ключей из реестра
Write-Host "[5/6] Очистка старых записей автозапуска..." -ForegroundColor Yellow
Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'TouchBar' -ErrorAction SilentlyContinue
$startupLnk = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Startup\TouchBar.lnk'
if (Test-Path $startupLnk) { Remove-Item $startupLnk -Force -ErrorAction SilentlyContinue }
Write-Host "      Реестр и папка Startup очищены" -ForegroundColor Green

# 6. Завершение старого процесса, копирование новой сборки и запуск
Write-Host "[6/6] Обновление приложения и перезапуск Touch Bar..." -ForegroundColor Yellow

# Надежное завершение всех связанных процессов
Stop-Process -Name TouchBar, DFRContentHost, TouchBarSettings -Force -ErrorAction SilentlyContinue
Get-CimInstance Win32_Process -ErrorAction SilentlyContinue | Where-Object {
    $_.Name -eq "dotnet.exe" -and ($_.CommandLine -like "*DFRContentHost*" -or $_.CommandLine -like "*HostApp_v2*" -or $_.CommandLine -like "*DFRDisplayKm*")
} | ForEach-Object {
    Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue
}
Get-Process -Name dotnet -ErrorAction SilentlyContinue | Where-Object { 
    $_.Path -like "*DFRDisplayKm*" -or $_.Path -like "*dotnet*" 
} | Stop-Process -Force -ErrorAction SilentlyContinue

$waitAttempts = 15
while ($waitAttempts -gt 0) {
    $running = Get-Process -Name dotnet, TouchBar, DFRContentHost, TouchBarSettings -ErrorAction SilentlyContinue | Where-Object {
        $_.Path -like "*DFRDisplayKm*" -or $_.Path -like "*dotnet*"
    }
    if (-not $running) { break }
    Start-Sleep -Milliseconds 200
    $waitAttempts--
}
Start-Sleep -Milliseconds 400

if (Test-Path $releaseBinDir) {
    Copy-Item "$releaseBinDir\*" -Destination $hostAppDir -Recurse -Force -ErrorAction SilentlyContinue
    
    # Копируем реальную реализацию System.Management для Windows (294 КБ)
    $realMgmt = Join-Path $releaseBinDir "runtimes\win\lib\netcoreapp2.0\System.Management.dll"
    if (Test-Path $realMgmt) {
        Copy-Item $realMgmt -Destination (Join-Path $hostAppDir "System.Management.dll") -Force -ErrorAction SilentlyContinue
    }
    
    # Копируем GUI-конфигуратор TouchBarSettings
    $settingsBinDir = Join-Path $scriptDir "Source\DFRContentHost-master\TouchBarSettings\bin\Release\netcoreapp3.1"
    if (Test-Path $settingsBinDir) {
        Copy-Item "$settingsBinDir\*" -Destination $hostAppDir -Recurse -Force -ErrorAction SilentlyContinue
        Copy-Item (Join-Path $settingsBinDir "TouchBarSettings.exe") -Destination (Join-Path $scriptDir "TouchBarSettings.exe") -Force -ErrorAction SilentlyContinue
        Write-Host "      Конфигуратор TouchBarSettings скопирован" -ForegroundColor Green
    }

    # Синхронизируем персонажей (10 встроенных скинов)
    $petsDir = Join-Path $scriptDir "pets"
    if (Test-Path $petsDir) {
        Copy-Item $petsDir -Destination $hostAppDir -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "      Скины персонажей (Pikachu, Dino, Cat, Eevee и др.) скопированы" -ForegroundColor Green
    }

    # Синхронизируем config.json
    $hostCfg = Join-Path $hostAppDir "config.json"
    $rootCfg = Join-Path $scriptDir "config.json"
    if (Test-Path $hostCfg) {
        Copy-Item $hostCfg -Destination $rootCfg -Force -ErrorAction SilentlyContinue
    } elseif (Test-Path $rootCfg) {
        Copy-Item $rootCfg -Destination $hostCfg -Force -ErrorAction SilentlyContinue
    }

    Write-Host "      Свежая сборка скопирована в HostApp_v2" -ForegroundColor Green
}

$cfg = @'
{
  "runtimeOptions": {
    "tfm": "netcoreapp3.0",
    "rollForward": "LatestMajor",
    "framework": {
      "name": "Microsoft.WindowsDesktop.App",
      "version": "3.0.0"
    }
  }
}
'@
Set-Content -Path (Join-Path $hostAppDir "DFRContentHost.runtimeconfig.json") -Value $cfg -Encoding utf8

try {
    Start-Process -FilePath $exePath -WorkingDirectory $scriptDir
    Start-Sleep -Seconds 2
    Write-Host ""
    Write-Host "=======================================================" -ForegroundColor Green
    Write-Host "   ГОТОВО! Touch Bar успешно обновлен и запущен!      " -ForegroundColor Green
    Write-Host "   - Виртуальная кнопка ESC для MacBook Pro 2016-2019 (SendInput)" -ForegroundColor Green
    Write-Host "   - Полноценная поддержка языков (Русский / English / Auto)" -ForegroundColor Green
    Write-Host "   - Детектор Touch ID (Windows Hello) с оверлеем в стиле macOS" -ForegroundColor Green
    Write-Host "   - Гибридный виджет температуры CPU (WMF + MSR DTS) " -ForegroundColor Green
    Write-Host "   - Окно настроек (GUI) в системном трее и TouchBarSettings.exe" -ForegroundColor Green
    Write-Host "   - Автосохранение config.json с применением на лету  " -ForegroundColor Green
    Write-Host "   - Автояркость (SensrSvc + ADAPTBRIGHT в схеме питания)" -ForegroundColor Green
    Write-Host "   - Синхронизация реальной яркости в слайдере        " -ForegroundColor Green
    Write-Host "   - Слайдеры теперь легко достигают 100% и 0%        " -ForegroundColor Green
    Write-Host "=======================================================" -ForegroundColor Green
} catch {
    Write-Host "      Ошибка запуска: $_" -ForegroundColor Red
}

Write-Host ""
Read-Host "Нажмите Enter для завершения..."

