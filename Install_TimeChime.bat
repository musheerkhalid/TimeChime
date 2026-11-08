@echo off
title Install Time Chime
cd /d "%~dp0"
echo ========================================================
echo               Time Chime Installer
echo ========================================================
echo.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0installer\Install-TimeChime.ps1"
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Installation encountered an error.
    pause
    exit /b %ERRORLEVEL%
)
echo.
echo Press any key to exit installer...
pause >nul

