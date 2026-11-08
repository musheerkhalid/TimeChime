@echo off
title Uninstall Time Chime
cd /d "%~dp0"
echo ========================================================
echo               Time Chime Uninstaller
echo ========================================================
echo.
if exist "%ProgramFiles%\Time Chime\Uninstall.bat" (
    call "%ProgramFiles%\Time Chime\Uninstall.bat"
) else if exist "%LOCALAPPDATA%\Programs\TimeChime\Uninstall.bat" (
    call "%LOCALAPPDATA%\Programs\TimeChime\Uninstall.bat"
) else (
    echo Time Chime installation not found. Performing full cleanup...
    taskkill /F /IM TimeChime.App.exe >nul 2>&1
    reg delete "HKLM\Software\Microsoft\Windows\CurrentVersion\Uninstall\TimeChime" /f >nul 2>&1
    reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\TimeChime" /f >nul 2>&1
    reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v TimeChime /f >nul 2>&1
    del "%ProgramData%\Microsoft\Windows\Start Menu\Programs\Time Chime.lnk" >nul 2>&1
    del "%APPDATA%\Microsoft\Windows\Start Menu\Programs\Time Chime.lnk" >nul 2>&1
    del "%Public%\Desktop\Time Chime.lnk" >nul 2>&1
    del "%USERPROFILE%\Desktop\Time Chime.lnk" >nul 2>&1
    echo Uninstallation cleanup finished.
    pause
)

