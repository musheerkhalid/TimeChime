using System.IO;
using Microsoft.Win32;

namespace TimeChime.App.Services;

public static class WindowsStartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "TimeChime";

    public static bool IsStartupEnabled()
    {
        return IsUserStartupEnabled() || IsSystemStartupEnabled();
    }

    public static bool IsUserStartupEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
            return key?.GetValue(AppName) != null;
        }
        catch { return false; }
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
                string lnk = Path.Combine(commonStartup, "Time Chime.lnk");
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
        // 1. Remove any existing registrations from all scopes first
        DisableAllStartup();

        if (!enable) return;

        string? exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath)) return;

        if (string.Equals(scope, "System", StringComparison.OrdinalIgnoreCase))
        {
            bool systemConfigured = false;

            // Try HKLM
            try
            {
                using var hklmKey = Registry.LocalMachine.OpenSubKey(RunKeyPath, true);
                if (hklmKey != null)
                {
                    hklmKey.SetValue(AppName, $"\"{exePath}\" --minimized");
                    systemConfigured = true;
                }
            }
            catch { }

            // Also place in Common Startup folder (All Users startup on login)
            try
            {
                string commonStartup = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);
                if (!string.IsNullOrEmpty(commonStartup) && Directory.Exists(commonStartup))
                {
                    string lnk = Path.Combine(commonStartup, "Time Chime.lnk");
                    CreateShortcut(lnk, exePath, Path.GetDirectoryName(exePath)!, "--minimized", "Time Chime Background Startup");
                    systemConfigured = true;
                }
            }
            catch { }

            // If system level permissions failed, fallback to Current User
            if (!systemConfigured)
            {
                EnableUserStartup(exePath);
            }
        }
        else
        {
            EnableUserStartup(exePath);
        }
    }

    private static void EnableUserStartup(string exePath)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            key?.SetValue(AppName, $"\"{exePath}\" --minimized");
        }
        catch (Exception ex)
        {
            try
            {
                File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "startup_reg_error.log"), ex.ToString());
            }
            catch { }
        }
    }

    private static void DisableAllStartup()
    {
        // Remove HKCU
        try
        {
            using var hkcuKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            hkcuKey?.DeleteValue(AppName, false);
        }
        catch { }

        // Remove HKLM
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
                string lnk = Path.Combine(commonStartup, "Time Chime.lnk");
                if (File.Exists(lnk)) File.Delete(lnk);
            }
        }
        catch { }
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
