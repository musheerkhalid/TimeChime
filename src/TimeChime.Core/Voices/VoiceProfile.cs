namespace TimeChime.Core.Voices;

/// <summary>
/// Immutable record describing a voice model profile.
/// </summary>
public record VoiceProfile
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string Gender { get; init; } // "Male" or "Female"
    public required string Locale { get; init; } // e.g. "en-US", "en-GB"
    public required string Engine { get; init; } // "Mock", "Piper", "Windows"
    public required string LicenseRecord { get; init; }
    public bool IsMock { get; init; }
    public string SampleText { get; init; } = "The time is ten fifteen AM.";

    public string TaggedDisplayName => IsMock ? $"{DisplayName} [MOCK]" : DisplayName;
}

