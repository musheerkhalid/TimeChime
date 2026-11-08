# Time Chime - Automated System-Wide Installer
param(
    [switch]$NoDesktopShortcut,
    [switch]$LaunchAfterInstall
)

$ErrorActionPreference = "Stop"

Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host "       Installing Time Chime v1.0.0 (System-Wide)        " -ForegroundColor Cyan
Write-Host "=========================================================" -ForegroundColor Cyan

# Check for Administrator elevation
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "Administrator privileges required to install to Program Files." -ForegroundColor Yellow
    Write-Host "Requesting elevation..." -ForegroundColor Yellow
    $argsList = "-NoProfile -ExecutionPolicy Bypass -File `"$($MyInvocation.MyCommand.Path)`""
    if ($NoDesktopShortcut) { $argsList += " -NoDesktopShortcut" }
    if ($LaunchAfterInstall) { $argsList += " -LaunchAfterInstall" }
    Start-Process powershell.exe -Verb RunAs -ArgumentList $argsList
    exit 0
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourceDir = Join-Path (Split-Path -Parent $scriptDir) "dist\win-x64\TimeChime"
if (-not (Test-Path $sourceDir)) {
    $sourceDir = Join-Path $scriptDir "TimeChime"
}

if (-not (Test-Path $sourceDir)) {
    Write-Error "Source directory not found. Please ensure the release payload is built."
}

$installDir = Join-Path $env:ProgramFiles "Time Chime"
Write-Host "Target Installation Directory: $installDir" -ForegroundColor Gray

# 1. Terminate running instance if updating
$running = Get-Process -Name "TimeChime.App" -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "Stopping running instance of Time Chime..." -ForegroundColor Yellow
    $running | Stop-Process -Force
    Start-Sleep -Seconds 1
}

# 2. Copy application payload
Write-Host "Copying application payload and voice assets..." -ForegroundColor Cyan
if (-not (Test-Path $installDir)) {
    New-Item -ItemType Directory -Path $installDir -Force | Out-Null
}

Copy-Item -Path "$sourceDir\*" -Destination $installDir -Recurse -Force

# 3. Create Shortcuts using WScript.Shell
$wsh = New-Object -ComObject WScript.Shell

# All-Users Start Menu Shortcut
$startMenuDir = [Environment]::GetFolderPath([Environment+SpecialFolder]::CommonPrograms)
if (-not (Test-Path $startMenuDir)) {
    $startMenuDir = [Environment]::GetFolderPath([Environment+SpecialFolder]::Programs)
}
$startMenuLnk = Join-Path $startMenuDir "Time Chime.lnk"
$shortcut = $wsh.CreateShortcut($startMenuLnk)
$shortcut.TargetPath = Join-Path $installDir "TimeChime.App.exe"
$shortcut.WorkingDirectory = $installDir
$shortcut.IconLocation = "$(Join-Path $installDir 'app.ico'), 0"
$shortcut.Description = "Time Chime - Spoken Clock Chime"
$shortcut.Save()
Write-Host "Created Start Menu Shortcut: $startMenuLnk" -ForegroundColor Green

# All-Users Desktop Shortcut
if (-not $NoDesktopShortcut) {
    $desktopDir = [Environment]::GetFolderPath([Environment+SpecialFolder]::CommonDesktopDirectory)
    if (-not (Test-Path $desktopDir)) {
        $desktopDir = [Environment]::GetFolderPath([Environment+SpecialFolder]::Desktop)
    }
    $desktopLnk = Join-Path $desktopDir "Time Chime.lnk"
    $dShortcut = $wsh.CreateShortcut($desktopLnk)
    $dShortcut.TargetPath = Join-Path $installDir "TimeChime.App.exe"
    $dShortcut.WorkingDirectory = $installDir
    $dShortcut.IconLocation = "$(Join-Path $installDir 'app.ico'), 0"
    $dShortcut.Description = "Time Chime - Spoken Clock Chime"
    $dShortcut.Save()
    Write-Host "Created Desktop Shortcut: $desktopLnk" -ForegroundColor Green
}

# 4. Create System Uninstaller Script in Install Dir
$uninstallScript = @"
@echo off
title Uninstall Time Chime
echo ========================================================
echo               Uninstalling Time Chime
echo ========================================================
echo.
net session >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo Administrator privileges required to uninstall from Program Files.
    echo Requesting elevation...
    powershell -Command "Start-Process cmd -ArgumentList '/c \`""%~f0\`""' -Verb RunAs"
    exit /b
)

taskkill /F /IM TimeChime.App.exe >nul 2>&1
reg delete "HKLM\Software\Microsoft\Windows\CurrentVersion\Uninstall\TimeChime" /f >nul 2>&1
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\TimeChime" /f >nul 2>&1
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v TimeChime /f >nul 2>&1
del "%ProgramData%\Microsoft\Windows\Start Menu\Programs\Time Chime.lnk" >nul 2>&1
del "%APPDATA%\Microsoft\Windows\Start Menu\Programs\Time Chime.lnk" >nul 2>&1
del "%Public%\Desktop\Time Chime.lnk" >nul 2>&1
del "%USERPROFILE%\Desktop\Time Chime.lnk" >nul 2>&1
cd /d "%ProgramFiles%"
rmdir /s /q "$installDir" >nul 2>&1
echo.
echo Time Chime successfully uninstalled.
pause
"@
Set-Content -Path (Join-Path $installDir "Uninstall.bat") -Value $uninstallScript -Encoding ASCII

# 5. Register in Windows Add/Remove Programs (Registry - HKLM)
$regPath = "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\TimeChime"
try {
    if (-not (Test-Path $regPath)) {
        New-Item -Path $regPath -Force | Out-Null
    }

    Set-ItemProperty -Path $regPath -Name "DisplayName" -Value "Time Chime"
    Set-ItemProperty -Path $regPath -Name "DisplayVersion" -Value "1.0.0"
    Set-ItemProperty -Path $regPath -Name "Publisher" -Value "Time Chime"
    Set-ItemProperty -Path $regPath -Name "InstallLocation" -Value $installDir
    Set-ItemProperty -Path $regPath -Name "DisplayIcon" -Value (Join-Path $installDir "app.ico")
    Set-ItemProperty -Path $regPath -Name "UninstallString" -Value "cmd.exe /c `"$installDir\Uninstall.bat`""
    Set-ItemProperty -Path $regPath -Name "NoModify" -Value 1 -Type DWord
    Set-ItemProperty -Path $regPath -Name "NoRepair" -Value 1 -Type DWord
} catch {
    Write-Warning "Could not register in HKLM. Trying HKCU..."
    $hkcuPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\TimeChime"
    if (-not (Test-Path $hkcuPath)) { New-Item -Path $hkcuPath -Force | Out-Null }
    Set-ItemProperty -Path $hkcuPath -Name "DisplayName" -Value "Time Chime"
    Set-ItemProperty -Path $hkcuPath -Name "DisplayVersion" -Value "1.0.0"
    Set-ItemProperty -Path $hkcuPath -Name "Publisher" -Value "Time Chime"
    Set-ItemProperty -Path $hkcuPath -Name "InstallLocation" -Value $installDir
    Set-ItemProperty -Path $hkcuPath -Name "DisplayIcon" -Value (Join-Path $installDir "app.ico")
    Set-ItemProperty -Path $hkcuPath -Name "UninstallString" -Value "cmd.exe /c `"$installDir\Uninstall.bat`""
    Set-ItemProperty -Path $hkcuPath -Name "NoModify" -Value 1 -Type DWord
    Set-ItemProperty -Path $hkcuPath -Name "NoRepair" -Value 1 -Type DWord
}

Write-Host "Registered in Windows Installed Apps." -ForegroundColor Green
Write-Host "Time Chime installation completed successfully!" -ForegroundColor Green

Write-Host "Registered in Windows Installed Apps (Add/Remove Programs)." -ForegroundColor Green

Write-Host "`n=========================================================" -ForegroundColor Green
Write-Host "         Time Chime Installation Complete!              " -ForegroundColor Green
Write-Host "=========================================================" -ForegroundColor Green

if ($LaunchAfterInstall) {
    Write-Host "Launching Time Chime..." -ForegroundColor Cyan
    Start-Process -FilePath (Join-Path $installDir "TimeChime.App.exe")
}

