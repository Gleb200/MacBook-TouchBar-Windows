@echo off
title Restart and Update Touch Bar
cd /d "%~dp0"

net session >nul 2>&1
if %ERRORLEVEL% equ 0 (
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Fix_TouchBar.ps1"
) else (
    echo Requesting Administrator privileges (UAC)...
    powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "Start-Process powershell.exe -ArgumentList '-NoProfile -ExecutionPolicy Bypass -File \"\"%~dp0Fix_TouchBar.ps1\"\"' -Verb RunAs"
)
