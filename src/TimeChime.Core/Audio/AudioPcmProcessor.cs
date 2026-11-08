namespace TimeChime.Core.Audio;

public static class AudioPcmProcessor
{
    /// <summary>
    /// Applies volume scaling (0-100%) and speed stretching (0.5x - 2.0x) to a 16-bit PCM WAV stream.
    /// Preserves standard sample rate (e.g. 24000 Hz or 44100 Hz) via linear interpolation resampling
    /// to guarantee 100% compatibility across all Windows audio endpoints.
    /// </summary>
    public static byte[] ApplyVolumeAndSpeed(byte[] wavBytes, double speed, int volumePercent)
    {
        if (wavBytes == null || wavBytes.Length < 44)
        {
            return wavBytes ?? [];
        }

        // Fast path: if 100% volume and 1.0x speed, return original
        if (volumePercent == 100 && Math.Abs(speed - 1.0) < 0.01)
        {
            return wavBytes;
        }

        // 1. Locate the "data" subchunk
        int dataChunkPos = 12;
        int dataSize = 0;
        bool foundData = false;

        while (dataChunkPos + 8 <= wavBytes.Length)
        {
            string chunkId = System.Text.Encoding.ASCII.GetString(wavBytes, dataChunkPos, 4);
            int chunkSize = BitConverter.ToInt32(wavBytes, dataChunkPos + 4);

            if (chunkId == "data")
            {
                dataSize = chunkSize;
                dataChunkPos += 8;
                foundData = true;
                break;
            }

            dataChunkPos += 8 + chunkSize;
        }

        if (!foundData || dataChunkPos >= wavBytes.Length)
        {
            dataChunkPos = 44;
            dataSize = wavBytes.Length - 44;
        }

        short audioFormat = BitConverter.ToInt16(wavBytes, 20); // 1 = PCM
        short channels = BitConverter.ToInt16(wavBytes, 22);
        int sampleRate = BitConverter.ToInt32(wavBytes, 24);
        short bitsPerSample = BitConverter.ToInt16(wavBytes, 34);

        // Fallback for non-16-bit PCM
        if (audioFormat != 1 || bitsPerSample != 16 || channels < 1 || channels > 2)
        {
            return wavBytes;
        }

        int bytesPerSample = (bitsPerSample / 8) * channels;
        int availableBytes = Math.Min(dataSize, wavBytes.Length - dataChunkPos);
        int inputSampleFrames = availableBytes / bytesPerSample;

        if (inputSampleFrames <= 0)
        {
            return wavBytes;
        }

        float gain = Math.Clamp(volumePercent / 100.0f, 0.0f, 1.0f);
        double clampedSpeed = Math.Clamp(speed, 0.5, 2.0);

        // Extract input samples
        int totalInputValues = inputSampleFrames * channels;
        short[] inSamples = new short[totalInputValues];
        for (int i = 0; i < totalInputValues; i++)
        {
            inSamples[i] = BitConverter.ToInt16(wavBytes, dataChunkPos + (i * 2));
        }

        // Calculate output sample frame count based on speed
        int outputSampleFrames = (int)Math.Round(inputSampleFrames / clampedSpeed);
        if (outputSampleFrames <= 0) outputSampleFrames = 1;

        int totalOutputValues = outputSampleFrames * channels;
        short[] outSamples = new short[totalOutputValues];

        // Linear interpolation resampling + volume gain
        for (int j = 0; j < outputSampleFrames; j++)
        {
            double pos = j * clampedSpeed;
            int i0 = (int)pos;
            int i1 = Math.Min(i0 + 1, inputSampleFrames - 1);
            double frac = pos - i0;

            for (int c = 0; c < channels; c++)
            {
                double s0 = inSamples[i0 * channels + c];
                double s1 = inSamples[i1 * channels + c];
                double interpolated = s0 + frac * (s1 - s0);

                double scaled = interpolated * gain;
                outSamples[j * channels + c] = (short)Math.Clamp((int)Math.Round(scaled), short.MinValue, short.MaxValue);
            }
        }

        // Reconstruct valid WAV binary
        int outDataBytes = totalOutputValues * 2;
        byte[] result = new byte[dataChunkPos + outDataBytes];

        // Copy original header
        Array.Copy(wavBytes, 0, result, 0, dataChunkPos);

        // Update RIFF chunk size at offset 4: (Total length - 8)
        BitConverter.GetBytes(dataChunkPos - 8 + outDataBytes).CopyTo(result, 4);

        // Update data chunk size at (dataChunkPos - 4)
        BitConverter.GetBytes(outDataBytes).CopyTo(result, dataChunkPos - 4);

        // Copy processed PCM samples
        Buffer.BlockCopy(outSamples, 0, result, dataChunkPos, outDataBytes);

        return result;
    }
}
