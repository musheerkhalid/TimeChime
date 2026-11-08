using System.IO;
using System.Windows;

namespace TimeChime.Installer;

public partial class App : Application
{
    private static readonly string LogDir = Path.Combine(Path.GetTempPath(), "TimeChime_Installer");

    public App()
    {
        DispatcherUnhandledException += (s, e) =>
        {
            try
            {
                WriteLog("installer_dispatcher_crash.log", e.Exception.ToString(), overwrite: true);
                MessageBox.Show(e.Exception.ToString(), "Time Chime Installer Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch { }
        };

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            try
            {
                WriteLog("installer_domain_crash.log", e.ExceptionObject?.ToString() ?? "Unknown error", overwrite: true);
                MessageBox.Show(e.ExceptionObject?.ToString() ?? "Unknown error", "Time Chime Installer Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch { }
        };
    }

    private void OnStartup(object sender, StartupEventArgs e)
    {
        try
        {
            WriteLog("installer_trace.log", $"Installer started. Args: {string.Join(" ", e.Args)}\n", overwrite: true);

            bool isSilent = e.Args.Any(a => string.Equals(a, "/S", StringComparison.OrdinalIgnoreCase) ||
                                           string.Equals(a, "--silent", StringComparison.OrdinalIgnoreCase) ||
                                           string.Equals(a, "-q", StringComparison.OrdinalIgnoreCase));

            if (isSilent)
            {
                WriteLog("installer_trace.log", "Executing silent installation...\n");
                try
                {
                    InstallerEngine.InstallSilently();
                    WriteLog("installer_trace.log", "Silent installation completed successfully.\n");
                    Shutdown(0);
                }
                catch (Exception ex)
                {
                    WriteLog("installer_trace.log", $"Silent installation failed: {ex}\n");
                    Shutdown(1);
                }
                return;
            }

            WriteLog("installer_trace.log", "Creating MainWindow...\n");
            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            WriteLog("installer_trace.log", "Showing MainWindow...\n");
            mainWindow.Show();
            WriteLog("installer_trace.log", "MainWindow shown.\n");
        }
        catch (Exception ex)
        {
            WriteLog("installer_startup_crash.log", ex.ToString(), overwrite: true);
            MessageBox.Show(ex.ToString(), "Time Chime Installer Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static void WriteLog(string fileName, string content, bool overwrite = false)
    {
        try
        {
            if (!Directory.Exists(LogDir))
            {
                Directory.CreateDirectory(LogDir);
            }

            string path = Path.Combine(LogDir, fileName);
            if (overwrite)
            {
                File.WriteAllText(path, content);
            }
            else
            {
                File.AppendAllText(path, content);
            }
        }
        catch
        {
            // Never let log I/O break installer execution
        }
    }
}
