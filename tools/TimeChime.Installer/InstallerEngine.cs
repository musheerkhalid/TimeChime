using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using Microsoft.Win32;

namespace TimeChime.Installer;

public static class InstallerEngine
{
    public static string DefaultInstallPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Time Chime");

    public static async Task InstallAsync(
        string targetDir,
        bool createDesktopShortcut,
        bool createStartMenuShortcut,
        bool launchAfterInstall,
        IProgress<(double Percent, string Status)> progress,
        CancellationToken token = default)
    {
        await Task.Run(() =>
        {
            progress.Report((5, "Stopping any running instances..."));
            KillRunningApp();

            progress.Report((10, "Preparing system installation directory..."));
            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            var asm = Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream("payload.zip");
            if (stream == null)
            {
                throw new InvalidOperationException("Embedded installation payload not found in setup executable.");
            }

            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            int totalEntries = archive.Entries.Count;
            int processed = 0;

            foreach (var entry in archive.Entries)
            {
                token.ThrowIfCancellationRequested();

                string relativePath = entry.FullName;
                if (relativePath.StartsWith("TimeChime/", StringComparison.OrdinalIgnoreCase))
                {
                    relativePath = relativePath.Substring("TimeChime/".Length);
                }

                if (string.IsNullOrEmpty(relativePath)) continue;

                string destinationPath = Path.Combine(targetDir, relativePath.Replace('/', Path.DirectorySeparatorChar));

                if (string.IsNullOrEmpty(entry.Name))
                {
                    Directory.CreateDirectory(destinationPath);
                }
                else
                {
                    string? parent = Path.GetDirectoryName(destinationPath);
                    if (!string.IsNullOrEmpty(parent) && !Directory.Exists(parent))
                    {
                        Directory.CreateDirectory(parent);
                    }

                    entry.ExtractToFile(destinationPath, overwrite: true);
                }

                processed++;
                if (processed % 25 == 0 || processed == totalEntries)
                {
                    double pct = 10 + ((double)processed / totalEntries) * 75;
                    progress.Report((pct, $"Extracting ({processed}/{totalEntries}): {entry.Name}"));
                }
            }

            progress.Report((88, "Creating system shortcuts..."));
            string exePath = Path.Combine(targetDir, "TimeChime.App.exe");
            string icoPath = Path.Combine(targetDir, "app.ico");

            if (createStartMenuShortcut)
            {
                // All Users (Common) Start Menu Programs folder
                string startMenuDir = Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);
                if (string.IsNullOrEmpty(startMenuDir) || !Directory.Exists(startMenuDir))
                {
                    startMenuDir = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                }
                string lnkPath = Path.Combine(startMenuDir, "Time Chime.lnk");
                CreateShortcut(lnkPath, exePath, targetDir, icoPath, "Time Chime - Spoken Clock Chime");
            }

            if (createDesktopShortcut)
            {
                // Public Desktop folder for all users
                string desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
                if (string.IsNullOrEmpty(desktopDir) || !Directory.Exists(desktopDir))
                {
                    desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                }
                string lnkPath = Path.Combine(desktopDir, "Time Chime.lnk");
                CreateShortcut(lnkPath, exePath, targetDir, icoPath, "Time Chime - Spoken Clock Chime");
            }

            progress.Report((93, "Generating system uninstaller..."));
            CreateUninstallerScript(targetDir);

            progress.Report((96, "Registering in Windows Installed Apps (System-Wide)..."));
            RegisterInAddRemovePrograms(targetDir);

            progress.Report((100, "Installation complete!"));

            if (launchAfterInstall && File.Exists(exePath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = exePath,
                        WorkingDirectory = targetDir,
                        UseShellExecute = true
                    });
                }
                catch { }
            }
        }, token);
    }

    public static void InstallSilently()
    {
        var progress = new Progress<(double, string)>();
        InstallAsync(DefaultInstallPath, createDesktopShortcut: true, createStartMenuShortcut: true, launchAfterInstall: true, progress).GetAwaiter().GetResult();
    }

    private static void KillRunningApp()
    {
        try
        {
            foreach (var p in Process.GetProcessesByName("TimeChime.App"))
            {
                try { p.Kill(); p.WaitForExit(2000); } catch { }
            }
        }
        catch { }
    }

    private static void CreateShortcut(string shortcutPath, string targetPath, string workingDir, string iconPath, string description)
    {
        try
        {
            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return;

            dynamic? shell = Activator.CreateInstance(shellType);
            if (shell == null) return;

            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = targetPath;
            shortcut.WorkingDirectory = workingDir;
            shortcut.Description = description;
            if (File.Exists(iconPath))
            {
                shortcut.IconLocation = $"{iconPath},0";
            }
            shortcut.Save();
        }
        catch { }
    }

    private static void CreateUninstallerScript(string targetDir)
    {
        string script = @"@echo off
title Uninstall Time Chime
echo ========================================================
echo               Uninstalling Time Chime
echo ========================================================
echo.
net session >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo Administrator privileges required to uninstall from Program Files.
    echo Requesting elevation...
    powershell -Command ""Start-Process cmd -ArgumentList '/c \`""%~f0\`""' -Verb RunAs""
    exit /b
)

taskkill /F /IM TimeChime.App.exe >nul 2>&1
reg delete ""HKLM\Software\Microsoft\Windows\CurrentVersion\Uninstall\TimeChime"" /f >nul 2>&1
reg delete ""HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\TimeChime"" /f >nul 2>&1
reg delete ""HKCU\Software\Microsoft\Windows\CurrentVersion\Run"" /v TimeChime /f >nul 2>&1
del ""%ProgramData%\Microsoft\Windows\Start Menu\Programs\Time Chime.lnk"" >nul 2>&1
del ""%APPDATA%\Microsoft\Windows\Start Menu\Programs\Time Chime.lnk"" >nul 2>&1
del ""%Public%\Desktop\Time Chime.lnk"" >nul 2>&1
del ""%USERPROFILE%\Desktop\Time Chime.lnk"" >nul 2>&1
cd /d ""%ProgramFiles%""
rmdir /s /q """ + targetDir + @""" >nul 2>&1
echo.
echo Time Chime successfully uninstalled.
pause
";
        File.WriteAllText(Path.Combine(targetDir, "Uninstall.bat"), script);
    }

    private static void RegisterInAddRemovePrograms(string targetDir)
    {
        // 1. Try HKLM for system-level installation
        try
        {
            using var hklmKey = Registry.LocalMachine.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\TimeChime");
            if (hklmKey != null)
            {
                hklmKey.SetValue("DisplayName", "Time Chime");
                hklmKey.SetValue("DisplayVersion", "1.0.0");
                hklmKey.SetValue("Publisher", "Time Chime");
                hklmKey.SetValue("InstallLocation", targetDir);
                hklmKey.SetValue("DisplayIcon", Path.Combine(targetDir, "app.ico"));
                hklmKey.SetValue("UninstallString", $"cmd.exe /c \"{Path.Combine(targetDir, "Uninstall.bat")}\"");
                hklmKey.SetValue("NoModify", 1, RegistryValueKind.DWord);
                hklmKey.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                return;
            }
        }
        catch { }

        // 2. Fallback to HKCU if HKLM write permissions are restricted
        try
        {
            using var hkcuKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\TimeChime");
            if (hkcuKey != null)
            {
                hkcuKey.SetValue("DisplayName", "Time Chime");
                hkcuKey.SetValue("DisplayVersion", "1.0.0");
                hkcuKey.SetValue("Publisher", "Time Chime");
                hkcuKey.SetValue("InstallLocation", targetDir);
                hkcuKey.SetValue("DisplayIcon", Path.Combine(targetDir, "app.ico"));
                hkcuKey.SetValue("UninstallString", $"cmd.exe /c \"{Path.Combine(targetDir, "Uninstall.bat")}\"");
                hkcuKey.SetValue("NoModify", 1, RegistryValueKind.DWord);
                hkcuKey.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }
        }
        catch { }
    }
}
