using System.Text.Json;
using TimeChime.Core.Models;
using TimeChime.Infrastructure.Persistence;
using Xunit;

namespace TimeChime.Core.Tests;

public class SettingsServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _settingsPath;

    public SettingsServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "TimeChimeTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
        _settingsPath = Path.Combine(_testDir, "settings.json");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    [Fact]
    public async Task LoadAsync_MissingFile_ReturnsDefaultSettings()
    {
        var service = new SettingsService(_settingsPath);
        var settings = await service.LoadAsync();

        Assert.NotNull(settings);
        Assert.True(settings.Enabled);
        Assert.Equal(15, settings.FrequencyMinutes);
        Assert.Equal("12h", settings.TimeFormat);
    }

    [Fact]
    public async Task SaveAsync_PersistsSettingsAndCreatesBackupOnSubsequentSave()
    {
        var service = new SettingsService(_settingsPath);

        var customSettings = new TimeChimeSettings
        {
            FrequencyMinutes = 30,
            TimeFormat = "24h",
            SelectedVoiceId = "mock:en_GB-oliver",
            VoiceVolumePercent = 95
        };

        // First Save
        await service.SaveAsync(customSettings);
        Assert.True(File.Exists(_settingsPath));

        // Verify primary file content
        var loaded = await service.LoadAsync();
        Assert.Equal(30, loaded.FrequencyMinutes);
        Assert.Equal("24h", loaded.TimeFormat);
        Assert.Equal("mock:en_GB-oliver", loaded.SelectedVoiceId);
        Assert.Equal(95, loaded.VoiceVolumePercent);

        // Second Save -> should create .bak of the first version
        customSettings.FrequencyMinutes = 60;
        await service.SaveAsync(customSettings);

        string backupPath = _settingsPath + ".bak";
        Assert.True(File.Exists(backupPath));

        // Backup file should contain the previous 30-min setting
        string backupContent = await File.ReadAllTextAsync(backupPath);
        var backupSettings = JsonSerializer.Deserialize<TimeChimeSettings>(backupContent);
        Assert.NotNull(backupSettings);
        Assert.Equal(30, backupSettings.FrequencyMinutes);
    }

    [Fact]
    public async Task LoadAsync_CorruptedPrimaryFile_RecoversFromBackup()
    {
        var service = new SettingsService(_settingsPath);

        // 1. Save valid settings to generate primary and backup
        var v1 = new TimeChimeSettings { FrequencyMinutes = 30, TimeFormat = "24h" };
        await service.SaveAsync(v1);

        var v2 = new TimeChimeSettings { FrequencyMinutes = 60, TimeFormat = "12h" };
        await service.SaveAsync(v2);

        // Primary is now v2 (60 min), backup is v1 (30 min)
        // 2. Corrupt the primary file with broken JSON
        await File.WriteAllTextAsync(_settingsPath, "{ corrupted json payload: [broken ... ");

        // 3. Load should detect corruption, recover from backup (v1, 30 min)
        var recovered = await service.LoadAsync();
        Assert.NotNull(recovered);
        Assert.Equal(30, recovered.FrequencyMinutes);
    }

    [Fact]
    public async Task LoadAsync_BothPrimaryAndBackupCorrupt_FallsBackToDefaultsAndPreservesCorruptFile()
    {
        var service = new SettingsService(_settingsPath);

        // Write garbage to both primary and backup
        await File.WriteAllTextAsync(_settingsPath, "TOTAL_GARBAGE");
        await File.WriteAllTextAsync(_settingsPath + ".bak", "MORE_GARBAGE");

        var fallback = await service.LoadAsync();
        Assert.NotNull(fallback);
        Assert.Equal(15, fallback.FrequencyMinutes); // Default value

        // Forensic copy should exist
        var corruptFiles = Directory.GetFiles(_testDir, "settings.json.corrupt.*");
        Assert.NotEmpty(corruptFiles);
    }
}

