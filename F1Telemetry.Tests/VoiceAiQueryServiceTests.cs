using F1Telemetry.AI.Interfaces;
using F1Telemetry.AI.Models;
using F1Telemetry.AI.Services;
using F1Telemetry.App.Services;
using F1Telemetry.Core.Interfaces;
using F1Telemetry.TTS.Models;
using F1Telemetry.TTS.Services;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies microphone-triggered AI race engineer queries.
/// </summary>
public sealed class VoiceAiQueryServiceTests
{
    /// <summary>
    /// Verifies recognized speech is passed into a realtime AI context and queued for TTS.
    /// </summary>
    [Fact]
    public async Task AskAsync_WithRecognizedQuestion_AsksAiAndQueuesSpeech()
    {
        var speech = new StubSpeechRecognitionService("我现在该不该进站");
        var ai = new RecordingAiAnalysisService();
        using var queue = new TtsQueue(new RecordingTtsService(), new TtsOptions { TtsEnabled = true });
        var service = new VoiceAiQueryService(speech, ai, new TtsMessageFactory(), queue);

        var result = await service.AskAsync(CreateRequest());

        Assert.True(result.IsSuccess);
        Assert.Equal("我现在该不该进站", result.RecognizedQuestion);
        Assert.Equal("继续保胎两圈，等窗口再进站。", result.SpeechText);
        Assert.True(result.WasQueuedForSpeech);
        var context = Assert.Single(ai.Contexts);
        Assert.Contains("我现在该不该进站", context.RealtimeEngineerAdviceSummary, StringComparison.Ordinal);
        Assert.Contains("语音问答", context.SessionFocusText, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies empty microphone recognition does not call the AI service.
    /// </summary>
    [Fact]
    public async Task AskAsync_WithoutRecognizedSpeech_DoesNotAskAi()
    {
        var speech = new StubSpeechRecognitionService(string.Empty);
        var ai = new RecordingAiAnalysisService();
        using var queue = new TtsQueue(new RecordingTtsService(), new TtsOptions { TtsEnabled = true });
        var service = new VoiceAiQueryService(speech, ai, new TtsMessageFactory(), queue);

        var result = await service.AskAsync(CreateRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal("未识别到语音问题", result.ErrorMessage);
        Assert.Empty(ai.Contexts);
    }

    private static VoiceAiQueryRequest CreateRequest()
    {
        return new VoiceAiQueryRequest
        {
            BaseContext = new AIAnalysisContext
            {
                SessionTypeText = "正赛",
                CurrentFuelRemainingLaps = 7.2f,
                CurrentTyre = "Medium",
                PositionStrategySummary = "当前 P6/20，前车差 1.2s，后车差 3.4s"
            },
            AiSettings = new AISettings
            {
                AiEnabled = true,
                ApiKey = "test-key"
            },
            TtsOptions = new TtsOptions
            {
                TtsEnabled = true,
                CooldownSeconds = 1
            },
            AdviceKey = "voice-ai:test"
        };
    }

    private sealed class StubSpeechRecognitionService : ISpeechRecognitionService
    {
        private readonly string _recognizedText;

        public StubSpeechRecognitionService(string recognizedText)
        {
            _recognizedText = recognizedText;
        }

        public Task<string> RecognizeOnceAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_recognizedText);
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
                Tts = "继续保胎两圈，等窗口再进站。"
            });
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
