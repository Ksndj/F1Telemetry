using F1Telemetry.Core.Interfaces;
using F1Telemetry.Core.Models;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace F1Telemetry.TTS.Services;

/// <summary>
/// Applies lightweight VoiceAI microphone preprocessing before speech recognition.
/// </summary>
public sealed class VoiceInputAudioProcessor : IVoiceInputAudioProcessor
{
    private const int TargetSampleRate = 16_000;
    private const int TargetBitsPerSample = 16;
    private const int TargetChannels = 1;
    private const int FrameDurationMs = 20;
    private const double LimiterPeak = 0.98d;
    private const double TargetRmsDb = -18d;
    private const double MaxAutoGain = 8d;
    private const double MinAutoGain = 0.5d;

    /// <inheritdoc />
    public VoiceInputAudioProcessingResult Process(
        VoiceRecordingResult recording,
        VoiceInputAudioSettings settings)
    {
        ArgumentNullException.ThrowIfNull(recording);
        var normalized = (settings ?? new VoiceInputAudioSettings()).Normalize();

        if (recording.WaveBytes.Length == 0)
        {
            return CreateEmptyResult(recording, normalized.EnableNoiseReduction, VoiceInputAudioFailureReasons.EmptyInput);
        }

        if (!normalized.EnableNoiseReduction)
        {
            return CreateBypassResult(recording);
        }

        var rawSamples = ReadMonoSamples(recording.WaveBytes);
        if (rawSamples.Length == 0)
        {
            return CreateEmptyResult(recording, preprocessingEnabled: true, VoiceInputAudioFailureReasons.EmptyInput);
        }

        var rawRmsDb = ToDb(CalculateRms(rawSamples));
        var processed = rawSamples.ToArray();
        if (normalized.EnableHighPassFilter)
        {
            ApplyHighPassFilter(processed, normalized.HighPassCutoffHz);
        }

        if (normalized.EnableNoiseGate)
        {
            ApplyNoiseGate(processed, normalized.NoiseGateThresholdDb);
        }

        var vadDetected = true;
        var speechDurationMs = ToDurationMs(processed.Length);
        if (normalized.EnableVad)
        {
            var detection = DetectSpeech(processed, normalized);
            vadDetected = detection.Detected;
            speechDurationMs = detection.SpeechDurationMs;
            if (!vadDetected || speechDurationMs < normalized.MinSpeechDurationMs)
            {
                return new VoiceInputAudioProcessingResult
                {
                    Recording = new VoiceRecordingResult
                    {
                        Duration = TimeSpan.Zero,
                        HasInput = false
                    },
                    RawRmsDb = rawRmsDb,
                    ProcessedRmsDb = ToDb(CalculateRms(processed)),
                    PeakDb = ToDb(CalculatePeak(processed)),
                    SpeechDurationMs = Math.Max(0, speechDurationMs),
                    VadDetected = false,
                    PreprocessingEnabled = true,
                    RecognitionFailedReason = VoiceInputAudioFailureReasons.NoSpeechDetected
                };
            }

            processed = processed[detection.StartSample..detection.EndSample];
        }

        if (normalized.EnableAutoGain)
        {
            ApplyAutoGain(processed);
        }

        var wasClipped = ApplyLimiter(processed);
        var processedWaveBytes = WriteWaveBytes(processed);
        var peak = CalculatePeak(processed);
        return new VoiceInputAudioProcessingResult
        {
            Recording = new VoiceRecordingResult
            {
                WaveBytes = processedWaveBytes,
                Duration = TimeSpan.FromMilliseconds(ToDurationMs(processed.Length)),
                PeakLevel = peak,
                AverageLevel = CalculateAverageLevel(processed),
                HasInput = processed.Length > 0 && (vadDetected || peak > 0d)
            },
            RawRmsDb = rawRmsDb,
            ProcessedRmsDb = ToDb(CalculateRms(processed)),
            PeakDb = ToDb(peak),
            SpeechDurationMs = speechDurationMs,
            WasClipped = wasClipped,
            VadDetected = vadDetected,
            PreprocessingEnabled = true
        };
    }

    private static VoiceInputAudioProcessingResult CreateBypassResult(VoiceRecordingResult recording)
    {
        if (!TryReadMonoSamples(recording.WaveBytes, out var samples))
        {
            var rawRms = ToDb(Math.Max(0d, recording.AverageLevel));
            var peakDb = ToDb(Math.Max(0d, recording.PeakLevel));
            return new VoiceInputAudioProcessingResult
            {
                Recording = recording,
                RawRmsDb = rawRms,
                ProcessedRmsDb = rawRms,
                PeakDb = peakDb,
                SpeechDurationMs = (int)Math.Round(recording.Duration.TotalMilliseconds),
                VadDetected = recording.HasInput,
                PreprocessingEnabled = false
            };
        }

        var rawRmsDb = ToDb(CalculateRms(samples));
        return new VoiceInputAudioProcessingResult
        {
            Recording = recording,
            RawRmsDb = rawRmsDb,
            ProcessedRmsDb = rawRmsDb,
            PeakDb = ToDb(CalculatePeak(samples)),
            SpeechDurationMs = (int)Math.Round(recording.Duration.TotalMilliseconds),
            VadDetected = recording.HasInput,
            PreprocessingEnabled = false
        };
    }

    private static VoiceInputAudioProcessingResult CreateEmptyResult(
        VoiceRecordingResult recording,
        bool preprocessingEnabled,
        string failureReason)
    {
        return new VoiceInputAudioProcessingResult
        {
            Recording = recording with { HasInput = false },
            PreprocessingEnabled = preprocessingEnabled,
            RecognitionFailedReason = failureReason
        };
    }

    private static bool TryReadMonoSamples(byte[] waveBytes, out float[] samples)
    {
        try
        {
            samples = ReadMonoSamples(waveBytes);
            return true;
        }
        catch
        {
            samples = Array.Empty<float>();
            return false;
        }
    }

    private static float[] ReadMonoSamples(byte[] waveBytes)
    {
        using var stream = new MemoryStream(waveBytes);
        using var reader = new WaveFileReader(stream);
        var provider = reader.ToSampleProvider();
        var format = provider.WaveFormat;
        var channels = Math.Max(1, format.Channels);
        var buffer = new float[format.SampleRate * channels / 2];
        var mono = new List<float>(Math.Max(0, (int)(reader.SampleCount / channels)));

        int read;
        while ((read = provider.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (var index = 0; index < read; index += channels)
            {
                var sum = 0d;
                var count = Math.Min(channels, read - index);
                for (var channel = 0; channel < count; channel++)
                {
                    sum += buffer[index + channel];
                }

                mono.Add((float)(sum / count));
            }
        }

        return format.SampleRate == TargetSampleRate
            ? mono.ToArray()
            : ResampleLinear(mono, format.SampleRate, TargetSampleRate);
    }

    private static float[] ResampleLinear(IReadOnlyList<float> samples, int sourceRate, int targetRate)
    {
        if (samples.Count == 0 || sourceRate <= 0 || sourceRate == targetRate)
        {
            return samples.ToArray();
        }

        var targetLength = Math.Max(1, (int)Math.Round(samples.Count * targetRate / (double)sourceRate));
        var resampled = new float[targetLength];
        var scale = (samples.Count - 1) / (double)Math.Max(1, targetLength - 1);
        for (var index = 0; index < targetLength; index++)
        {
            var sourcePosition = index * scale;
            var left = (int)Math.Floor(sourcePosition);
            var right = Math.Min(samples.Count - 1, left + 1);
            var fraction = sourcePosition - left;
            resampled[index] = (float)(samples[left] + (samples[right] - samples[left]) * fraction);
        }

        return resampled;
    }

    private static byte[] WriteWaveBytes(IReadOnlyList<float> samples)
    {
        var format = new WaveFormat(TargetSampleRate, TargetBitsPerSample, TargetChannels);
        using var stream = new MemoryStream();
        using (var writer = new WaveFileWriter(stream, format))
        {
            var bytes = new byte[samples.Count * 2];
            for (var index = 0; index < samples.Count; index++)
            {
                var normalized = Math.Clamp(samples[index], -1f, 1f);
                var sample = normalized >= 0
                    ? (short)Math.Round(normalized * short.MaxValue)
                    : (short)Math.Round(normalized * -short.MinValue);
                BitConverter.GetBytes(sample).CopyTo(bytes, index * 2);
            }

            writer.Write(bytes, 0, bytes.Length);
        }

        return stream.ToArray();
    }

    private static void ApplyHighPassFilter(float[] samples, double cutoffHz)
    {
        if (samples.Length == 0)
        {
            return;
        }

        var rc = 1d / (2d * Math.PI * cutoffHz);
        var dt = 1d / TargetSampleRate;
        var alpha = rc / (rc + dt);
        var previousInput = samples[0];
        var previousOutput = 0d;
        for (var index = 0; index < samples.Length; index++)
        {
            var input = samples[index];
            var output = alpha * (previousOutput + input - previousInput);
            samples[index] = (float)output;
            previousInput = input;
            previousOutput = output;
        }
    }

    private static void ApplyNoiseGate(float[] samples, double thresholdDb)
    {
        var threshold = DbToLinear(thresholdDb);
        var frameSize = SamplesFromMilliseconds(FrameDurationMs);
        for (var start = 0; start < samples.Length; start += frameSize)
        {
            var end = Math.Min(samples.Length, start + frameSize);
            if (CalculateRms(samples, start, end - start) >= threshold)
            {
                continue;
            }

            Array.Clear(samples, start, end - start);
        }
    }

    private static VoiceActivityDetection DetectSpeech(float[] samples, VoiceInputAudioSettings settings)
    {
        var threshold = DbToLinear(settings.NoiseGateThresholdDb);
        var frameSize = SamplesFromMilliseconds(FrameDurationMs);
        var firstSpeechFrame = -1;
        var lastSpeechFrame = -1;
        var frameIndex = 0;
        for (var start = 0; start < samples.Length; start += frameSize, frameIndex++)
        {
            var end = Math.Min(samples.Length, start + frameSize);
            if (CalculateRms(samples, start, end - start) < threshold)
            {
                continue;
            }

            firstSpeechFrame = firstSpeechFrame < 0 ? frameIndex : firstSpeechFrame;
            lastSpeechFrame = frameIndex;
        }

        if (firstSpeechFrame < 0 || lastSpeechFrame < firstSpeechFrame)
        {
            return VoiceActivityDetection.None;
        }

        var firstSpeechSample = firstSpeechFrame * frameSize;
        var lastSpeechSample = Math.Min(samples.Length, (lastSpeechFrame + 1) * frameSize);
        var startSample = Math.Max(0, firstSpeechSample - SamplesFromMilliseconds(settings.PreSpeechPaddingMs));
        var endSample = Math.Min(samples.Length, lastSpeechSample + SamplesFromMilliseconds(settings.PostSpeechPaddingMs));
        return new VoiceActivityDetection(
            true,
            startSample,
            Math.Max(startSample, endSample),
            ToDurationMs(lastSpeechSample - firstSpeechSample));
    }

    private static void ApplyAutoGain(float[] samples)
    {
        var rms = CalculateRms(samples);
        if (rms <= 0d)
        {
            return;
        }

        var target = DbToLinear(TargetRmsDb);
        var gain = Math.Clamp(target / rms, MinAutoGain, MaxAutoGain);
        for (var index = 0; index < samples.Length; index++)
        {
            samples[index] = (float)(samples[index] * gain);
        }
    }

    private static bool ApplyLimiter(float[] samples)
    {
        var wasClipped = false;
        for (var index = 0; index < samples.Length; index++)
        {
            if (samples[index] > LimiterPeak)
            {
                samples[index] = (float)LimiterPeak;
                wasClipped = true;
            }
            else if (samples[index] < -LimiterPeak)
            {
                samples[index] = (float)-LimiterPeak;
                wasClipped = true;
            }
        }

        return wasClipped;
    }

    private static double CalculateRms(IReadOnlyList<float> samples)
    {
        return CalculateRms(samples, 0, samples.Count);
    }

    private static double CalculateRms(IReadOnlyList<float> samples, int start, int length)
    {
        if (length <= 0)
        {
            return 0d;
        }

        var end = Math.Min(samples.Count, start + length);
        var sumSquares = 0d;
        var count = 0;
        for (var index = Math.Max(0, start); index < end; index++)
        {
            sumSquares += samples[index] * samples[index];
            count++;
        }

        return count == 0 ? 0d : Math.Sqrt(sumSquares / count);
    }

    private static double CalculatePeak(IReadOnlyList<float> samples)
    {
        var peak = 0d;
        for (var index = 0; index < samples.Count; index++)
        {
            peak = Math.Max(peak, Math.Abs(samples[index]));
        }

        return peak;
    }

    private static double CalculateAverageLevel(IReadOnlyList<float> samples)
    {
        if (samples.Count == 0)
        {
            return 0d;
        }

        var sum = 0d;
        for (var index = 0; index < samples.Count; index++)
        {
            sum += Math.Abs(samples[index]);
        }

        return sum / samples.Count;
    }

    private static double ToDb(double linear)
    {
        return linear <= 0d
            ? VoiceInputAudioMetrics.FloorDb
            : Math.Max(VoiceInputAudioMetrics.FloorDb, 20d * Math.Log10(linear));
    }

    private static double DbToLinear(double db)
    {
        return Math.Pow(10d, db / 20d);
    }

    private static int SamplesFromMilliseconds(int milliseconds)
    {
        return Math.Max(0, (int)Math.Round(TargetSampleRate * milliseconds / 1000d));
    }

    private static int ToDurationMs(int sampleCount)
    {
        return Math.Max(0, (int)Math.Round(sampleCount * 1000d / TargetSampleRate));
    }

    private readonly record struct VoiceActivityDetection(
        bool Detected,
        int StartSample,
        int EndSample,
        int SpeechDurationMs)
    {
        public static VoiceActivityDetection None { get; } = new(false, 0, 0, 0);
    }
}
