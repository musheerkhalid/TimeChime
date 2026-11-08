using System.Diagnostics;
using System.Text.Json;
using KokoroSharp;
using NAudio.Wave;
using TimeChime.Core.Scheduling;

namespace TimeChime.VoiceGenerator;

public record VoiceSpec(
    string Id,
    string DisplayName,
    string Gender,
    string Locale,
    string Accent,
    string Description,
    string Language
);

public record PreviewSpec(
    string Key,
    string FileName,
    string Text
);

public record SlotSpec(
    string SlotKey,
    int Hour,
    int Minute,
    string Text,
    string FileName
);

public record GeneratedClipMetadata(
    string SlotKey,
    string Text,
    string RelativePath,
    long FileSizeBytes,
    double DurationSeconds,
    int SampleRate,
    int Channels,
    double PeakDbfs,
    double RmsDbfs
);

public record GeneratedVoiceRecord(
    string VoiceId,
    string DisplayName,
    string Gender,
    string Locale,
    string Accent,
    string Description,
    string Engine,
    string License,
    string SampleText,
    List<GeneratedClipMetadata> Previews,
    List<GeneratedClipMetadata> Clips
);

public record VoiceCatalogManifest(
    int SchemaVersion,
    string Engine,
    string SourceModel,
    string License,
    string GeneratedAtUtc,
    List<GeneratedVoiceRecord> Voices
);

public static class Program
{
    public static readonly VoiceSpec[] ApprovedVoices =
    [
        // English — 3 Female
        new("af_heart", "Heart (US Expressive)", "Female", "en-US", "American", "Warm, expressive American female voice", "en"),
        new("af_bella", "Bella (US Warm)", "Female", "en-US", "American", "Warm, inviting conversational American female voice", "en"),
        new("bf_emma", "Emma (British Formal)", "Female", "en-GB", "British", "Clear, poised British Received Pronunciation", "en"),

        // English — 3 Male
        new("am_michael", "Michael (US Rich)", "Male", "en-US", "American", "Rich baritone American male narrator", "en"),
        new("am_eric", "Eric (US Grounded)", "Male", "en-US", "American", "Grounded, natural conversational American male voice", "en"),
        new("bm_george", "George (British Warm)", "Male", "en-GB", "British", "Warm, distinguished British male voice", "en"),

        // Hindi — 1 Female
        new("hf_alpha", "Alpha (Hindi Expressive)", "Female", "hi-IN", "Indian", "Expressive, articulate Hindi female neural voice", "hi"),

        // Hindi — 1 Male
        new("hm_omega", "Omega (Hindi Natural)", "Male", "hi-IN", "Indian", "Warm, natural conversational Hindi male voice", "hi")
    ];

    public static readonly PreviewSpec[] EnglishPreviews =
    [
        new("0900", "sample_09_00.wav", "It's nine o'clock."),
        new("1015", "sample_10_15.wav", "It's ten fifteen."),
        new("1230", "sample_12_30.wav", "It's twelve thirty."),
        new("2345", "sample_23_45.wav", "It's eleven forty-five.")
    ];

    public static readonly PreviewSpec[] HindiPreviews =
    [
        new("0900", "sample_09_00.wav", "अभी सुबह के नौ बज रहे हैं।"),
        new("1015", "sample_10_15.wav", "अभी सुबह के सवा दस बजे हैं।"),
        new("1230", "sample_12_30.wav", "अभी दोपहर के साढ़े बारह बजे हैं।"),
        new("2345", "sample_23_45.wav", "अभी रात के पौने बारह बजे हैं।")
    ];

    public static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        bool fullMode = args.Contains("--full", StringComparer.OrdinalIgnoreCase);

        Console.WriteLine("=========================================================");
        Console.WriteLine(fullMode
            ? " Time Chime — Kokoro-82M Voice Pack Generator (Full 480 Recordings)"
            : " Time Chime — Kokoro-82M Voice Preview Generator (Phase 3A - 32 Previews)");
        Console.WriteLine("=========================================================");

        string solutionRoot = FindSolutionRoot();
        Console.WriteLine($"Repository root: {solutionRoot}");

        string assetsDir = Path.Combine(solutionRoot, "assets", "voices");
        string appAssetsDir = Path.Combine(solutionRoot, "src", "TimeChime.App", "assets", "voices");

        Directory.CreateDirectory(assetsDir);
        Directory.CreateDirectory(appAssetsDir);

        Console.WriteLine("Loading Kokoro-82M neural synthesizer...");
        var synth = KokoroWavSynthesizer.LoadModel();
        Console.WriteLine("Kokoro-82M loaded successfully.");

        var catalogManifest = new VoiceCatalogManifest(
            SchemaVersion: 3,
            Engine: "Kokoro-82M",
            SourceModel: "hexgrad/Kokoro-82M v1.0",
            License: "Apache-2.0",
            GeneratedAtUtc: DateTime.UtcNow.ToString("O"),
            Voices: []
        );

        var globalSw = Stopwatch.StartNew();
        int totalPreviewsGenerated = 0;
        int totalClipsGenerated = 0;

        foreach (var spec in ApprovedVoices)
        {
            Console.WriteLine($"\n--- Processing Voice: {spec.Id} ({spec.DisplayName}) [{spec.Language.ToUpper()}] ---");
            var voice = KokoroVoiceManager.GetVoice(spec.Id);

            string voiceDir = Path.Combine(assetsDir, spec.Id);
            string appVoiceDir = Path.Combine(appAssetsDir, spec.Id);
            string previewDir = Path.Combine(voiceDir, "previews");
            string appPreviewDir = Path.Combine(appVoiceDir, "previews");

            Directory.CreateDirectory(voiceDir);
            Directory.CreateDirectory(appVoiceDir);
            Directory.CreateDirectory(previewDir);
            Directory.CreateDirectory(appPreviewDir);

            // 1. Generate 4 Phase 3A Previews
            var previewSpecs = spec.Language == "hi" ? HindiPreviews : EnglishPreviews;
            var previewRecords = new List<GeneratedClipMetadata>();

            foreach (var p in previewSpecs)
            {
                string targetPath = Path.Combine(previewDir, p.FileName);
                string appTargetPath = Path.Combine(appPreviewDir, p.FileName);

                var meta = GenerateOrValidateClip(synth, voice, p.Text, targetPath, appTargetPath, spec.Id, p.Key, forceRegenerate: true);
                previewRecords.Add(meta);
                totalPreviewsGenerated++;
                Console.WriteLine($"  [Preview] {p.Key} ({p.FileName}) -> \"{p.Text}\" ({meta.DurationSeconds}s, {meta.PeakDbfs:F1} dBFS)");
            }

            // 2. Full Mode: Generate Production Recordings
            var clipRecords = new List<GeneratedClipMetadata>();
            if (fullMode)
            {
                if (spec.Language == "hi")
                {
                    // 96 unique time slots
                    var slots = BuildHindi96Slots();
                    Console.WriteLine($"  Synthesizing 96 unique Hindi production slots...");
                    for (int i = 0; i < slots.Count; i++)
                    {
                        var slot = slots[i];
                        string targetPath = Path.Combine(voiceDir, slot.FileName);
                        string appTargetPath = Path.Combine(appVoiceDir, slot.FileName);
                        var meta = GenerateOrValidateClip(synth, voice, slot.Text, targetPath, appTargetPath, spec.Id, slot.SlotKey, forceRegenerate: true);
                        clipRecords.Add(meta);
                        totalClipsGenerated++;
                    }
                }
                else
                {
                    // English: 48 unique time announcements, reused for AM and PM
                    var slots48 = BuildEnglish48Slots();
                    Console.WriteLine($"  Synthesizing 48 unique English production slots (and linking 24h pairs)...");
                    foreach (var slot in slots48)
                    {
                        string targetPath = Path.Combine(voiceDir, slot.FileName);
                        string appTargetPath = Path.Combine(appVoiceDir, slot.FileName);
                        var meta = GenerateOrValidateClip(synth, voice, slot.Text, targetPath, appTargetPath, spec.Id, slot.SlotKey, forceRegenerate: true);
                        clipRecords.Add(meta);
                        totalClipsGenerated++;

                        // Corresponding 12-hour counterpart slot (e.g. 0000 <-> 1200, 0115 <-> 1315)
                        int h = int.Parse(slot.SlotKey.Substring(0, 2));
                        string m = slot.SlotKey.Substring(2, 2);
                        int altH = (h + 12) % 24;
                        string altKey = $"{altH:D2}{m}";
                        string altFileName = $"{altKey}.wav";
                        string altTarget = Path.Combine(voiceDir, altFileName);
                        string altAppTarget = Path.Combine(appVoiceDir, altFileName);

                        File.Copy(targetPath, altTarget, overwrite: true);
                        File.Copy(appTargetPath, altAppTarget, overwrite: true);
                    }
                }
            }

            string sampleText = spec.Language == "hi" ? "अभी सुबह के सवा दस बजे हैं।" : "It's ten fifteen.";

            catalogManifest.Voices.Add(new GeneratedVoiceRecord(
                VoiceId: spec.Id,
                DisplayName: spec.DisplayName,
                Gender: spec.Gender,
                Locale: spec.Locale,
                Accent: spec.Accent,
                Description: spec.Description,
                Engine: "Kokoro-82M",
                License: "Apache-2.0",
                SampleText: sampleText,
                Previews: previewRecords,
                Clips: clipRecords
            ));
        }

        globalSw.Stop();
        Console.WriteLine($"\n=========================================================");
        Console.WriteLine($" Total Execution Time: {globalSw.Elapsed.TotalSeconds:F1}s");
        Console.WriteLine($" Total Previews: {totalPreviewsGenerated}, Production Clips: {totalClipsGenerated}");
        Console.WriteLine("=========================================================");

        // Write Catalog Manifest
        string manifestJson = JsonSerializer.Serialize(catalogManifest, new JsonSerializerOptions { WriteIndented = true });
        string manifestPath = Path.Combine(assetsDir, "voice_manifest.json");
        string appManifestPath = Path.Combine(appAssetsDir, "voice_manifest.json");

        File.WriteAllText(manifestPath, manifestJson);
        File.WriteAllText(appManifestPath, manifestJson);
        Console.WriteLine($"Wrote catalog manifest to: {manifestPath}");

        // Generate Preview Markdown Report
        GeneratePreviewsMarkdownReport(solutionRoot, catalogManifest);
        Console.WriteLine("Generated audit report at: docs/voice-previews-report.md");

        if (fullMode)
        {
            GeneratePacksMarkdownReport(solutionRoot, catalogManifest);
            Console.WriteLine("Generated audit report at: docs/voice-packs-report.md");
        }

        return 0;
    }

    private static void GeneratePacksMarkdownReport(string root, VoiceCatalogManifest manifest)
    {
        string docsDir = Path.Combine(root, "docs");
        Directory.CreateDirectory(docsDir);
        string reportPath = Path.Combine(docsDir, "voice-packs-report.md");

        using var sw = new StreamWriter(reportPath);
        sw.WriteLine("# Phase 3B — Kokoro-82M Voice Packs Audit Report (8 Final Voices)");
        sw.WriteLine();
        sw.WriteLine($"- **Engine**: {manifest.Engine} ({manifest.SourceModel})");
        sw.WriteLine($"- **License**: {manifest.License}");
        sw.WriteLine($"- **Generated At (UTC)**: {manifest.GeneratedAtUtc}");
        sw.WriteLine($"- **Total Voices**: {manifest.Voices.Count} (6 English: 3 Female, 3 Male; 2 Hindi: 1 Female, 1 Male)");
        sw.WriteLine($"- **Total Production Recordings**: 480 unique recordings (English: 6 x 48 = 288; Hindi: 2 x 96 = 192)");
        sw.WriteLine($"- **Total Accessible Slots per Voice**: 96 slots across all 24 hours");
        sw.WriteLine();
        sw.WriteLine("## Voice Pack Summary Table");
        sw.WriteLine();
        sw.WriteLine("| Voice ID | Display Name | Gender | Locale | Accent | Unique Recordings | Avg Peak dBFS | Avg RMS dBFS | Total Size |");
        sw.WriteLine("|---|---|---|---|---|---|---|---|---|");

        foreach (var v in manifest.Voices)
        {
            double avgPeak = v.Clips.Count > 0 ? v.Clips.Average(s => s.PeakDbfs) : 0;
            double avgRms = v.Clips.Count > 0 ? v.Clips.Average(s => s.RmsDbfs) : 0;
            long totalBytes = v.Clips.Sum(s => s.FileSizeBytes);
            sw.WriteLine($"| `{v.VoiceId}` | **{v.DisplayName}** | {v.Gender} | `{v.Locale}` | {v.Accent} | {v.Clips.Count} | {avgPeak:F1} dB | {avgRms:F1} dB | {totalBytes / (1024 * 1024.0):F2} MB |");
        }

        sw.WriteLine();
        sw.WriteLine("## Production Verification Checkpoints");
        sw.WriteLine();
        sw.WriteLine("- **English Voices**: Synthesizes 48 quarter-hour recordings per voice using 12-hour spoken numbers and \"It's...\" phrasing. Reuses corresponding AM/PM audio files.");
        sw.WriteLine("- **Hindi Voices**: Synthesizes 96 unique quarter-hour recordings per voice in Devanagari with context-appropriate time-of-day references (सुबह, दोपहर, शाम, रात).");
        sw.WriteLine("- **Audio Specifications**: 24,000 Hz, 16-bit Mono PCM WAV, peak-normalized to -1.0 dBFS with zero clipping.");
    }

    private static GeneratedClipMetadata GenerateOrValidateClip(
        KokoroWavSynthesizer synth,
        dynamic voice,
        string text,
        string targetPath,
        string appTargetPath,
        string voiceId,
        string slotKey,
        bool forceRegenerate = false)
    {
        if (!forceRegenerate && File.Exists(targetPath) && new FileInfo(targetPath).Length > 10000)
        {
            if (!File.Exists(appTargetPath) || new FileInfo(appTargetPath).Length != new FileInfo(targetPath).Length)
            {
                File.Copy(targetPath, appTargetPath, overwrite: true);
            }
            return ValidateExistingWav(targetPath, voiceId, slotKey, text);
        }

        string tempRawPath = Path.Combine(Path.GetTempPath(), $"kokoro_{voiceId}_{slotKey}_{Guid.NewGuid():N}.wav");
        try
        {
            var rawAudio = synth.Synthesize(text, voice);
            KokoroWavSynthesizer.SaveAudioToFile(rawAudio, tempRawPath);

            var meta = NormalizeAndSaveWav(tempRawPath, targetPath, voiceId, slotKey, text);
            File.Copy(targetPath, appTargetPath, overwrite: true);
            return meta;
        }
        finally
        {
            if (File.Exists(tempRawPath))
            {
                try { File.Delete(tempRawPath); } catch { }
            }
        }
    }

    public static List<SlotSpec> BuildEnglish48Slots()
    {
        var slots = new List<SlotSpec>(48);
        for (int h = 0; h < 12; h++)
        {
            foreach (int m in new[] { 0, 15, 30, 45 })
            {
                string key = $"{h:D2}{m:D2}";
                var dto = new DateTimeOffset(2026, 1, 1, h, m, 0, TimeSpan.Zero);
                string text = AnnouncementFormatter.FormatEnglish(dto);
                string fileName = $"{key}.wav";
                slots.Add(new SlotSpec(key, h, m, text, fileName));
            }
        }
        return slots;
    }

    public static List<SlotSpec> BuildHindi96Slots()
    {
        var slots = new List<SlotSpec>(96);
        for (int h = 0; h < 24; h++)
        {
            foreach (int m in new[] { 0, 15, 30, 45 })
            {
                string key = $"{h:D2}{m:D2}";
                var dto = new DateTimeOffset(2026, 1, 1, h, m, 0, TimeSpan.Zero);
                string text = AnnouncementFormatter.FormatHindi(dto);
                string fileName = $"{key}.wav";
                slots.Add(new SlotSpec(key, h, m, text, fileName));
            }
        }
        return slots;
    }

    private static GeneratedClipMetadata NormalizeAndSaveWav(string sourceWavPath, string destinationWavPath, string voiceId, string slotKey, string text)
    {
        using var reader = new WaveFileReader(sourceWavPath);
        var sampleProvider = reader.ToSampleProvider();
        float[] buffer = new float[reader.SampleCount];
        int read = sampleProvider.Read(buffer, 0, buffer.Length);

        float maxPeak = 0f;
        double sumSquares = 0;
        for (int i = 0; i < read; i++)
        {
            float abs = Math.Abs(buffer[i]);
            if (abs > maxPeak) maxPeak = abs;
            sumSquares += abs * abs;
        }

        // Peak target: -1.0 dBFS (~0.891)
        float targetPeak = 0.891f;
        float gain = maxPeak > 0 ? targetPeak / maxPeak : 1.0f;

        for (int i = 0; i < read; i++)
        {
            buffer[i] = Math.Clamp(buffer[i] * gain, -1.0f, 1.0f);
        }

        float finalPeak = 0f;
        double finalSumSquares = 0;
        for (int i = 0; i < read; i++)
        {
            float abs = Math.Abs(buffer[i]);
            if (abs > finalPeak) finalPeak = abs;
            finalSumSquares += abs * abs;
        }

        double rms = Math.Sqrt(finalSumSquares / read);
        double peakDbfs = 20 * Math.Log10(Math.Max(finalPeak, 1e-6));
        double rmsDbfs = 20 * Math.Log10(Math.Max(rms, 1e-6));

        var waveFormat = new WaveFormat(24000, 16, 1);
        using (var writer = new WaveFileWriter(destinationWavPath, waveFormat))
        {
            short[] pcm16 = new short[read];
            for (int i = 0; i < read; i++)
            {
                pcm16[i] = (short)Math.Clamp((int)(buffer[i] * short.MaxValue), short.MinValue, short.MaxValue);
            }
            writer.WriteSamples(pcm16, 0, read);
        }

        var fi = new FileInfo(destinationWavPath);
        double duration = (double)read / reader.WaveFormat.SampleRate;

        return new GeneratedClipMetadata(
            SlotKey: slotKey,
            Text: text,
            RelativePath: $"assets/voices/{voiceId}/previews/{Path.GetFileName(destinationWavPath)}",
            FileSizeBytes: fi.Length,
            DurationSeconds: Math.Round(duration, 2),
            SampleRate: 24000,
            Channels: 1,
            PeakDbfs: Math.Round(peakDbfs, 2),
            RmsDbfs: Math.Round(rmsDbfs, 2)
        );
    }

    private static GeneratedClipMetadata ValidateExistingWav(string wavPath, string voiceId, string slotKey, string text)
    {
        using var reader = new WaveFileReader(wavPath);
        var sampleProvider = reader.ToSampleProvider();
        float[] buffer = new float[reader.SampleCount];
        int read = sampleProvider.Read(buffer, 0, buffer.Length);

        float maxPeak = 0f;
        double sumSquares = 0;
        for (int i = 0; i < read; i++)
        {
            float abs = Math.Abs(buffer[i]);
            if (abs > maxPeak) maxPeak = abs;
            sumSquares += abs * abs;
        }

        double rms = Math.Sqrt(sumSquares / read);
        double peakDbfs = 20 * Math.Log10(Math.Max(maxPeak, 1e-6));
        double rmsDbfs = 20 * Math.Log10(Math.Max(rms, 1e-6));
        var fi = new FileInfo(wavPath);
        double duration = (double)read / reader.WaveFormat.SampleRate;

        return new GeneratedClipMetadata(
            SlotKey: slotKey,
            Text: text,
            RelativePath: $"assets/voices/{voiceId}/{Path.GetFileName(wavPath)}",
            FileSizeBytes: fi.Length,
            DurationSeconds: Math.Round(duration, 2),
            SampleRate: reader.WaveFormat.SampleRate,
            Channels: reader.WaveFormat.Channels,
            PeakDbfs: Math.Round(peakDbfs, 2),
            RmsDbfs: Math.Round(rmsDbfs, 2)
        );
    }

    private static void GeneratePreviewsMarkdownReport(string root, VoiceCatalogManifest manifest)
    {
        string docsDir = Path.Combine(root, "docs");
        Directory.CreateDirectory(docsDir);
        string reportPath = Path.Combine(docsDir, "voice-previews-report.md");

        using var sw = new StreamWriter(reportPath);
        sw.WriteLine("# Phase 3A — Kokoro-82M Voice Previews Report (8 Final Voices)");
        sw.WriteLine();
        sw.WriteLine($"- **Engine**: {manifest.Engine} ({manifest.SourceModel})");
        sw.WriteLine($"- **License**: {manifest.License}");
        sw.WriteLine($"- **Generated At (UTC)**: {manifest.GeneratedAtUtc}");
        sw.WriteLine($"- **Total Voices**: {manifest.Voices.Count} (6 English: 3 Female, 3 Male; 2 Hindi: 1 Female, 1 Male)");
        sw.WriteLine($"- **Total Preview Clips**: {manifest.Voices.Sum(v => v.Previews.Count)} WAV files (4 per voice)");
        sw.WriteLine();
        sw.WriteLine("## Voice Catalog & Previews Summary");
        sw.WriteLine();
        sw.WriteLine("| Voice ID | Display Name | Gender | Locale | Accent | Previews | Avg Peak dBFS | Avg RMS dBFS | Sample File Size |");
        sw.WriteLine("|---|---|---|---|---|---|---|---|---|");

        foreach (var v in manifest.Voices)
        {
            double avgPeak = v.Previews.Count > 0 ? v.Previews.Average(s => s.PeakDbfs) : 0;
            double avgRms = v.Previews.Count > 0 ? v.Previews.Average(s => s.RmsDbfs) : 0;
            long totalBytes = v.Previews.Sum(s => s.FileSizeBytes);
            sw.WriteLine($"| `{v.VoiceId}` | **{v.DisplayName}** | {v.Gender} | `{v.Locale}` | {v.Accent} | {v.Previews.Count} / 4 | {avgPeak:F1} dB | {avgRms:F1} dB | {totalBytes / 1024.0:F1} KB |");
        }

        sw.WriteLine();
        sw.WriteLine("## Preview Utterance Samples");
        sw.WriteLine();

        foreach (var v in manifest.Voices)
        {
            sw.WriteLine($"### Voice: `{v.VoiceId}` — {v.DisplayName}");
            sw.WriteLine();
            sw.WriteLine("| Slot Key | Spoken Utterance | Duration | Peak dBFS | RMS dBFS | File Name |");
            sw.WriteLine("|---|---|---|---|---|---|");
            foreach (var clip in v.Previews)
            {
                sw.WriteLine($"| `{clip.SlotKey}` | *\"{clip.Text}\"* | {clip.DurationSeconds}s | {clip.PeakDbfs:F1} dB | {clip.RmsDbfs:F1} dB | `{Path.GetFileName(clip.RelativePath)}` |");
            }
            sw.WriteLine();
        }
    }

    private static string FindSolutionRoot()
    {
        string current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "TimeChime.sln")))
            {
                return current;
            }
            current = Directory.GetParent(current)?.FullName ?? string.Empty;
        }
        return Directory.GetCurrentDirectory();
    }
}
