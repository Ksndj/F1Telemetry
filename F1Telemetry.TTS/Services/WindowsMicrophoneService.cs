using System.Globalization;
using F1Telemetry.Core.Interfaces;
using F1Telemetry.Core.Models;
using NAudio.Wave;

namespace F1Telemetry.TTS.Services;

/// <summary>
/// Uses NAudio to enumerate, test, and record from Windows microphone devices.
/// </summary>
public sealed class WindowsMicrophoneService : IMicrophoneService
{
    private const int DefaultSampleRate = 16_000;
    private const int DefaultChannels = 1;
    private const double InputThreshold = 0.02d;

    /// <inheritdoc />
    public IReadOnlyList<MicrophoneDeviceInfo> GetDevices()
    {
        var devices = new List<MicrophoneDeviceInfo>();
        for (var index = 0; index < WaveInEvent.DeviceCount; index++)
        {
            var capabilities = WaveInEvent.GetCapabilities(index);
            devices.Add(new MicrophoneDeviceInfo
            {
                DeviceId = index.ToString(CultureInfo.InvariantCulture),
                DisplayName = capabilities.ProductName,
                IsDefault = index == 0
            });
        }

        return devices;
    }

    /// <inheritdoc />
    public IVoiceRecordingSession StartRecording(string? deviceId)
    {
        var deviceNumber = ResolveDeviceNumber(deviceId);
        var waveIn = new WaveInEvent
        {
            DeviceNumber = deviceNumber,
            WaveFormat = new WaveFormat(DefaultSampleRate, 16, DefaultChannels),
            BufferMilliseconds = 80
        };

        return new NAudioVoiceRecordingSession(waveIn);
    }

    /// <inheritdoc />
    public async Task<MicrophoneTestResult> TestInputAsync(
        string? deviceId,
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        using var session = StartRecording(deviceId);
        await Task.Delay(duration, cancellationToken);
        var result = await session.StopAsync(cancellationToken);
        return new MicrophoneTestResult
        {
            HasInput = result.HasInput,
            PeakLevel = result.PeakLevel,
            AverageLevel = result.AverageLevel,
            Duration = result.Duration,
            StatusText = result.HasInput
                ? $"麦克风输入正常 · 峰值 {result.PeakLevel:P0}"
                : "未检测到明显麦克风输入"
        };
    }

    private static int ResolveDeviceNumber(string? deviceId)
    {
        if (int.TryParse(deviceId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var deviceNumber) &&
            deviceNumber >= 0 &&
            deviceNumber < WaveInEvent.DeviceCount)
        {
            return deviceNumber;
        }

        return 0;
    }

    private sealed class NAudioVoiceRecordingSession : IVoiceRecordingSession
    {
        private readonly WaveInEvent _waveIn;
        private readonly MemoryStream _audioStream = new();
        private readonly TaskCompletionSource _stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly object _syncRoot = new();
        private readonly DateTimeOffset _startedAt = DateTimeOffset.UtcNow;
        private long _sampleCount;
        private double _levelSum;
        private double _peakLevel;
        private bool _stopRequested;
        private bool _disposed;

        public NAudioVoiceRecordingSession(WaveInEvent waveIn)
        {
            _waveIn = waveIn;
            _waveIn.DataAvailable += WaveIn_DataAvailable;
            _waveIn.RecordingStopped += WaveIn_RecordingStopped;
            _waveIn.StartRecording();
        }

        public async Task<VoiceRecordingResult> StopAsync(CancellationToken cancellationToken = default)
        {
            lock (_syncRoot)
            {
                if (!_stopRequested)
                {
                    _stopRequested = true;
                    _waveIn.StopRecording();
                }
            }

            await _stopped.Task.WaitAsync(cancellationToken);
            lock (_syncRoot)
            {
                var waveBytes = BuildWaveBytes(_audioStream.ToArray(), _waveIn.WaveFormat);
                var average = _sampleCount == 0 ? 0d : _levelSum / _sampleCount;
                return new VoiceRecordingResult
                {
                    WaveBytes = waveBytes,
                    Duration = DateTimeOffset.UtcNow - _startedAt,
                    PeakLevel = _peakLevel,
                    AverageLevel = average,
                    HasInput = _peakLevel >= InputThreshold || average >= InputThreshold / 2d
                };
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (!_stopRequested)
            {
                try
                {
                    _waveIn.StopRecording();
                }
                catch
                {
                }
            }

            _waveIn.DataAvailable -= WaveIn_DataAvailable;
            _waveIn.RecordingStopped -= WaveIn_RecordingStopped;
            _waveIn.Dispose();
            _audioStream.Dispose();
        }

        private void WaveIn_DataAvailable(object? sender, WaveInEventArgs e)
        {
            lock (_syncRoot)
            {
                _audioStream.Write(e.Buffer, 0, e.BytesRecorded);
                for (var offset = 0; offset + 1 < e.BytesRecorded; offset += 2)
                {
                    var sample = BitConverter.ToInt16(e.Buffer, offset);
                    var level = Math.Abs(sample / 32768d);
                    _peakLevel = Math.Max(_peakLevel, level);
                    _levelSum += level;
                    _sampleCount++;
                }
            }
        }

        private void WaveIn_RecordingStopped(object? sender, StoppedEventArgs e)
        {
            if (e.Exception is null)
            {
                _stopped.TrySetResult();
            }
            else
            {
                _stopped.TrySetException(e.Exception);
            }
        }

        private static byte[] BuildWaveBytes(byte[] pcmBytes, WaveFormat waveFormat)
        {
            using var stream = new MemoryStream();
            using (var writer = new WaveFileWriter(stream, waveFormat))
            {
                writer.Write(pcmBytes, 0, pcmBytes.Length);
            }

            return stream.ToArray();
        }
    }
}
