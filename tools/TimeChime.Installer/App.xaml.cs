using System.IO;
using System.Windows;

namespace TimeChime.Installer;

public partial class App : Application
{
    private static readonly string LogPath = Path.Combine(AppContext.BaseDirectory, "installer_trace.log");

    public App()
    {
        DispatcherUnhandledException += (s, e) =>
        {
            try
            {
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "installer_dispatcher_crash.log"), e.Exception.ToString());
                MessageBox.Show(e.Exception.ToString(), "Time Chime Installer Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch { }
        };

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            try
            {
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "installer_domain_crash.log"), e.ExceptionObject?.ToString());
                MessageBox.Show(e.ExceptionObject?.ToString() ?? "Unknown error", "Time Chime Installer Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch { }
        };
    }

    private void OnStartup(object sender, StartupEventArgs e)
    {
        try
        {
            File.WriteAllText(LogPath, $"Installer started. Args: {string.Join(" ", e.Args)}\n");

            bool isSilent = e.Args.Any(a => string.Equals(a, "/S", StringComparison.OrdinalIgnoreCase) ||
                                           string.Equals(a, "--silent", StringComparison.OrdinalIgnoreCase) ||
                                           string.Equals(a, "-q", StringComparison.OrdinalIgnoreCase));

            if (isSilent)
            {
                File.AppendAllText(LogPath, "Executing silent installation...\n");
                try
                {
                    InstallerEngine.InstallSilently();
                    File.AppendAllText(LogPath, "Silent installation completed successfully.\n");
                    Shutdown(0);
                }
                catch (Exception ex)
                {
                    File.AppendAllText(LogPath, $"Silent installation failed: {ex}\n");
                    Shutdown(1);
                }
                return;
            }

            File.AppendAllText(LogPath, "Creating MainWindow...\n");
            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            File.AppendAllText(LogPath, "Showing MainWindow...\n");
            mainWindow.Show();
            File.AppendAllText(LogPath, "MainWindow shown.\n");
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "installer_startup_crash.log"), ex.ToString());
            MessageBox.Show(ex.ToString(), "Time Chime Installer Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
