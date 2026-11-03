@echo off
setlocal
title DELTARUNE Mod Importer ^& Update Manager
cd /d "%~dp0"

if "%~1"=="" (
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Import-Mod.ps1"
    goto :done
)

set "FIRST_ARG=%~1"
if "%FIRST_ARG:~0,1%"=="-" (
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Import-Mod.ps1" %*
) else (
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Import-Mod.ps1" -Source "%~1"
)

:done
if errorlevel 1 pause
