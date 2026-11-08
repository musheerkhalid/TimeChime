


using System.IO;
using System.Media;
using System.Text.Json;
using TimeChime.Core.Contracts;
using TimeChime.Core.Voices;
using Windows.Media.SpeechSynthesis;

namespace TimeChime.App.Services;

public class MockVoiceService : IVoiceCatalog, ISpeechSynthesizer
{
    private static readonly List<VoiceProfile> Catalog = LoadVoiceCatalog();

    private static List<VoiceProfile> LoadVoiceCatalog()
    {
        string manifestPath = Path.Combine(AppContext.BaseDirectory, "assets", "voices", "voice_manifest.json");
        if (!File.Exists(manifestPath))
        {
            manifestPath = Path.Combine(Directory.GetCurrentDirectory(), "assets", "voices", "voice_manifest.json");
        }

        if (File.Exists(manifestPath))
        {
            try
            {
                var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
                var list = new List<VoiceProfile>();

                foreach (var el in doc.RootElement.GetProperty("Voices").EnumerateArray())
                {
                    string id = el.GetProperty("VoiceId").GetString()!;
                    string name = el.GetProperty("DisplayName").GetString()!;
                    string gender = el.GetProperty("Gender").GetString()!;
                    string locale = el.GetProperty("Locale").GetString()!;

                    string sampleText = locale.StartsWith("hi", StringComparison.OrdinalIgnoreCase)
                        ? "अभी सुबह के सवा दस बजे हैं।"
                        : "It's ten fifteen.";

                    list.Add(new VoiceProfile
                    {
                        Id = id,
                        DisplayName = name,
                        Gender = gender,
                        Locale = locale,
                        Engine = "Kokoro-82M Neural",
                        LicenseRecord = "Apache-2.0",
                        IsMock = false,
                        SampleText = sampleText
                    });
                }

                if (list.Count > 0) return list;
            }
            catch (Exception ex)
            {
                File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "manifest_load_error.log"), ex.ToString());
            }
        }

        // Fallback default definitions if manifest is not found (8 Approved Voices)
        return
        [
            new VoiceProfile { Id = "af_heart", DisplayName = "Heart (US Expressive)", Gender = "Female", Locale = "en-US", Engine = "Kokoro-82M Neural", LicenseRecord = "Apache-2.0", IsMock = false, SampleText = "It's ten fifteen." },
            new VoiceProfile { Id = "af_bella", DisplayName = "Bella (US Warm)", Gender = "Female", Locale = "en-US", Engine = "Kokoro-82M Neural", LicenseRecord = "Apache-2.0", IsMock = false, SampleText = "It's ten fifteen." },
            new VoiceProfile { Id = "bf_emma", DisplayName = "Emma (British Formal)", Gender = "Female", Locale = "en-GB", Engine = "Kokoro-82M Neural", LicenseRecord = "Apache-2.0", IsMock = false, SampleText = "It's ten fifteen." },
            new VoiceProfile { Id = "am_michael", DisplayName = "Michael (US Rich)", Gender = "Male", Locale = "en-US", Engine = "Kokoro-82M Neural", LicenseRecord = "Apache-2.0", IsMock = false, SampleText = "It's ten fifteen." },
            new VoiceProfile { Id = "am_eric", DisplayName = "Eric (US Grounded)", Gender = "Male", Locale = "en-US", Engine = "Kokoro-82M Neural", LicenseRecord = "Apache-2.0", IsMock = false, SampleText = "It's ten fifteen." },
            new VoiceProfile { Id = "bm_george", DisplayName = "George (British Warm)", Gender = "Male", Locale = "en-GB", Engine = "Kokoro-82M Neural", LicenseRecord = "Apache-2.0", IsMock = false, SampleText = "It's ten fifteen." },
            new VoiceProfile { Id = "hf_alpha", DisplayName = "Alpha (Hindi Expressive)", Gender = "Female", Locale = "hi-IN", Engine = "Kokoro-82M Neural", LicenseRecord = "Apache-2.0", IsMock = false, SampleText = "अभी सुबह के सवा दस बजे हैं।" },
            new VoiceProfile { Id = "hm_omega", DisplayName = "Omega (Hindi Natural)", Gender = "Male", Locale = "hi-IN", Engine = "Kokoro-82M Neural", LicenseRecord = "Apache-2.0", IsMock = false, SampleText = "अभी सुबह के सवा दस बजे हैं।" }
        ];
    }

    private static byte[]? _cachedBellWav;
    private static byte[] GetBellWavBytes()
    {
        if (_cachedBellWav != null) return _cachedBellWav;

        int sampleRate = 44100;
        double duration = 0.85;
        int numSamples = (int)(sampleRate * duration);
        short[] samples = new short[numSamples];

        for (int i = 0; i < numSamples; i++)
        {
            double t = (double)i / sampleRate;
            double attack = Math.Min(1.0, t / 0.006);
            double decay1 = Math.Exp(-4.0 * t);
            double decay2 = Math.Exp(-6.5 * t);
            double decay3 = Math.Exp(-9.0 * t);

            double wave = (0.60 * Math.Sin(2 * Math.PI * 880.0 * t) * decay1)
                        + (0.30 * Math.Sin(2 * Math.PI * 1760.0 * t) * decay2)
                        + (0.10 * Math.Sin(2 * Math.PI * 2640.0 * t) * decay3);

            double sampleValue = attack * wave * short.MaxValue * 0.70;
            samples[i] = (short)Math.Clamp(sampleValue, short.MinValue, short.MaxValue);
        }

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write("RIFF"u8);
        bw.Write(36 + (numSamples * 2));
        bw.Write("WAVE"u8);
        bw.Write("fmt "u8);
        bw.Write(16);
        bw.Write((short)1); // PCM
        bw.Write((short)1); // Mono
        bw.Write(sampleRate);
        bw.Write(sampleRate * 2);
        bw.Write((short)2);
        bw.Write((short)16);
        bw.Write("data"u8);
        bw.Write(numSamples * 2);
        for (int i = 0; i < numSamples; i++)
        {
            bw.Write(samples[i]);
        }
        bw.Flush();
        _cachedBellWav = ms.ToArray();
        return _cachedBellWav;
    }

    private CancellationTokenSource? _previewCts;
    private SoundPlayer? _activePlayer;

    public bool IsPreviewPlaying => _previewCts != null && !_previewCts.IsCancellationRequested;

    public event Action<bool>? PreviewStateChanged;

    public Task<IReadOnlyList<VoiceProfile>> ListAvailableAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<VoiceProfile>>(Catalog);
    }

    public VoiceProfile? GetVoiceById(string voiceId)
    {
        return Catalog.FirstOrDefault(v => v.Id == voiceId);
    }

    public async Task SynthesizeAndPlayPreviewAsync(
        string voiceId,
        string text,
        double speed,
        int volumePercent,
        bool playBell = false,
        CancellationToken cancellationToken = default)
    {
        StopPreview();

        _previewCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _previewCts.Token;
        PreviewStateChanged?.Invoke(true);

        try
        {
            // 1. Play Gentle Bell if enabled (scaled to match user's chosen volume)
            if (playBell)
            {
                byte[] bellBytes = GetBellWavBytes();
                byte[] scaledBell = TimeChime.Core.Audio.AudioPcmProcessor.ApplyVolumeAndSpeed(bellBytes, 1.0, volumePercent);
                using var bellStream = new MemoryStream(scaledBell);
                _activePlayer = new SoundPlayer(bellStream);
                await Task.Run(() => _activePlayer.PlaySync(), token);
                _activePlayer.Dispose();
                _activePlayer = null;

                // Gentle natural pause after bell resonance
                await Task.Delay(250, token);
            }

            // 2. Locate Kokoro Neural Pre-rendered Audio File (Full 96-Slot Pack or Preview)
            string? sampleWavPath = ResolveKokoroAudioPath(voiceId, text);

            if (sampleWavPath != null && File.Exists(sampleWavPath))
            {
                // Play genuine Kokoro neural speech clip from disk with user volume and speech rate applied
                byte[] rawAudio = await File.ReadAllBytesAsync(sampleWavPath, token);
                byte[] processedAudio = TimeChime.Core.Audio.AudioPcmProcessor.ApplyVolumeAndSpeed(rawAudio, speed, volumePercent);
                using var memStream = new MemoryStream(processedAudio);
                _activePlayer = new SoundPlayer(memStream);

                await Task.Run(() =>
                {
                    _activePlayer.PlaySync();
                }, token);
                return;
            }

            // 3. Missing-file error handling (Strict requirement: Never silently substitute another voice)
            string errorMsg = $"[TimeChime ERROR] Missing audio pack clip for voice '{voiceId}' requesting phrase '{text}'. Playback suppressed to avoid substituting another voice.";
            try
            {
                File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "missing_audio.log"), $"{DateTime.UtcNow:O} {errorMsg}\n");
            }
            catch { }
        }
        catch (OperationCanceledException)
        {
            // Expected on user cancellation
        }
        catch (Exception ex)
        {
            File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "preview_error.log"), ex.ToString());
            int fallbackMs = Math.Clamp((int)(2500 / speed), 800, 5000);
            await Task.Delay(fallbackMs, token);
        }
        finally
        {
            _activePlayer?.Dispose();
            _activePlayer = null;
            _previewCts?.Dispose();
            _previewCts = null;
            PreviewStateChanged?.Invoke(false);
        }
    }

    public static string? ResolveKokoroAudioPath(string voiceId, string requestedText)
    {
        // 1. Check if slot key matches (e.g. 0000, 0900, 1200, 1500, 2345)
        if (TimeChime.Core.Scheduling.TimeSlotMapper.TryExtractSlotKey(requestedText, out string slotKey))
        {
            string slotFileName = $"{slotKey}.wav";
            string[] directPaths =
            [
                Path.Combine(AppContext.BaseDirectory, "assets", "voices", voiceId, slotFileName),
                Path.Combine(Directory.GetCurrentDirectory(), "assets", "voices", voiceId, slotFileName),
                Path.Combine(AppContext.BaseDirectory, "assets", "voices", voiceId, "previews", slotFileName),
                Path.Combine(Directory.GetCurrentDirectory(), "assets", "voices", voiceId, "previews", slotFileName)
            ];

            foreach (var path in directPaths)
            {
                if (File.Exists(path)) return path;
            }

            // For English voices, check corresponding 12-hour counterpart slot (e.g. 1500 -> 0300)
            if (!voiceId.StartsWith("h", StringComparison.OrdinalIgnoreCase) && slotKey.Length == 4)
            {
                int h = int.Parse(slotKey.Substring(0, 2));
                string m = slotKey.Substring(2, 2);
                int altH = (h + 12) % 24;
                string altKey = $"{altH:D2}{m}";
                string altFileName = $"{altKey}.wav";

                string[] altPaths =
                [
                    Path.Combine(AppContext.BaseDirectory, "assets", "voices", voiceId, altFileName),
                    Path.Combine(Directory.GetCurrentDirectory(), "assets", "voices", voiceId, altFileName)
                ];

                foreach (var path in altPaths)
                {
                    if (File.Exists(path)) return path;
                }
            }
        }

        // 2. Check preview samples mapping (e.g. sample_09_00.wav, sample_10_15.wav, sample_12_30.wav, sample_23_45.wav)
        string[] searchRoots =
        [
            Path.Combine(AppContext.BaseDirectory, "assets", "voices", voiceId, "previews"),
            Path.Combine(Directory.GetCurrentDirectory(), "assets", "voices", voiceId, "previews"),
            Path.Combine(AppContext.BaseDirectory, "assets", "voices", voiceId),
            Path.Combine(Directory.GetCurrentDirectory(), "assets", "voices", voiceId)
        ];

        string fileName = "sample_10_15.wav";
        if (requestedText.Contains("nine o'clock", StringComparison.OrdinalIgnoreCase) || requestedText.Contains("नौ बज", StringComparison.OrdinalIgnoreCase))
        {
            fileName = "sample_09_00.wav";
        }
        else if (requestedText.Contains("ten fifteen", StringComparison.OrdinalIgnoreCase) || requestedText.Contains("सवा दस", StringComparison.OrdinalIgnoreCase))
        {
            fileName = "sample_10_15.wav";
        }
        else if (requestedText.Contains("twelve thirty", StringComparison.OrdinalIgnoreCase) || requestedText.Contains("साढ़े बारह", StringComparison.OrdinalIgnoreCase))
        {
            fileName = "sample_12_30.wav";
        }
        else if (requestedText.Contains("eleven forty-five", StringComparison.OrdinalIgnoreCase) || requestedText.Contains("पौने बारह", StringComparison.OrdinalIgnoreCase))
        {
            fileName = "sample_23_45.wav";
        }

        foreach (var dir in searchRoots)
        {
            string candidate = Path.Combine(dir, fileName);
            if (File.Exists(candidate)) return candidate;

            if (Directory.Exists(dir))
            {
                var files = Directory.GetFiles(dir, "*.wav");
                if (files.Length > 0) return files[0];
            }
        }

        return null;
    }

    public void StopPreview()
    {
        try
        {
            _activePlayer?.Stop();
        }
        catch { }

        if (_previewCts != null)
        {
            _previewCts.Cancel();
            _previewCts.Dispose();
            _previewCts = null;
            PreviewStateChanged?.Invoke(false);
        }
    }
}
