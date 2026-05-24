using System.Windows.Threading;
using F1Telemetry.AI.Interfaces;
using F1Telemetry.AI.Models;
using F1Telemetry.AI.Services;
using F1Telemetry.Analytics.Events;
using F1Telemetry.Analytics.Interfaces;
using F1Telemetry.Analytics.Laps;
using F1Telemetry.Analytics.State;
using F1Telemetry.App.Services;
using F1Telemetry.App.ViewModels;
using F1Telemetry.Core.Interfaces;
using F1Telemetry.Core.Models;
using F1Telemetry.Storage.Interfaces;
using F1Telemetry.Storage.Models;
using F1Telemetry.TTS.Models;
using F1Telemetry.TTS.Services;
using F1Telemetry.Udp.Packets;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies steering-wheel voice AI Raw Input binding and talk-mode behavior.
/// </summary>
public sealed class DashboardVoiceAiInputTests
{
    /// <summary>
    /// Verifies the settings capture flow saves a Raw Input HID button binding.
    /// </summary>
    [Fact]
    public void BindVoiceAiInputCommand_CapturesNextRawInputButton()
    {
        var harness = CreateHarness();
        try
        {
            harness.ViewModel.BindVoiceAiInputCommand.Execute(null);
            harness.ViewModel.ObserveVoiceAiButtonInput(
                CreateButtonInput(isPressed: true, receivedAt: DateTimeOffset.UtcNow + TimeSpan.FromSeconds(1)));

            Assert.Equal(VoiceAiInputBindingKind.RawInputHidButton, harness.ViewModel.VoiceAiInputBinding.Kind);
            Assert.Equal("raw-device-1", harness.ViewModel.VoiceAiInputBinding.DeviceId);
            Assert.Equal(4, harness.ViewModel.VoiceAiInputBinding.ButtonIndex);
            Assert.Equal("方向盘/手柄设备 · 按钮 4", harness.ViewModel.VoiceAiBindingText);
            Assert.NotNull(harness.SettingsStore.LastVoiceAiOptions);
            Assert.Equal(4, harness.SettingsStore.LastVoiceAiOptions!.InputBinding.ButtonIndex);
        }
        finally
        {
            harness.ViewModel.Dispose();
        }
    }

    /// <summary>
    /// Verifies clearing the voice AI input removes the persisted binding.
    /// </summary>
    [Fact]
    public void ClearVoiceAiInputCommand_RemovesSavedBinding()
    {
        var harness = CreateHarness(CreateBoundOptions(VoiceAiTalkMode.HoldToTalk));
        try
        {
            harness.ViewModel.ClearVoiceAiInputCommand.Execute(null);

            Assert.Equal(VoiceAiInputBindingKind.None, harness.ViewModel.VoiceAiInputBinding.Kind);
            Assert.Equal("未绑定方向盘按钮", harness.ViewModel.VoiceAiBindingText);
            Assert.NotNull(harness.SettingsStore.LastVoiceAiOptions);
            Assert.Equal(VoiceAiInputBindingKind.None, harness.SettingsStore.LastVoiceAiOptions!.InputBinding.Kind);
        }
        finally
        {
            harness.ViewModel.Dispose();
        }
    }

    /// <summary>
    /// Verifies the capture window ignores stale input while Raw Input settles.
    /// </summary>
    [Fact]
    public void BindVoiceAiInputCommand_IgnoresInputBeforeArmed()
    {
        var harness = CreateHarness();
        try
        {
            harness.ViewModel.BindVoiceAiInputCommand.Execute(null);
            harness.ViewModel.ObserveVoiceAiButtonInput(CreateButtonInput(isPressed: true));

            Assert.Equal(VoiceAiInputBindingKind.None, harness.ViewModel.VoiceAiInputBinding.Kind);
            Assert.Equal("未绑定方向盘按钮", harness.ViewModel.VoiceAiBindingText);
        }
        finally
        {
            harness.ViewModel.Dispose();
        }
    }

    /// <summary>
    /// Verifies the capture window rejects release and multi-button changes.
    /// </summary>
    [Fact]
    public void BindVoiceAiInputCommand_RejectsReleaseAndMultipleChanges()
    {
        var harness = CreateHarness();
        try
        {
            var armedAt = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(1);
            harness.ViewModel.BindVoiceAiInputCommand.Execute(null);
            harness.ViewModel.ObserveVoiceAiButtonInput(CreateButtonInput(isPressed: false, receivedAt: armedAt));
            harness.ViewModel.ObserveVoiceAiButtonInput(
                CreateButtonInput(
                    isPressed: true,
                    receivedAt: armedAt + TimeSpan.FromMilliseconds(100),
                    pressedChangeCount: 2,
                    changedBitCount: 2));

            Assert.Equal(VoiceAiInputBindingKind.None, harness.ViewModel.VoiceAiInputBinding.Kind);
        }
        finally
        {
            harness.ViewModel.Dispose();
        }
    }

    /// <summary>
    /// Verifies hold-to-talk starts recording on press and submits the completed recording on release.
    /// </summary>
    [Fact]
    public void HoldToTalk_SubmitsRecordingOnRelease()
    {
        var harness = CreateHarness(CreateBoundOptions(VoiceAiTalkMode.HoldToTalk));
        try
        {
            var startedAt = DateTimeOffset.UtcNow;
            harness.ViewModel.ObserveVoiceAiButtonInput(CreateButtonInput(isPressed: true, receivedAt: startedAt));
            Assert.True(harness.ViewModel.IsVoiceAiRecording);

            harness.ViewModel.ObserveVoiceAiButtonInput(
                CreateButtonInput(isPressed: false, receivedAt: startedAt + TimeSpan.FromMilliseconds(100)));
            PumpDispatcherUntil(() => harness.AiService.Contexts.Count == 1, TimeSpan.FromSeconds(2));

            Assert.False(harness.ViewModel.IsVoiceAiRecording);
            Assert.Contains("进站", harness.AiService.Contexts[0].RealtimeEngineerAdviceSummary, StringComparison.Ordinal);
            Assert.Equal(1, harness.MicrophoneService.StartCount);
        }
        finally
        {
            harness.ViewModel.Dispose();
        }
    }

    /// <summary>
    /// Verifies duplicate hold-to-talk presses do not restart the recording session.
    /// </summary>
    [Fact]
    public void HoldToTalk_IgnoresDuplicatePress()
    {
        var harness = CreateHarness(CreateBoundOptions(VoiceAiTalkMode.HoldToTalk));
        try
        {
            var startedAt = DateTimeOffset.UtcNow;
            harness.ViewModel.ObserveVoiceAiButtonInput(CreateButtonInput(isPressed: true, receivedAt: startedAt));
            harness.ViewModel.ObserveVoiceAiButtonInput(
                CreateButtonInput(isPressed: true, receivedAt: startedAt + TimeSpan.FromMilliseconds(100)));

            Assert.True(harness.ViewModel.IsVoiceAiRecording);
            Assert.Equal(1, harness.MicrophoneService.StartCount);
        }
        finally
        {
            harness.ViewModel.Dispose();
        }
    }

    /// <summary>
    /// Verifies toggle-to-talk uses the next released press to end and submit the recording.
    /// </summary>
    [Fact]
    public void ToggleToTalk_SubmitsRecordingOnSecondPressAfterRelease()
    {
        var harness = CreateHarness(CreateBoundOptions(VoiceAiTalkMode.ToggleToTalk));
        try
        {
            var startedAt = DateTimeOffset.UtcNow;
            harness.ViewModel.ObserveVoiceAiButtonInput(CreateButtonInput(isPressed: true, receivedAt: startedAt));
            Assert.True(harness.ViewModel.IsVoiceAiRecording);

            harness.ViewModel.ObserveVoiceAiButtonInput(
                CreateButtonInput(isPressed: false, receivedAt: startedAt + TimeSpan.FromMilliseconds(100)));
            harness.ViewModel.ObserveVoiceAiButtonInput(
                CreateButtonInput(isPressed: true, receivedAt: startedAt + TimeSpan.FromMilliseconds(200)));
            PumpDispatcherUntil(() => harness.AiService.Contexts.Count == 1, TimeSpan.FromSeconds(2));

            Assert.False(harness.ViewModel.IsVoiceAiRecording);
            Assert.Equal(1, harness.MicrophoneService.StartCount);
        }
        finally
        {
            harness.ViewModel.Dispose();
        }
    }

    /// <summary>
    /// Verifies toggle-to-talk requires a release before the second press can submit.
    /// </summary>
    [Fact]
    public void ToggleToTalk_IgnoresRepeatedPressUntilRelease()
    {
        var harness = CreateHarness(CreateBoundOptions(VoiceAiTalkMode.ToggleToTalk));
        try
        {
            var startedAt = DateTimeOffset.UtcNow;
            harness.ViewModel.ObserveVoiceAiButtonInput(CreateButtonInput(isPressed: true, receivedAt: startedAt));
            harness.ViewModel.ObserveVoiceAiButtonInput(
                CreateButtonInput(isPressed: true, receivedAt: startedAt + TimeSpan.FromMilliseconds(100)));

            Assert.True(harness.ViewModel.IsVoiceAiRecording);
            Assert.Empty(harness.AiService.Contexts);
            Assert.Equal(1, harness.MicrophoneService.StartCount);
        }
        finally
        {
            harness.ViewModel.Dispose();
        }
    }

    /// <summary>
    /// Verifies empty recordings are not submitted to AI.
    /// </summary>
    [Fact]
    public void VoiceAiRecording_WhenEmpty_DoesNotAskAi()
    {
        var harness = CreateHarness(CreateBoundOptions(VoiceAiTalkMode.HoldToTalk));
        harness.MicrophoneService.NextRecording = new VoiceRecordingResult
        {
            HasInput = false,
            WaveBytes = [],
            Duration = TimeSpan.FromMilliseconds(500)
        };

        try
        {
            var startedAt = DateTimeOffset.UtcNow;
            harness.ViewModel.ObserveVoiceAiButtonInput(CreateButtonInput(isPressed: true, receivedAt: startedAt));
            harness.ViewModel.ObserveVoiceAiButtonInput(
                CreateButtonInput(isPressed: false, receivedAt: startedAt + TimeSpan.FromMilliseconds(100)));
            PumpDispatcherUntil(
                () => string.Equals(harness.ViewModel.VoiceAiStatusText, "未检测到语音输入", StringComparison.Ordinal),
                TimeSpan.FromSeconds(2));

            Assert.Empty(harness.AiService.Contexts);
            Assert.Equal("未检测到语音输入", harness.ViewModel.VoiceAiStatusText);
        }
        finally
        {
            harness.ViewModel.Dispose();
        }
    }

    private static DashboardVoiceAiHarness CreateHarness(VoiceAiOptions? voiceAiOptions = null)
    {
        var settingsStore = new FakeAppSettingsStore(voiceAiOptions ?? new VoiceAiOptions { Enabled = true });
        var aiService = new RecordingAiAnalysisService();
        var microphoneService = new FakeMicrophoneService();
        var ttsQueue = new TtsQueue(new RecordingTtsService(), new TtsOptions { TtsEnabled = true });
        var voiceAiQueryService = new VoiceAiQueryService(
            new StubSpeechRecognitionService("我现在该不该进站"),
            aiService,
            new TtsMessageFactory(),
            ttsQueue);
        var viewModel = new DashboardViewModel(
            new FakeUdpListener(),
            new FakePacketDispatcher(),
            new SessionStateStore(new CarStateStore()),
            new LapAnalyzer(),
            new EventDetectionService(),
            aiService,
            settingsStore,
            new FakeUdpRawLogWriter(),
            new TtsMessageFactory(),
            ttsQueue,
            new FakeStoragePersistenceService(),
            Dispatcher.CurrentDispatcher,
            new WindowsVoiceCatalog(() => new WindowsVoiceCatalogResult(Array.Empty<string>(), string.Empty, "No voices.")),
            voiceAiQueryService: voiceAiQueryService,
            microphoneService: microphoneService);
        viewModel.UpdateVoiceAiRawInputStatus("方向盘 Raw Input 已就绪。", isReady: true);
        return new DashboardVoiceAiHarness(viewModel, settingsStore, aiService, microphoneService);
    }

    private static VoiceAiOptions CreateBoundOptions(VoiceAiTalkMode talkMode)
    {
        return new VoiceAiOptions
        {
            Enabled = true,
            InputBinding = CreateButtonInput(isPressed: true).ToBinding(),
            TalkMode = talkMode,
            MicrophoneDeviceId = "0",
            MicrophoneDeviceName = "Test Mic"
        };
    }

    private static VoiceAiButtonInput CreateButtonInput(
        bool isPressed,
        DateTimeOffset? receivedAt = null,
        int? pressedChangeCount = null,
        int changedBitCount = 1)
    {
        return new VoiceAiButtonInput
        {
            DeviceId = "raw-device-1",
            DeviceName = @"\\?\HID#VID_346E&PID_0004#MOZA",
            ButtonIndex = 4,
            ButtonMask = 8,
            IsPressed = isPressed,
            PressedChangeCount = pressedChangeCount ?? (isPressed ? 1 : 0),
            ChangedBitCount = changedBitCount,
            ReceivedAt = receivedAt ?? DateTimeOffset.UtcNow
        };
    }

    private static void PumpDispatcherUntil(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (!condition())
        {
            if (DateTimeOffset.UtcNow > deadline)
            {
                Assert.True(condition(), "Condition was not met before the dispatcher pump timed out.");
            }

            PumpDispatcherFrame();
        }
    }

    private static void PumpDispatcherFrame()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private sealed record DashboardVoiceAiHarness(
        DashboardViewModel ViewModel,
        FakeAppSettingsStore SettingsStore,
        RecordingAiAnalysisService AiService,
        FakeMicrophoneService MicrophoneService);

    private sealed class FakeMicrophoneService : IMicrophoneService
    {
        public int StartCount { get; private set; }

        public VoiceRecordingResult NextRecording { get; set; } = new()
        {
            HasInput = true,
            WaveBytes = [1, 2, 3, 4],
            PeakLevel = 0.5d,
            Duration = TimeSpan.FromSeconds(1)
        };

        public IReadOnlyList<MicrophoneDeviceInfo> GetDevices()
        {
            return
            [
                new MicrophoneDeviceInfo
                {
                    DeviceId = "0",
                    DisplayName = "Test Mic",
                    IsDefault = true
                }
            ];
        }

        public IVoiceRecordingSession StartRecording(string? deviceId)
        {
            StartCount++;
            return new FakeVoiceRecordingSession(NextRecording);
        }

        public Task<MicrophoneTestResult> TestInputAsync(
            string? deviceId,
            TimeSpan duration,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new MicrophoneTestResult
            {
                HasInput = true,
                PeakLevel = 0.7d,
                AverageLevel = 0.3d,
                Duration = duration,
                StatusText = "麦克风输入正常"
            });
        }
    }

    private sealed class FakeVoiceRecordingSession(VoiceRecordingResult recording) : IVoiceRecordingSession
    {
        public Task<VoiceRecordingResult> StopAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(recording);
        }

        public void Dispose()
        {
        }
    }

    private sealed class StubSpeechRecognitionService(string recognizedText) : ISpeechRecognitionService
    {
        public Task<string> RecognizeAsync(VoiceRecordingResult recording, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(recognizedText);
        }
    }

    private sealed class RecordingAiAnalysisService : IAIAnalysisService
    {
        public List<AIAnalysisContext> Contexts { get; } = new();

        public Task<AIAnalysisResult> AnalyzeAsync(
            AIAnalysisContext context,
            AISettings settings,
            CancellationToken cancellationToken = default)
        {
            Contexts.Add(context);
            return Task.FromResult(new AIAnalysisResult
            {
                IsSuccess = true,
                Tts = "两圈后进站。"
            });
        }
    }

    private sealed class FakeAppSettingsStore : IAppSettingsStore
    {
        private readonly VoiceAiOptions _voiceAiOptions;

        public FakeAppSettingsStore(VoiceAiOptions voiceAiOptions)
        {
            _voiceAiOptions = voiceAiOptions;
        }

        public VoiceAiOptions? LastVoiceAiOptions { get; private set; }

        public Task<AppSettingsDocument> LoadAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AppSettingsDocument
            {
                Ai = new AISettings
                {
                    AiEnabled = true,
                    ApiKey = "test-key"
                },
                Tts = new TtsOptions
                {
                    TtsEnabled = true,
                    CooldownSeconds = 1
                },
                VoiceAi = _voiceAiOptions
            });
        }

        public Task SaveAiSettingsAsync(AISettings settings, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SaveTtsSettingsAsync(TtsOptions options, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SaveRaceWeekendTyrePlanAsync(RaceWeekendTyrePlan plan, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SaveUdpRawLogOptionsAsync(UdpRawLogOptions options, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SaveVoiceAiOptionsAsync(VoiceAiOptions options, CancellationToken cancellationToken = default)
        {
            LastVoiceAiOptions = options;
            return Task.CompletedTask;
        }

        public Task SaveUdpSettingsAsync(UdpSettings settings, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUdpListener : IUdpListener
    {
        public event EventHandler<UdpDatagram>? DatagramReceived
        {
            add { }
            remove { }
        }

        public event EventHandler<Exception>? ReceiveFaulted
        {
            add { }
            remove { }
        }

        public bool IsListening { get; private set; }

        public int? ListeningPort { get; private set; }

        public Task StartAsync(int port, CancellationToken cancellationToken = default)
        {
            IsListening = true;
            ListeningPort = port;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            IsListening = false;
            ListeningPort = null;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakePacketDispatcher : IPacketDispatcher<PacketId, PacketHeader>
    {
        public event EventHandler<PacketDispatchResult<PacketId, PacketHeader>>? PacketDispatched
        {
            add { }
            remove { }
        }

        public bool TryDispatch(UdpDatagram datagram, out string? error)
        {
            error = null;
            return false;
        }
    }

    private sealed class FakeUdpRawLogWriter : IUdpRawLogWriter
    {
        public UdpRawLogStatus Status { get; private set; } = new();

        public void UpdateOptions(UdpRawLogOptions options)
        {
            Status = Status with { Enabled = options.Enabled, DirectoryPath = options.DirectoryPath };
        }

        public void TryEnqueue(UdpDatagram datagram)
        {
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeStoragePersistenceService : IStoragePersistenceService
    {
        public event EventHandler<string>? LogEmitted
        {
            add { }
            remove { }
        }

        public void ObserveParsedPacket(ParsedPacket parsedPacket)
        {
        }

        public void EnqueueLapSummary(LapSummary lapSummary)
        {
        }

        public void EnqueueRaceEvent(RaceEvent raceEvent)
        {
        }

        public void EnqueueAiReport(int lapNumber, AIAnalysisResult analysisResult)
        {
        }

        public Task CompleteActiveSessionAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingTtsService : ITtsService
    {
        public void Configure(string? voiceName, int volume, int rate)
        {
        }

        public Task SpeakAsync(string text, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
