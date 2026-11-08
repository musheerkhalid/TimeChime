using System.IO;
using System.Windows;
using TimeChime.App.Services;
using TimeChime.App.ViewModels;
using TimeChime.Infrastructure.Persistence;

namespace TimeChime.App.Views;

public partial class MainWindow : Window
{
    private SystemTrayManager? _trayManager;
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        var voiceService = new MockVoiceService();
        var settingsService = new SettingsService();
        _viewModel = new MainViewModel(voiceService, voiceService, settingsService);
        DataContext = _viewModel;

        InitializeTray();

        Closed += (s, e) =>
        {
            _trayManager?.Dispose();
            _trayManager = null;
        };
    }

    public void InitializeTray()
    {
        if (_trayManager != null) return;

        try
        {
            _trayManager = new SystemTrayManager(this, _viewModel);
            _trayManager.Initialize();
        }
        catch (Exception ex)
        {
            AppLogger.Log("tray_error.log", ex.ToString());
        }
    }
}
