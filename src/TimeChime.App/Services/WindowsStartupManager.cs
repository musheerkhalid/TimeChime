using System.IO;
using Microsoft.Win32;

namespace TimeChime.App.Services;

public static class WindowsStartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupApprovedRunPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const string StartupApprovedFolderPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";
    private const string AppName = "TimeChime";
    private const string ShortcutFileName = "Time Chime.lnk";
    private static readonly byte[] ApprovedBytes = [2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];

    public static bool IsStartupEnabled()
    {
        return IsUserStartupEnabled() || IsSystemStartupEnabled();
    }

    public static bool IsUserStartupEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
            if (key?.GetValue(AppName) != null) return true;

            string userStartup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            if (!string.IsNullOrEmpty(userStartup) && File.Exists(Path.Combine(userStartup, ShortcutFileName)))
            {
                return true;
            }
        }
        catch { }
        return false;
    }

    public static bool IsSystemStartupEnabled()
    {
        try
        {
            using var hklmKey = Registry.LocalMachine.OpenSubKey(RunKeyPath, false);
            if (hklmKey?.GetValue(AppName) != null) return true;

            string commonStartup = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);
            if (!string.IsNullOrEmpty(commonStartup))
            {
                string lnk = Path.Combine(commonStartup, ShortcutFileName);
                if (File.Exists(lnk)) return true;
            }
        }
        catch { }
        return false;
    }

    public static string DetectActiveScope()
    {
        return IsSystemStartupEnabled() ? "System" : "User";
    }

    public static void SetStartup(bool enable, string scope = "User")
    {
        if (!enable)
        {
            DisableAllStartup();
            return;
        }

        string? exePath = ResolveBestExecutablePath();
        if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath)) return;

        if (string.Equals(scope, "System", StringComparison.OrdinalIgnoreCase))
        {
            // 1. Try HKLM Run & StartupApproved (works when elevated / installer)
            try
            {
                using var hklmKey = Registry.LocalMachine.OpenSubKey(RunKeyPath, true);
                hklmKey?.SetValue(AppName, $"\"{exePath}\" --minimized");

                using var hklmApproved = Registry.LocalMachine.CreateSubKey(StartupApprovedRunPath, true);
                hklmApproved?.SetValue(AppName, ApprovedBytes, RegistryValueKind.Binary);
            }
            catch { }

            // 2. Try All-Users Common Startup folder & StartupApproved
            try
            {
                string commonStartup = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);
                if (!string.IsNullOrEmpty(commonStartup) && Directory.Exists(commonStartup))
                {
                    string lnk = Path.Combine(commonStartup, ShortcutFileName);
                    CreateShortcut(lnk, exePath, Path.GetDirectoryName(exePath)!, "--minimized", "Time Chime Background Startup");

                    using var hklmFolderApproved = Registry.LocalMachine.CreateSubKey(StartupApprovedFolderPath, true);
                    hklmFolderApproved?.SetValue(ShortcutFileName, ApprovedBytes, RegistryValueKind.Binary);
                }
            }
            catch { }

            // 3. Always also ensure Current User (HKCU) startup & StartupApproved are enabled
            // so startup works 100% reliably even if configured without Admin elevation or if HKLM StartupApproved was restricted.
            EnableUserStartup(exePath);
        }
        else
        {
            // User-only scope: remove system-level entries if possible, then enable user-level
            DisableSystemStartup();
            EnableUserStartup(exePath);
        }
    }

    private static string? ResolveBestExecutablePath()
    {
        return Environment.ProcessPath;
    }

    private static void EnableUserStartup(string exePath)
    {
        bool runKeyWritten = false;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            if (key != null)
            {
                key.SetValue(AppName, $"\"{exePath}\" --minimized");
                runKeyWritten = true;
            }

            using var approvedRun = Registry.CurrentUser.CreateSubKey(StartupApprovedRunPath, true);
            approvedRun?.SetValue(AppName, ApprovedBytes, RegistryValueKind.Binary);

            using var approvedFolder = Registry.CurrentUser.CreateSubKey(StartupApprovedFolderPath, true);
            approvedFolder?.SetValue(ShortcutFileName, ApprovedBytes, RegistryValueKind.Binary);
        }
        catch (Exception ex)
        {
            AppLogger.Log("startup_reg_error.log", ex.ToString());
        }

        try
        {
            string userStartup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            if (!string.IsNullOrEmpty(userStartup) && Directory.Exists(userStartup))
            {
                string lnk = Path.Combine(userStartup, ShortcutFileName);
                if (runKeyWritten)
                {
                    if (File.Exists(lnk)) File.Delete(lnk);
                }
                else
                {
                    CreateShortcut(lnk, exePath, Path.GetDirectoryName(exePath)!, "--minimized", "Time Chime Background Startup");
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.Log("startup_reg_error.log", ex.ToString());
        }
    }

    private static void DisableSystemStartup()
    {
        // Remove HKLM Run
        try
        {
            using var hklmKey = Registry.LocalMachine.OpenSubKey(RunKeyPath, true);
            hklmKey?.DeleteValue(AppName, false);
        }
        catch { }

        // Remove Common Startup Shortcut
        try
        {
            string commonStartup = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);
            if (!string.IsNullOrEmpty(commonStartup))
            {
                string lnk = Path.Combine(commonStartup, ShortcutFileName);
                if (File.Exists(lnk)) File.Delete(lnk);
            }
        }
        catch { }
    }

    private static void DisableAllStartup()
    {
        // Remove HKCU Run
        try
        {
            using var hkcuKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            hkcuKey?.DeleteValue(AppName, false);
        }
        catch { }

        // Remove User Startup Shortcut
        try
        {
            string userStartup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            if (!string.IsNullOrEmpty(userStartup))
            {
                string lnk = Path.Combine(userStartup, ShortcutFileName);
                if (File.Exists(lnk)) File.Delete(lnk);
            }
        }
        catch { }

        DisableSystemStartup();
    }

    private static void CreateShortcut(string shortcutPath, string targetPath, string workingDir, string arguments, string description)
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
            shortcut.Arguments = arguments;
            shortcut.Description = description;
            shortcut.Save();
        }
        catch { }
    }
}
