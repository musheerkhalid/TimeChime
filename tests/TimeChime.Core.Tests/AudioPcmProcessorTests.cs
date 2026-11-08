using System.Text;
using TimeChime.Core.Audio;
using Xunit;

namespace TimeChime.Core.Tests;

public class AudioPcmProcessorTests
{
    private static byte[] CreateTestWav(int sampleRate = 24000, int numSamples = 2400, short sampleValue = 10000)
    {
        int dataSize = numSamples * 2;
        byte[] wav = new byte[44 + dataSize];

        // RIFF header
        Encoding.ASCII.GetBytes("RIFF").CopyTo(wav, 0);
        BitConverter.GetBytes(36 + dataSize).CopyTo(wav, 4);
        Encoding.ASCII.GetBytes("WAVE").CopyTo(wav, 8);

        // fmt chunk
        Encoding.ASCII.GetBytes("fmt ").CopyTo(wav, 12);
        BitConverter.GetBytes(16).CopyTo(wav, 16); // Subchunk1Size
        BitConverter.GetBytes((short)1).CopyTo(wav, 20); // PCM
        BitConverter.GetBytes((short)1).CopyTo(wav, 22); // Mono
        BitConverter.GetBytes(sampleRate).CopyTo(wav, 24);
        BitConverter.GetBytes(sampleRate * 2).CopyTo(wav, 28);
        BitConverter.GetBytes((short)2).CopyTo(wav, 32); // BlockAlign
        BitConverter.GetBytes((short)16).CopyTo(wav, 34); // BitsPerSample

        // data chunk
        Encoding.ASCII.GetBytes("data").CopyTo(wav, 36);
        BitConverter.GetBytes(dataSize).CopyTo(wav, 40);

        for (int i = 0; i < numSamples; i++)
        {
            BitConverter.GetBytes(sampleValue).CopyTo(wav, 44 + (i * 2));
        }

        return wav;
    }

    [Fact]
    public void VolumeScaling_HalvesAmplitude_At50Percent()
    {
        var original = CreateTestWav(sampleRate: 24000, numSamples: 100, sampleValue: 10000);
        var processed = AudioPcmProcessor.ApplyVolumeAndSpeed(original, speed: 1.0, volumePercent: 50);

        short originalVal = BitConverter.ToInt16(original, 44);
        short scaledVal = BitConverter.ToInt16(processed, 44);

        Assert.Equal(10000, originalVal);
        Assert.InRange(scaledVal, 4990, 5010);
    }

    [Fact]
    public void VolumeScaling_MutesAudio_AtZeroPercent()
    {
        var original = CreateTestWav(sampleRate: 24000, numSamples: 100, sampleValue: 10000);
        var processed = AudioPcmProcessor.ApplyVolumeAndSpeed(original, speed: 1.0, volumePercent: 0);

        for (int i = 44; i < processed.Length; i += 2)
        {
            short val = BitConverter.ToInt16(processed, i);
            Assert.Equal(0, val);
        }
    }

    [Fact]
    public void SpeedStretching_HalvesDuration_At2xSpeed()
    {
        var original = CreateTestWav(sampleRate: 24000, numSamples: 2400, sampleValue: 10000); // 0.1s
        var processed = AudioPcmProcessor.ApplyVolumeAndSpeed(original, speed: 2.0, volumePercent: 100);

        int origDataSize = BitConverter.ToInt32(original, 40);
        int procDataSize = BitConverter.ToInt32(processed, 40);

        Assert.Equal(4800, origDataSize);
        Assert.Equal(2400, procDataSize); // half sample count
    }

    [Fact]
    public void SpeedStretching_DoublesDuration_AtHalfSpeed()
    {
        var original = CreateTestWav(sampleRate: 24000, numSamples: 1000, sampleValue: 10000);
        var processed = AudioPcmProcessor.ApplyVolumeAndSpeed(original, speed: 0.5, volumePercent: 100);

        int origDataSize = BitConverter.ToInt32(original, 40);
        int procDataSize = BitConverter.ToInt32(processed, 40);

        Assert.Equal(2000, origDataSize);
        Assert.Equal(4000, procDataSize); // double sample count
    }

    [Fact]
    public void FullPassThrough_AtDefault100VolumeAnd1xSpeed()
    {
        var original = CreateTestWav(sampleRate: 24000, numSamples: 500, sampleValue: 8000);
        var processed = AudioPcmProcessor.ApplyVolumeAndSpeed(original, speed: 1.0, volumePercent: 100);

        Assert.Same(original, processed);
    }
}
