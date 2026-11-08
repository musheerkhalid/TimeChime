namespace TimeChime.Core.Models;

public record ActiveWindowRule
{
    public TimeOnly Start { get; init; } = new(9, 0);
    public TimeOnly End { get; init; } = new(18, 0);
    public bool AllDay { get; init; } = false;

    /// <summary>
    /// Evaluates if a given time is within the window.
    /// Start is inclusive, End is exclusive.
    /// When Start == End, if AllDay is true, it covers 24 hours.
    /// When Start > End, it spans midnight.
    /// </summary>
    public bool Contains(TimeOnly time)
    {
        if (AllDay) return true;
        if (Start == End) return true; // 24-hour window per blueprint section 4

        if (Start < End)
        {
            // Standard daytime window: [Start, End)
            return time >= Start && time < End;
        }
        else
        {
            // Overnight window spanning midnight, e.g. 22:00 -> 06:00:
            // Valid if >= 22:00 OR < 06:00
            return time >= Start || time < End;
        }
    }
}

public class TimeChimeSettings
{
    public int SchemaVersion { get; set; } = 1;
    public bool Enabled { get; set; } = true;
    public bool Paused { get; set; } = false;
    public int FrequencyMinutes { get; set; } = 15; // 15, 30, 60
    public ActiveWindowRule ActiveWindow { get; set; } = new();
    public List<DayOfWeek> ActiveDays { get; set; } =
    [
        DayOfWeek.Monday,
        DayOfWeek.Tuesday,
        DayOfWeek.Wednesday,
        DayOfWeek.Thursday,
        DayOfWeek.Friday
    ];

    public string SelectedVoiceId { get; set; } = "mock:en_US-clara";
    public string Locale { get; set; } = "en-US";
    public string TimeFormat { get; set; } = "12h"; // "12h" or "24h"
    public int VoiceVolumePercent { get; set; } = 80; // 0..100
    public double SpeechRate { get; set; } = 1.0; // 0.5..2.0
    public bool BellEnabled { get; set; } = false;
    public bool LaunchAtLogin { get; set; } = false;
    public string StartupScope { get; set; } = "User"; // "User" (Current User) or "System" (All Users)
    public bool MinimizeToTray { get; set; } = true;
    public string Theme { get; set; } = "system"; // "dark", "light", "system"
}

