using System.IO;
using F1Telemetry.Core.Models;
using F1Telemetry.TTS.Services;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies lightweight VoiceAI microphone preprocessing behavior.
/// </summary>
public sealed class VoiceInputAudioProcessorTests
{
    /// <summary>
    /// Verifies empty audio remains empty instead of being filtered into data.
    /// </summary>
    [Fact]
    public void Process_HighPassFilter_DoesNotChangeEmptyInput()
    {
        var processor = new VoiceInputAudioProcessor();

        var result = processor.Process(
            new VoiceRecordingResult(),
            new VoiceInputAudioSettings
            {
                EnableHighPassFilter = true
            });

        Assert.Empty(result.Recording.WaveBytes);
        Assert.False(result.Recording.HasInput);
        Assert.Equal(VoiceInputAudioFailureReasons.EmptyInput, result.RecognitionFailedReason);
    }

    /// <summary>
    /// Verifies the noise gate mutes low-amplitude steady noise.
    /// </summary>
    [Fact]
    public void Process_NoiseGate_ReducesLowAmplitudeNoise()
    {
        var processor = new VoiceInputAudioProcessor();
        var recording = CreateWaveRecording(Enumerable.Repeat(0.001f, 16_000).ToArray());

        var result = processor.Process(
            recording,
            new VoiceInputAudioSettings
            {
                EnableHighPassFilter = false,
                EnableNoiseGate = true,
                NoiseGateThresholdDb = -40d,
                EnableVad = false,
                EnableAutoGain = false
            });

        Assert.True(result.ProcessedRmsDb < result.RawRmsDb - 20d);
        Assert.All(ReadMonoSamples(result.Recording.WaveBytes), sample => Assert.Equal(0f, sample));
    }

    /// <summary>
    /// Verifies VAD rejects recordings with no speech.
    /// </summary>
    [Fact]
    public void Process_VadWithoutSpeech_ReturnsNoSpeechDetected()
    {
        var processor = new VoiceInputAudioProcessor();
        var recording = CreateWaveRecording(new float[16_000]);

        var result = processor.Process(recording, new VoiceInputAudioSettings());

        Assert.False(result.VadDetected);
        Assert.False(result.Recording.HasInput);
        Assert.Equal(VoiceInputAudioFailureReasons.NoSpeechDetected, result.RecognitionFailedReason);
    }

    /// <summary>
    /// Verifies VAD padding keeps the start and end of short phrases.
    /// </summary>
    [Fact]
    public void Process_VadPadding_PreservesShortPhraseEdges()
    {
        var processor = new VoiceInputAudioProcessor();
        var samples = new List<float>();
        samples.AddRange(new float[3_200]);
        samples.AddRange(Enumerable.Repeat(0.1f, 1_920));
        samples.AddRange(new float[4_800]);
        var recording = CreateWaveRecording(samples.ToArray());

        var result = processor.Process(
            recording,
            new VoiceInputAudioSettings
            {
                EnableHighPassFilter = false,
                EnableNoiseGate = false,
                EnableVad = true,
                PreSpeechPaddingMs = 150,
                PostSpeechPaddingMs = 250,
                EnableAutoGain = false,
                MinSpeechDurationMs = 80
            });

        Assert.True(result.VadDetected);
        Assert.InRange(result.Recording.Duration.TotalMilliseconds, 490d, 560d);
        Assert.InRange(result.SpeechDurationMs, 100, 140);
    }

    /// <summary>
    /// Verifies disabling the total switch bypasses all preprocessing stages.
    /// </summary>
    [Fact]
    public void Process_WhenNoiseReductionDisabled_BypassesFilteringGateVadAndAgc()
    {
        var processor = new VoiceInputAudioProcessor();
        var recording = CreateWaveRecording(new float[16_000]);

        var result = processor.Process(
            recording,
            new VoiceInputAudioSettings
            {
                EnableNoiseReduction = false,
                EnableHighPassFilter = true,
                EnableNoiseGate = true,
                EnableVad = true,
                EnableAutoGain = true
            });

        Assert.False(result.PreprocessingEnabled);
        Assert.Same(recording.WaveBytes, result.Recording.WaveBytes);
        Assert.Equal(recording.HasInput, result.VadDetected);
        Assert.Equal(string.Empty, result.RecognitionFailedReason);
    }

    private static VoiceRecordingResult CreateWaveRecording(IReadOnlyList<float> samples)
    {
        using var stream = new MemoryStream();
        using (var writer = new WaveFileWriter(stream, new WaveFormat(16_000, 16, 1)))
        {
            var bytes = new byte[samples.Count * 2];
            for (var index = 0; index < samples.Count; index++)
            {
                var sample = (short)Math.Round(Math.Clamp(samples[index], -1f, 1f) * short.MaxValue);
                BitConverter.GetBytes(sample).CopyTo(bytes, index * 2);
            }

            writer.Write(bytes, 0, bytes.Length);
        }

        var peak = samples.Count == 0 ? 0d : samples.Max(sample => Math.Abs(sample));
        var average = samples.Count == 0 ? 0d : samples.Average(sample => Math.Abs(sample));
        return new VoiceRecordingResult
        {
            HasInput = peak > 0.02d,
            WaveBytes = stream.ToArray(),
            PeakLevel = peak,
            AverageLevel = average,
            Duration = TimeSpan.FromSeconds(samples.Count / 16_000d)
        };
    }

    private static IReadOnlyList<float> ReadMonoSamples(byte[] waveBytes)
    {
        using var stream = new MemoryStream(waveBytes);
        using var reader = new WaveFileReader(stream);
        var provider = reader.ToSampleProvider();
        var buffer = new float[reader.WaveFormat.SampleRate];
        var samples = new List<float>();
        int read;
        while ((read = provider.Read(buffer, 0, buffer.Length)) > 0)
        {
            samples.AddRange(buffer.Take(read));
        }

        return samples;
    }
}
