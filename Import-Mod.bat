@echo off
setlocal
title DELTARUNE Mod Importer ^& Update Manager
cd /d "%~dp0"

if "%~1"=="" (
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Import-Mod.ps1"
) else (
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Import-Mod.ps1" -Source "%~1"
)

if errorlevel 1 pause
