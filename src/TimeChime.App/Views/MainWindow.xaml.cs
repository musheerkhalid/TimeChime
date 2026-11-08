using System.IO;
using System.Windows;
using TimeChime.App.Services;
using TimeChime.App.ViewModels;
using TimeChime.Infrastructure.Persistence;

namespace TimeChime.App.Views;

public partial class MainWindow : Window
{
    private SystemTrayManager? _trayManager;

    public MainWindow()
    {
        InitializeComponent();

        var voiceService = new MockVoiceService();
        var settingsService = new SettingsService();
        var viewModel = new MainViewModel(voiceService, voiceService, settingsService);
        DataContext = viewModel;

        Loaded += (s, e) =>
        {
            try
            {
                _trayManager = new SystemTrayManager(this, viewModel);
                _trayManager.Initialize();
            }
            catch (Exception ex)
            {
                File.AppendAllText("tray_error.log", ex.ToString());
            }
        };

        Closed += (s, e) =>
        {
            _trayManager?.Dispose();
            _trayManager = null;
        };
    }
}
