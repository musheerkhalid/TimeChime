using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using System.Windows.Threading;
using TimeChime.App.Services;
using TimeChime.Core.Contracts;
using TimeChime.Core.Models;
using TimeChime.Core.Scheduling;
using TimeChime.Core.Voices;
using TimeChime.Infrastructure.Persistence;

namespace TimeChime.App.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly IVoiceCatalog _voiceCatalog;
    private readonly ISpeechSynthesizer _speechSynthesizer;
    private readonly ISettingsService _settingsService;

    private readonly DispatcherTimer _clockTimer;
    private DateTimeOffset? _lastAnnouncedInstant;
    private CancellationTokenSource? _saveCts;
    private bool _isInitializing = true;

    private bool _isEnabled = true;
    private bool _isPaused = false;
    private int _frequencyMinutes = 15;
    private string _startTimeText = "09:00";
    private string _endTimeText = "18:00";
    private bool _isAllDay = false;

    private bool _mon = true;
    private bool _tue = true;
    private bool _wed = true;
    private bool _thu = true;
    private bool _fri = true;
    private bool _sat = false;
    private bool _sun = false;

    private string _searchQuery = string.Empty;
    private string _selectedGenderFilter = "All";
    private string _selectedLocaleFilter = "All";
    private VoiceProfile? _selectedVoice;
    private string? _playingVoiceId;
    private bool _isPreviewPlaying = false;
    private string _previewStatusText = "Ready to preview";

    private int _volumePercent = 80;
    private double _speechRate = 1.0;
    private bool _bellEnabled = false;
    private bool _is12HourFormat = true;
    private bool _launchAtLogin = false;
    private bool _minimizeToTray = true;
    private string _currentTheme = "dark";

    private string _currentTimeDisplay = string.Empty;
    private string _nextChimeDisplay = string.Empty;
    private string _nextChimeSpokenPhrase = string.Empty;
    private string _saveStatusText = "Settings automatically saved to local storage.";

    private List<VoiceProfile> _allVoices = [];
    public ObservableCollection<VoiceProfile> FilteredVoices { get; } = [];

    public MainViewModel(
        IVoiceCatalog voiceCatalog,
        ISpeechSynthesizer speechSynthesizer,
        ISettingsService? settingsService = null)
    {
        _voiceCatalog = voiceCatalog;
        _speechSynthesizer = speechSynthesizer;
        _settingsService = settingsService ?? new SettingsService();

        // Commands
        ToggleMasterCommand = new RelayCommand(ToggleMaster);
        TogglePauseCommand = new RelayCommand(TogglePause);
        TogglePreviewCommand = new RelayCommand(async () => await TogglePreviewAsync());
        SelectVoiceCommand = new RelayCommand(p => { if (p is VoiceProfile v) SelectedVoice = v; });
        PlayVoiceCommand = new RelayCommand(async p => await PlayVoiceAsync(p as VoiceProfile));
        SelectWeekdaysOnlyCommand = new RelayCommand(SelectWeekdaysOnly);
        SelectAllDaysCommand = new RelayCommand(SelectAllDays);
        SetThemeCommand = new RelayCommand(p => { if (p is string t) SetTheme(t); });
        ResetDefaultsCommand = new RelayCommand(ResetDefaults);

        if (_speechSynthesizer is MockVoiceService mockService)
        {
            mockService.PreviewStateChanged += isPlaying =>
            {
                IsPreviewPlaying = isPlaying;
                PreviewStatusText = isPlaying ? "Playing simulated offline speech preview..." : "Preview finished.";
            };
        }

        // Background Clock & Scheduler Timer (Ticks every 1 second)
        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (s, e) => EvaluateScheduler();
        _clockTimer.Start();

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        var voices = await _voiceCatalog.ListAvailableAsync();
        _allVoices = voices.ToList();
        ApplyVoiceFilters();

        var settings = await _settingsService.LoadAsync();
        ApplySettings(settings);

        EvaluateScheduler();
    }

    // Master Properties
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (SetProperty(ref _isEnabled, value))
            {
                OnPropertyChanged(nameof(StatusBadgeText));
                EvaluateScheduler();
                TriggerAutoSave();
            }
        }
    }

    public bool IsPaused
    {
        get => _isPaused;
        set
        {
            if (SetProperty(ref _isPaused, value))
            {
                OnPropertyChanged(nameof(StatusBadgeText));
                OnPropertyChanged(nameof(PauseButtonText));
                EvaluateScheduler();
                TriggerAutoSave();
            }
        }
    }

    public string StatusBadgeText
    {
        get
        {
            if (!_isEnabled) return "DISABLED";
            if (_isPaused) return "PAUSED";
            return "ACTIVE";
        }
    }

    public string PauseButtonText => _isPaused ? "Resume" : "Pause";

    // Frequency Segmented Radios
    public int FrequencyMinutes
    {
        get => _frequencyMinutes;
        set
        {
            if (SetProperty(ref _frequencyMinutes, value))
            {
                OnPropertyChanged(nameof(IsQuarterHourly));
                OnPropertyChanged(nameof(IsHalfHourly));
                OnPropertyChanged(nameof(IsHourly));
                EvaluateScheduler();
                TriggerAutoSave();
            }
        }
    }

    public bool IsQuarterHourly
    {
        get => _frequencyMinutes == 15;
        set { if (value) FrequencyMinutes = 15; }
    }

    public bool IsHalfHourly
    {
        get => _frequencyMinutes == 30;
        set { if (value) FrequencyMinutes = 30; }
    }

    public bool IsHourly
    {
        get => _frequencyMinutes == 60;
        set { if (value) FrequencyMinutes = 60; }
    }

    // Daily Active Window
    public string StartTimeText
    {
        get => _startTimeText;
        set
        {
            if (SetProperty(ref _startTimeText, value))
            {
                EvaluateScheduler();
                TriggerAutoSave();
            }
        }
    }

    public string EndTimeText
    {
        get => _endTimeText;
        set
        {
            if (SetProperty(ref _endTimeText, value))
            {
                EvaluateScheduler();
                TriggerAutoSave();
            }
        }
    }

    public bool IsAllDay
    {
        get => _isAllDay;
        set
        {
            if (SetProperty(ref _isAllDay, value))
            {
                EvaluateScheduler();
                TriggerAutoSave();
            }
        }
    }

    // Weekday Chips
    public bool MondayEnabled
    {
        get => _mon;
        set { if (SetProperty(ref _mon, value)) { OnPropertyChanged(nameof(IsWeekendOff)); EvaluateScheduler(); TriggerAutoSave(); } }
    }

    public bool TuesdayEnabled
    {
        get => _tue;
        set { if (SetProperty(ref _tue, value)) { OnPropertyChanged(nameof(IsWeekendOff)); EvaluateScheduler(); TriggerAutoSave(); } }
    }

    public bool WednesdayEnabled
    {
        get => _wed;
        set { if (SetProperty(ref _wed, value)) { OnPropertyChanged(nameof(IsWeekendOff)); EvaluateScheduler(); TriggerAutoSave(); } }
    }

    public bool ThursdayEnabled
    {
        get => _thu;
        set { if (SetProperty(ref _thu, value)) { OnPropertyChanged(nameof(IsWeekendOff)); EvaluateScheduler(); TriggerAutoSave(); } }
    }

    public bool FridayEnabled
    {
        get => _fri;
        set { if (SetProperty(ref _fri, value)) { OnPropertyChanged(nameof(IsWeekendOff)); EvaluateScheduler(); TriggerAutoSave(); } }
    }

    public bool SaturdayEnabled
    {
        get => _sat;
        set { if (SetProperty(ref _sat, value)) { OnPropertyChanged(nameof(IsWeekendOff)); EvaluateScheduler(); TriggerAutoSave(); } }
    }

    public bool SundayEnabled
    {
        get => _sun;
        set { if (SetProperty(ref _sun, value)) { OnPropertyChanged(nameof(IsWeekendOff)); EvaluateScheduler(); TriggerAutoSave(); } }
    }

    public bool IsWeekendOff
    {
        get => !_sat && !_sun;
        set
        {
            if (value)
            {
                SaturdayEnabled = false;
                SundayEnabled = false;
            }
            else
            {
                SaturdayEnabled = true;
                SundayEnabled = true;
            }
            OnPropertyChanged(nameof(IsWeekendOff));
        }
    }

    // Voice Library Filters & Selection
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                ApplyVoiceFilters();
            }
        }
    }

    public string SelectedGenderFilter
    {
        get => _selectedGenderFilter;
        set
        {
            if (SetProperty(ref _selectedGenderFilter, value))
            {
                ApplyVoiceFilters();
            }
        }
    }

    public string SelectedLocaleFilter
    {
        get => _selectedLocaleFilter;
        set
        {
            if (SetProperty(ref _selectedLocaleFilter, value))
            {
                ApplyVoiceFilters();
            }
        }
    }

    public VoiceProfile? SelectedVoice
    {
        get => _selectedVoice;
        set
        {
            if (SetProperty(ref _selectedVoice, value))
            {
                TriggerAutoSave();
            }
        }
    }

    public string? PlayingVoiceId
    {
        get => _playingVoiceId;
        set
        {
            if (SetProperty(ref _playingVoiceId, value))
            {
                OnPropertyChanged(nameof(IsPreviewPlaying));
                OnPropertyChanged(nameof(PreviewButtonText));
            }
        }
    }

    public bool IsPreviewPlaying
    {
        get => _isPreviewPlaying;
        private set
        {
            if (SetProperty(ref _isPreviewPlaying, value))
            {
                if (!value) PlayingVoiceId = null;
                OnPropertyChanged(nameof(PreviewButtonText));
            }
        }
    }

    public string PreviewButtonText => _isPreviewPlaying ? "Stop Preview" : "Play Preview";
    public string PreviewStatusText
    {
        get => _previewStatusText;
        private set => SetProperty(ref _previewStatusText, value);
    }

    // Audio & Behavior
    public int VolumePercent
    {
        get => _volumePercent;
        set
        {
            if (SetProperty(ref _volumePercent, value))
            {
                TriggerAutoSave();
            }
        }
    }

    public double SpeechRate
    {
        get => _speechRate;
        set
        {
            if (SetProperty(ref _speechRate, Math.Round(value, 1)))
            {
                OnPropertyChanged(nameof(SpeechRateDisplay));
                TriggerAutoSave();
            }
        }
    }

    public string SpeechRateDisplay => $"{_speechRate:0.0}x";

    public bool BellEnabled
    {
        get => _bellEnabled;
        set
        {
            if (SetProperty(ref _bellEnabled, value))
            {
                TriggerAutoSave();
            }
        }
    }

    public bool Is12HourFormat
    {
        get => _is12HourFormat;
        set
        {
            if (SetProperty(ref _is12HourFormat, value))
            {
                EvaluateScheduler();
                TriggerAutoSave();
            }
        }
    }

    private string _startupScope = "User";

    public bool LaunchAtLogin
    {
        get => _launchAtLogin;
        set
        {
            if (SetProperty(ref _launchAtLogin, value))
            {
                TimeChime.App.Services.WindowsStartupManager.SetStartup(value, StartupScope);
                TriggerAutoSave();
            }
        }
    }

    public string StartupScope
    {
        get => _startupScope;
        set
        {
            if (SetProperty(ref _startupScope, value))
            {
                OnPropertyChanged(nameof(IsUserStartupScope));
                OnPropertyChanged(nameof(IsSystemStartupScope));
                if (LaunchAtLogin)
                {
                    TimeChime.App.Services.WindowsStartupManager.SetStartup(true, _startupScope);
                }
                TriggerAutoSave();
            }
        }
    }

    public bool IsUserStartupScope
    {
        get => string.Equals(_startupScope, "User", StringComparison.OrdinalIgnoreCase);
        set
        {
            if (value) StartupScope = "User";
        }
    }

    public bool IsSystemStartupScope
    {
        get => string.Equals(_startupScope, "System", StringComparison.OrdinalIgnoreCase);
        set
        {
            if (value) StartupScope = "System";
        }
    }

    public bool MinimizeToTray
    {
        get => _minimizeToTray;
        set
        {
            if (SetProperty(ref _minimizeToTray, value))
            {
                TriggerAutoSave();
            }
        }
    }

    public string CurrentTheme
    {
        get => _currentTheme;
        set
        {
            if (SetProperty(ref _currentTheme, value))
            {
                TriggerAutoSave();
            }
        }
    }

    // Schedule Predictions
    public string CurrentTimeDisplay
    {
        get => _currentTimeDisplay;
        private set => SetProperty(ref _currentTimeDisplay, value);
    }

    public string NextChimeDisplay
    {
        get => _nextChimeDisplay;
        private set => SetProperty(ref _nextChimeDisplay, value);
    }

    public string NextChimeSpokenPhrase
    {
        get => _nextChimeSpokenPhrase;
        private set => SetProperty(ref _nextChimeSpokenPhrase, value);
    }

    public string SaveStatusText
    {
        get => _saveStatusText;
        private set => SetProperty(ref _saveStatusText, value);
    }

    // Commands
    public ICommand ToggleMasterCommand { get; }
    public ICommand TogglePauseCommand { get; }
    public ICommand TogglePreviewCommand { get; }
    public ICommand SelectVoiceCommand { get; }
    public ICommand PlayVoiceCommand { get; }
    public ICommand SelectWeekdaysOnlyCommand { get; }
    public ICommand SelectAllDaysCommand { get; }
    public ICommand SetThemeCommand { get; }
    public ICommand ResetDefaultsCommand { get; }

    private void ToggleMaster()
    {
        IsEnabled = !IsEnabled;
    }

    private void TogglePause()
    {
        IsPaused = !IsPaused;
    }

    public async Task PlayVoiceAsync(VoiceProfile? voice)
    {
        if (voice == null) return;

        if (PlayingVoiceId == voice.Id && IsPreviewPlaying)
        {
            _speechSynthesizer.StopPreview();
            PlayingVoiceId = null;
            return;
        }

        SelectedVoice = voice;
        PlayingVoiceId = voice.Id;
        string lang = voice.Locale.StartsWith("hi", StringComparison.OrdinalIgnoreCase) ? "hi" : "en";
        string text = !string.IsNullOrEmpty(voice.SampleText) 
            ? voice.SampleText 
            : AnnouncementFormatter.Format(DateTimeOffset.Now, Is12HourFormat ? "12h" : "24h", lang);
        await _speechSynthesizer.SynthesizeAndPlayPreviewAsync(voice.Id, text, SpeechRate, VolumePercent, BellEnabled);
        PlayingVoiceId = null;
    }

    private async Task TogglePreviewAsync()
    {
        if (SelectedVoice == null) return;

        if (IsPreviewPlaying)
        {
            _speechSynthesizer.StopPreview();
            return;
        }

        string lang = SelectedVoice.Locale.StartsWith("hi", StringComparison.OrdinalIgnoreCase) ? "hi" : "en";
        string text = !string.IsNullOrEmpty(SelectedVoice.SampleText)
            ? SelectedVoice.SampleText
            : AnnouncementFormatter.Format(DateTimeOffset.Now, Is12HourFormat ? "12h" : "24h", lang);
        await _speechSynthesizer.SynthesizeAndPlayPreviewAsync(SelectedVoice.Id, text, SpeechRate, VolumePercent, BellEnabled);
    }

    private void SelectWeekdaysOnly()
    {
        MondayEnabled = true;
        TuesdayEnabled = true;
        WednesdayEnabled = true;
        ThursdayEnabled = true;
        FridayEnabled = true;
        SaturdayEnabled = false;
        SundayEnabled = false;
        OnPropertyChanged(nameof(IsWeekendOff));
    }

    private void SelectAllDays()
    {
        MondayEnabled = true;
        TuesdayEnabled = true;
        WednesdayEnabled = true;
        ThursdayEnabled = true;
        FridayEnabled = true;
        SaturdayEnabled = true;
        SundayEnabled = true;
        OnPropertyChanged(nameof(IsWeekendOff));
    }

    private void SetTheme(string theme)
    {
        CurrentTheme = theme;
        ThemeManager.ApplyTheme(theme);
    }

    private void ResetDefaults()
    {
        _ = _settingsService.ResetToDefaultsAsync();
        ApplySettings(new TimeChimeSettings());
        SaveStatusText = "Defaults restored.";
        EvaluateScheduler();
    }

    private void ApplyVoiceFilters()
    {
        FilteredVoices.Clear();
        foreach (var v in _allVoices)
        {
            if (!string.IsNullOrWhiteSpace(_searchQuery) &&
                !v.DisplayName.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase) &&
                !v.Locale.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (_selectedGenderFilter != "All" && !string.Equals(v.Gender, _selectedGenderFilter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (_selectedLocaleFilter != "All" && !v.Locale.Contains(_selectedLocaleFilter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            FilteredVoices.Add(v);
        }
    }

    public HashSet<DayOfWeek> GetActiveDaysSet()
    {
        var set = new HashSet<DayOfWeek>();
        if (_mon) set.Add(DayOfWeek.Monday);
        if (_tue) set.Add(DayOfWeek.Tuesday);
        if (_wed) set.Add(DayOfWeek.Wednesday);
        if (_thu) set.Add(DayOfWeek.Thursday);
        if (_fri) set.Add(DayOfWeek.Friday);
        if (_sat) set.Add(DayOfWeek.Saturday);
        if (_sun) set.Add(DayOfWeek.Sunday);
        return set;
    }

    private void EvaluateScheduler()
    {
        var now = DateTimeOffset.Now;
        CurrentTimeDisplay = now.ToString(Is12HourFormat ? "hh:mm:ss tt" : "HH:mm:ss");

        var activeDays = GetActiveDaysSet();
        TimeOnly.TryParse(StartTimeText, out var start);
        TimeOnly.TryParse(EndTimeText, out var end);

        var eval = ScheduleCalculator.Evaluate(
            now,
            IsEnabled,
            IsPaused,
            FrequencyMinutes,
            start,
            end,
            IsAllDay,
            activeDays,
            _lastAnnouncedInstant);

        if (!IsEnabled)
        {
            NextChimeDisplay = "Announcements are disabled";
            NextChimeSpokenPhrase = "(Master switch is off)";
        }
        else if (IsPaused)
        {
            NextChimeDisplay = "Announcements are paused";
            NextChimeSpokenPhrase = "(Paused by user)";
        }
        else if (eval.NextOccurrence.HasValue)
        {
            var target = eval.NextOccurrence.Value;
            var diff = target - now;
            int remainingMin = Math.Max(1, (int)Math.Ceiling(diff.TotalMinutes));
            string timeStr = target.ToString(Is12HourFormat ? "hh:mm tt" : "HH:mm");
            NextChimeDisplay = $"{timeStr} (in {remainingMin} min)";
            string phrase = AnnouncementFormatter.Format(target, Is12HourFormat ? "12h" : "24h");
            NextChimeSpokenPhrase = $"\"{phrase}\"";
        }
        else
        {
            NextChimeDisplay = "Outside active window";
            NextChimeSpokenPhrase = "(No upcoming chime scheduled)";
        }

        if (eval.ShouldTriggerNow)
        {
            _lastAnnouncedInstant = now;
            _ = PlayScheduledChimeAsync();
        }
    }

    private async Task PlayScheduledChimeAsync()
    {
        if (SelectedVoice == null || _speechSynthesizer.IsPreviewPlaying) return;

        try
        {
            string lang = SelectedVoice.Locale.StartsWith("hi", StringComparison.OrdinalIgnoreCase) ? "hi" : "en";
            string phrase = AnnouncementFormatter.Format(DateTimeOffset.Now, Is12HourFormat ? "12h" : "24h", lang);
            await _speechSynthesizer.SynthesizeAndPlayPreviewAsync(
                SelectedVoice.Id,
                phrase,
                SpeechRate,
                VolumePercent,
                BellEnabled);
        }
        catch (Exception ex)
        {
            File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "schedule_error.log"), ex.ToString());
        }
    }

    private void TriggerAutoSave()
    {
        if (_isInitializing) return;

        _saveCts?.Cancel();
        _saveCts = new CancellationTokenSource();
        var token = _saveCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(350, token); // 350ms debounce
                var settings = ToSettings();
                await _settingsService.SaveAsync(settings, token);

                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    SaveStatusText = $"Settings saved ({DateTime.Now:HH:mm:ss})";
                });
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    SaveStatusText = "Failed to save settings.";
                });
                File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "save_error.log"), ex.ToString());
            }
        }, token);
    }

    private TimeChimeSettings ToSettings()
    {
        var days = new List<DayOfWeek>();
        if (_mon) days.Add(DayOfWeek.Monday);
        if (_tue) days.Add(DayOfWeek.Tuesday);
        if (_wed) days.Add(DayOfWeek.Wednesday);
        if (_thu) days.Add(DayOfWeek.Thursday);
        if (_fri) days.Add(DayOfWeek.Friday);
        if (_sat) days.Add(DayOfWeek.Saturday);
        if (_sun) days.Add(DayOfWeek.Sunday);

        TimeOnly.TryParse(StartTimeText, out var start);
        TimeOnly.TryParse(EndTimeText, out var end);

        return new TimeChimeSettings
        {
            Enabled = IsEnabled,
            Paused = IsPaused,
            FrequencyMinutes = FrequencyMinutes,
            ActiveWindow = new ActiveWindowRule
            {
                Start = start,
                End = end,
                AllDay = IsAllDay
            },
            ActiveDays = days,
            SelectedVoiceId = SelectedVoice?.Id ?? "af_heart",
            VoiceVolumePercent = VolumePercent,
            SpeechRate = SpeechRate,
            BellEnabled = BellEnabled,
            TimeFormat = Is12HourFormat ? "12h" : "24h",
            LaunchAtLogin = LaunchAtLogin,
            StartupScope = StartupScope,
            MinimizeToTray = MinimizeToTray,
            Theme = CurrentTheme
        };
    }

    private void ApplySettings(TimeChimeSettings s)
    {
        _isInitializing = true;
        try
        {
            _isEnabled = s.Enabled;
            _isPaused = s.Paused;
            _frequencyMinutes = s.FrequencyMinutes;
            _startTimeText = s.ActiveWindow.Start.ToString("HH:mm");
            _endTimeText = s.ActiveWindow.End.ToString("HH:mm");
            _isAllDay = s.ActiveWindow.AllDay;

            var days = s.ActiveDays.ToHashSet();
            _mon = days.Contains(DayOfWeek.Monday);
            _tue = days.Contains(DayOfWeek.Tuesday);
            _wed = days.Contains(DayOfWeek.Wednesday);
            _thu = days.Contains(DayOfWeek.Thursday);
            _fri = days.Contains(DayOfWeek.Friday);
            _sat = days.Contains(DayOfWeek.Saturday);
            _sun = days.Contains(DayOfWeek.Sunday);

            _volumePercent = s.VoiceVolumePercent;
            _speechRate = s.SpeechRate;
            _bellEnabled = s.BellEnabled;
            _is12HourFormat = string.Equals(s.TimeFormat, "12h", StringComparison.OrdinalIgnoreCase);
            _launchAtLogin = s.LaunchAtLogin;
            _startupScope = !string.IsNullOrEmpty(s.StartupScope) ? s.StartupScope : "User";
            if (_launchAtLogin)
            {
                TimeChime.App.Services.WindowsStartupManager.SetStartup(true, _startupScope);
            }
            _minimizeToTray = s.MinimizeToTray;
            _currentTheme = s.Theme;

            if (!string.IsNullOrEmpty(s.SelectedVoiceId))
            {
                var voice = _allVoices.FirstOrDefault(v => v.Id == s.SelectedVoiceId);
                if (voice != null)
                {
                    _selectedVoice = voice;
                }
            }

            if (_selectedVoice == null && _allVoices.Count > 0)
            {
                _selectedVoice = _allVoices[0];
            }

            if (!string.IsNullOrEmpty(_currentTheme))
            {
                ThemeManager.ApplyTheme(_currentTheme);
            }

            OnPropertyChanged(string.Empty);
        }
        finally
        {
            _isInitializing = false;
        }
    }
}
