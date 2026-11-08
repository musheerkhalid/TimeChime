using TimeChime.Core.Voices;

namespace TimeChime.Core.Contracts;

public interface IVoiceCatalog
{
    Task<IReadOnlyList<VoiceProfile>> ListAvailableAsync(CancellationToken cancellationToken = default);
    VoiceProfile? GetVoiceById(string voiceId);
}

public interface ISpeechSynthesizer
{
    Task SynthesizeAndPlayPreviewAsync(string voiceId, string text, double speed, int volumePercent, bool playBell = false, CancellationToken cancellationToken = default);
    void StopPreview();
    bool IsPreviewPlaying { get; }
}

