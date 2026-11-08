using System.Text.Json;
using TimeChime.Core.Contracts;
using TimeChime.Core.Models;

namespace TimeChime.Infrastructure.Persistence;

public class SettingsService : ISettingsService
{
    private readonly string _settingsFilePath;
    private readonly string _backupFilePath;
    private readonly string _tempFilePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private TimeChimeSettings _currentSettings = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public SettingsService(string? customSettingsPath = null)
    {
        if (string.IsNullOrWhiteSpace(customSettingsPath))
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appDir = Path.Combine(appData, "TimeChime");
            _settingsFilePath = Path.Combine(appDir, "settings.json");
        }
        else
        {
            _settingsFilePath = Path.GetFullPath(customSettingsPath);
        }

        string? dir = Path.GetDirectoryName(_settingsFilePath);
        string fileName = Path.GetFileName(_settingsFilePath);
        _backupFilePath = Path.Combine(dir ?? string.Empty, fileName + ".bak");
        _tempFilePath = Path.Combine(dir ?? string.Empty, fileName + ".tmp");
    }

    public TimeChimeSettings CurrentSettings => _currentSettings;

    public event Action<TimeChimeSettings>? SettingsChanged;

    public async Task<TimeChimeSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_settingsFilePath))
            {
                _currentSettings = new TimeChimeSettings();
                return _currentSettings;
            }

            try
            {
                string json = await File.ReadAllTextAsync(_settingsFilePath, cancellationToken);
                if (string.IsNullOrWhiteSpace(json))
                {
                    throw new JsonException("Settings file is empty.");
                }

                var loaded = JsonSerializer.Deserialize<TimeChimeSettings>(json, JsonOptions);
                if (loaded == null)
                {
                    throw new JsonException("Deserialized settings object is null.");
                }

                _currentSettings = loaded;
                return _currentSettings;
            }
            catch (Exception)
            {
                // Attempt restore from backup
                if (File.Exists(_backupFilePath))
                {
                    try
                    {
                        string backupJson = await File.ReadAllTextAsync(_backupFilePath, cancellationToken);
                        var backupLoaded = JsonSerializer.Deserialize<TimeChimeSettings>(backupJson, JsonOptions);
                        if (backupLoaded != null)
                        {
                            _currentSettings = backupLoaded;
                            return _currentSettings;
                        }
                    }
                    catch
                    {
                        // Backup also corrupted
                    }
                }

                // Preserve corrupted file for user forensics
                string corruptPath = _settingsFilePath + $".corrupt.{DateTime.UtcNow:yyyyMMddHHmmss}";
                try
                {
                    File.Copy(_settingsFilePath, corruptPath, overwrite: true);
                }
                catch
                {
                    // Ignore copy error during fallback
                }

                _currentSettings = new TimeChimeSettings();
                return _currentSettings;
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAsync(TimeChimeSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            string? directory = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 1. Serialize to JSON
            string json = JsonSerializer.Serialize(settings, JsonOptions);

            // 2. Write to temporary file with flush
            await using (var fs = new FileStream(_tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            await using (var writer = new StreamWriter(fs))
            {
                await writer.WriteAsync(json.AsMemory(), cancellationToken);
                await writer.FlushAsync(cancellationToken);
                await fs.FlushAsync(cancellationToken);
            }

            // 3. Create or update backup if primary settings file already exists
            if (File.Exists(_settingsFilePath))
            {
                try
                {
                    File.Copy(_settingsFilePath, _backupFilePath, overwrite: true);
                }
                catch
                {
                    // Best-effort backup
                }
            }

            // 4. Atomic file replace
            File.Move(_tempFilePath, _settingsFilePath, overwrite: true);

            _currentSettings = settings;
            SettingsChanged?.Invoke(settings);
        }
        finally
        {
            if (File.Exists(_tempFilePath))
            {
                try { File.Delete(_tempFilePath); } catch { }
            }
            _lock.Release();
        }
    }

    public async Task ResetToDefaultsAsync(CancellationToken cancellationToken = default)
    {
        var defaults = new TimeChimeSettings();
        await SaveAsync(defaults, cancellationToken);
    }
}
