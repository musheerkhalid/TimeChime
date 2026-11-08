using System.Windows;

namespace TimeChime.Installer;

public partial class MainWindow : Window
{
    private bool _isCompleted = false;

    public MainWindow()
    {
        InitializeComponent();
        TxtInstallPath.Text = InstallerEngine.DefaultInstallPath;
    }

    private async void OnInstallClick(object sender, RoutedEventArgs e)
    {
        if (_isCompleted)
        {
            Close();
            return;
        }

        BtnInstall.IsEnabled = false;
        BtnCancel.IsEnabled = false;
        PnlOptions.Visibility = Visibility.Collapsed;
        PnlProgress.Visibility = Visibility.Visible;

        var progress = new Progress<(double Percent, string Status)>(report =>
        {
            PbInstall.Value = report.Percent;
            TxtStatus.Text = report.Status;
        });

        try
        {
            await InstallerEngine.InstallAsync(
                targetDir: InstallerEngine.DefaultInstallPath,
                createDesktopShortcut: ChkDesktop.IsChecked == true,
                createStartMenuShortcut: ChkStartMenu.IsChecked == true,
                launchAfterInstall: ChkLaunch.IsChecked == true,
                progress: progress
            );

            _isCompleted = true;
            TxtStatus.Text = "Time Chime has been successfully installed!";
            TxtStatus.Foreground = System.Windows.Media.Brushes.LightGreen;
            BtnInstall.Content = "Finish";
            BtnInstall.IsEnabled = true;
            BtnCancel.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            TxtStatus.Text = $"Installation failed: {ex.Message}";
            TxtStatus.Foreground = System.Windows.Media.Brushes.IndianRed;
            BtnCancel.IsEnabled = true;
            BtnInstall.IsEnabled = true;
            BtnInstall.Content = "Retry";
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

