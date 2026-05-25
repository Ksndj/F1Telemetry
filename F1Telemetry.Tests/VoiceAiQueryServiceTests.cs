using System.IO;
using System.Text.Json;
using F1Telemetry.AI.Interfaces;
using F1Telemetry.AI.Models;
using F1Telemetry.AI.Services;
using F1Telemetry.Analytics.Strategy;
using F1Telemetry.App.Logging;
using F1Telemetry.App.Services;
using F1Telemetry.Core.Interfaces;
using F1Telemetry.Core.Models;
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

    /// <summary>
    /// Verifies cancellation stops an in-flight strategy answer before TTS.
    /// </summary>
    [Fact]
    public async Task AskTextAsync_WhenCanceled_PropagatesCancellation()
    {
        var speech = new StubSpeechRecognitionService(string.Empty);
        var ai = new BlockingAiAnalysisService();
        using var queue = new TtsQueue(new RecordingTtsService(), new TtsOptions { TtsEnabled = true });
        var service = new VoiceAiQueryService(speech, ai, new TtsMessageFactory(), queue);
        using var cts = new CancellationTokenSource();

        var task = service.AskTextAsync(CreateStrategyRequest("现在进站吗", adviceKey: "voice-ai:cancel"), cts.Token);
        await ai.Started.Task;
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    /// <summary>
    /// Verifies stale session answers are ignored after the AI returns.
    /// </summary>
    [Fact]
    public async Task AskTextAsync_WhenSessionUidChanges_IgnoresOldAnswer()
    {
        var speech = new StubSpeechRecognitionService(string.Empty);
        var ai = new RecordingAiAnalysisService();
        using var queue = new TtsQueue(new RecordingTtsService(), new TtsOptions { TtsEnabled = true });
        var service = new VoiceAiQueryService(speech, ai, new TtsMessageFactory(), queue);
        var request = CreateStrategyRequest("现在进站吗", adviceKey: "voice-ai:session") with
        {
            CaptureCurrentSessionUid = () => 999
        };

        var result = await service.AskTextAsync(request);

        Assert.False(result.IsSuccess);
        Assert.True(result.WasIgnoredBecauseSessionChanged);
        Assert.Equal("会话已变化，已忽略旧回答", result.ErrorMessage);
        Assert.False(result.WasQueuedForSpeech);
    }

    /// <summary>
    /// Verifies network or API failures still produce rule-based fallback advice.
    /// </summary>
    [Fact]
    public async Task AskTextAsync_WhenAiFails_UsesRuleBasedFallback()
    {
        var speech = new StubSpeechRecognitionService(string.Empty);
        var ai = new FailingAiAnalysisService(AIErrorMessageFormatter.NetworkError);
        using var queue = new TtsQueue(new RecordingTtsService(), new TtsOptions { TtsEnabled = true });
        var service = new VoiceAiQueryService(speech, ai, new TtsMessageFactory(), queue);

        var result = await service.AskTextAsync(CreateStrategyRequest("ERS怎么用", adviceKey: "voice-ai:fallback"));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Advice);
        Assert.True(result.Advice!.IsFallback);
        Assert.Contains("省电", result.SpeechText, StringComparison.Ordinal);
        Assert.True(result.WasQueuedForSpeech);
    }

    /// <summary>
    /// Verifies invalid structured AI output is surfaced as a failure.
    /// </summary>
    [Fact]
    public async Task AskTextAsync_WhenAiReturnsInvalidFormat_ShowsFailureReason()
    {
        var speech = new StubSpeechRecognitionService(string.Empty);
        var ai = new FailingAiAnalysisService("AI 返回格式无效");
        using var queue = new TtsQueue(new RecordingTtsService(), new TtsOptions { TtsEnabled = true });
        var service = new VoiceAiQueryService(speech, ai, new TtsMessageFactory(), queue);

        var result = await service.AskTextAsync(CreateStrategyRequest("现在进站吗", adviceKey: "voice-ai:invalid"));

        Assert.False(result.IsSuccess);
        Assert.Equal("AI 返回格式无效", result.ErrorMessage);
        Assert.False(result.WasQueuedForSpeech);
    }

    /// <summary>
    /// Verifies no-telemetry strategy answers stay text-only and explain the skipped speech.
    /// </summary>
    [Fact]
    public async Task AskTextAsync_WhenNoTelemetry_DoesNotQueueTts()
    {
        var speech = new StubSpeechRecognitionService(string.Empty);
        var ai = new RecordingAiAnalysisService();
        using var queue = new TtsQueue(new RecordingTtsService(), new TtsOptions { TtsEnabled = true });
        var service = new VoiceAiQueryService(speech, ai, new TtsMessageFactory(), queue);

        var result = await service.AskTextAsync(
            CreateStrategyRequest("现在进站吗", adviceKey: "voice-ai:no-telemetry", mode: RaceAssistantMode.NoTelemetry));

        Assert.True(result.IsSuccess);
        Assert.False(result.WasQueuedForSpeech);
        Assert.Equal("缺少实时遥测", result.SpeechSkippedReason);
    }

    /// <summary>
    /// Verifies a successful RaceAssistant answer writes an audit record with correlation ids.
    /// </summary>
    [Fact]
    public async Task AskTextAsync_WhenAiSucceeds_WritesAuditRecord()
    {
        var directory = CreateTempDirectory();
        await using var auditLogger = new RaceAssistantAuditLogger(new AppRunContext("run-voice", DateTimeOffset.Now), directory);
        var speech = new StubSpeechRecognitionService(string.Empty);
        var ai = new RecordingAiAnalysisService();
        using var queue = new TtsQueue(new RecordingTtsService(), new TtsOptions { TtsEnabled = true });
        var service = new VoiceAiQueryService(speech, ai, new TtsMessageFactory(), queue, auditLogger);

        var result = await service.AskTextAsync(CreateStrategyRequest("现在进站吗", adviceKey: "voice-ai:audit") with
        {
            Track = "Monza",
            SessionType = "正赛",
            UdpRawLogFile = @"C:\Users\driver\AppData\Roaming\F1Telemetry\.logs\udp\f1telemetry-udp.jsonl"
        });
        await auditLogger.FlushAsync(TimeSpan.FromSeconds(2));

        Assert.True(result.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(result.QuestionId));
        using var json = await ReadSingleAuditJsonAsync(directory);
        var root = json.RootElement;
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("run-voice", root.GetProperty("runId").GetString());
        Assert.Equal(result.QuestionId, root.GetProperty("questionId").GetString());
        Assert.Equal("f1telemetry-udp.jsonl", root.GetProperty("udpRawLogFile").GetString());
        Assert.False(root.TryGetProperty("promptSummary", out _));
        Assert.True(root.GetProperty("ttsQueued").GetBoolean());
    }

    /// <summary>
    /// Verifies prompt summary auditing is opt-in and compact.
    /// </summary>
    [Fact]
    public async Task AskTextAsync_WhenPromptSummaryEnabled_WritesCompactPromptSummary()
    {
        var directory = CreateTempDirectory();
        await using var auditLogger = new RaceAssistantAuditLogger(new AppRunContext("run-prompt", DateTimeOffset.Now), directory);
        auditLogger.UpdateSettings(new LogSettings { RaceAssistantLogPromptSummary = true });
        var speech = new StubSpeechRecognitionService(string.Empty);
        var ai = new RecordingAiAnalysisService();
        using var queue = new TtsQueue(new RecordingTtsService(), new TtsOptions { TtsEnabled = true });
        var service = new VoiceAiQueryService(speech, ai, new TtsMessageFactory(), queue, auditLogger);

        var result = await service.AskTextAsync(CreateStrategyRequest("现在进站吗", adviceKey: "voice-ai:audit-prompt"));
        await auditLogger.FlushAsync(TimeSpan.FromSeconds(2));

        Assert.True(result.IsSuccess);
        using var json = await ReadSingleAuditJsonAsync(directory);
        var summary = json.RootElement.GetProperty("promptSummary").GetString();
        Assert.Contains("intent=", summary, StringComparison.Ordinal);
        Assert.Contains("mode=", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("现在进站吗", summary, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies AI failures still write an audit record for the fallback answer.
    /// </summary>
    [Fact]
    public async Task AskTextAsync_WhenAiFails_WritesFallbackAuditRecord()
    {
        var directory = CreateTempDirectory();
        await using var auditLogger = new RaceAssistantAuditLogger(new AppRunContext("run-fallback", DateTimeOffset.Now), directory);
        var speech = new StubSpeechRecognitionService(string.Empty);
        var ai = new FailingAiAnalysisService(AIErrorMessageFormatter.NetworkError);
        using var queue = new TtsQueue(new RecordingTtsService(), new TtsOptions { TtsEnabled = true });
        var service = new VoiceAiQueryService(speech, ai, new TtsMessageFactory(), queue, auditLogger);

        var result = await service.AskTextAsync(CreateStrategyRequest("ERS怎么用", adviceKey: "voice-ai:audit-fallback"));
        await auditLogger.FlushAsync(TimeSpan.FromSeconds(2));

        Assert.True(result.IsSuccess);
        using var json = await ReadSingleAuditJsonAsync(directory);
        Assert.True(json.RootElement.GetProperty("usedFallback").GetBoolean());
        Assert.Contains("网络", json.RootElement.GetProperty("failureReason").GetString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies no-telemetry answers audit skipped speech.
    /// </summary>
    [Fact]
    public async Task AskTextAsync_WhenNoTelemetry_WritesSkippedSpeechAuditRecord()
    {
        var directory = CreateTempDirectory();
        await using var auditLogger = new RaceAssistantAuditLogger(new AppRunContext("run-no-telemetry", DateTimeOffset.Now), directory);
        var speech = new StubSpeechRecognitionService(string.Empty);
        var ai = new RecordingAiAnalysisService();
        using var queue = new TtsQueue(new RecordingTtsService(), new TtsOptions { TtsEnabled = true });
        var service = new VoiceAiQueryService(speech, ai, new TtsMessageFactory(), queue, auditLogger);

        var result = await service.AskTextAsync(
            CreateStrategyRequest("现在进站吗", adviceKey: "voice-ai:audit-no-telemetry", mode: RaceAssistantMode.NoTelemetry));
        await auditLogger.FlushAsync(TimeSpan.FromSeconds(2));

        Assert.True(result.IsSuccess);
        using var json = await ReadSingleAuditJsonAsync(directory);
        Assert.False(json.RootElement.GetProperty("ttsQueued").GetBoolean());
        Assert.Equal("缺少实时遥测", json.RootElement.GetProperty("speechSkippedReason").GetString());
    }

    /// <summary>
    /// Verifies audit write failures do not affect the answer result.
    /// </summary>
    [Fact]
    public async Task AskTextAsync_WhenAuditWriteFails_DoesNotFailQuestion()
    {
        var root = CreateTempDirectory();
        var invalidDirectory = Path.Combine(root, "not-a-directory");
        await File.WriteAllTextAsync(invalidDirectory, "blocked");
        var auditLogger = new RaceAssistantAuditLogger(new AppRunContext("run-write-fail", DateTimeOffset.Now), invalidDirectory);
        var speech = new StubSpeechRecognitionService(string.Empty);
        var ai = new RecordingAiAnalysisService();
        using var queue = new TtsQueue(new RecordingTtsService(), new TtsOptions { TtsEnabled = true });
        var service = new VoiceAiQueryService(speech, ai, new TtsMessageFactory(), queue, auditLogger);

        var result = await service.AskTextAsync(CreateStrategyRequest("现在进站吗", adviceKey: "voice-ai:audit-write-fail"));
        await auditLogger.FlushAsync(TimeSpan.FromMilliseconds(500));
        await auditLogger.DisposeAsync();

        Assert.True(result.IsSuccess);
        Assert.Contains("日志", auditLogger.Status.LastWarning, StringComparison.Ordinal);
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
            AdviceKey = "voice-ai:test",
            Recording = new VoiceRecordingResult
            {
                HasInput = true,
                WaveBytes = [1, 2, 3, 4],
                PeakLevel = 0.4d,
                Duration = TimeSpan.FromSeconds(1)
            }
        };
    }

    private static async Task<JsonDocument> ReadSingleAuditJsonAsync(string directory)
    {
        var file = Assert.Single(Directory.EnumerateFiles(directory, "race-assistant-*.jsonl"));
        await using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        var lines = (await reader.ReadToEndAsync()).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        var line = Assert.Single(lines);
        return JsonDocument.Parse(line);
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "F1TelemetryTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static VoiceAiQueryRequest CreateStrategyRequest(
        string question,
        string adviceKey,
        RaceAssistantMode mode = RaceAssistantMode.RaceStintManagement)
    {
        var context = new StrategyQuestionContext
        {
            SessionUid = 123,
            Question = question,
            Intent = question.Contains("ERS", StringComparison.OrdinalIgnoreCase)
                ? VoiceQuestionIntent.ERS_STRATEGY
                : VoiceQuestionIntent.PIT_DECISION,
            Mode = mode,
            RequiredData = ["ers-store-energy", "tyre-wear"],
            Snapshot = new RaceAssistantSnapshot
            {
                SessionUid = 123,
                Mode = mode,
                Quality = mode == RaceAssistantMode.NoTelemetry
                    ? new SnapshotQuality
                    {
                        IsStale = true,
                        MissingData = ["fresh-snapshot"],
                        MaxRecommendedConfidence = StrategyAdviceConfidence.Low
                    }
                    : new SnapshotQuality { MaxRecommendedConfidence = StrategyAdviceConfidence.High },
                RuleSignals =
                [
                    new StrategyRuleSignal
                    {
                        SignalType = "low-ers",
                        AdviceType = RaceAssistantAdviceType.ErsManagement,
                        Summary = "ERS 储能偏低。",
                        RecommendedAction = "ERS偏低，直道先省电。",
                        Confidence = StrategyAdviceConfidence.High,
                        RiskLevel = StrategyRiskLevel.Medium,
                        RequiredData = ["ers-store-energy"]
                    }
                ]
            }
        };

        return new VoiceAiQueryRequest
        {
            BaseContext = new AIAnalysisContext(),
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
            AdviceKey = adviceKey,
            QuestionText = question,
            StrategyQuestionContext = context,
            CaptureCurrentSessionUid = () => 123,
            EnableTtsAnswer = true
        };
    }

    private sealed class StubSpeechRecognitionService : ISpeechRecognitionService
    {
        private readonly string _recognizedText;

        public StubSpeechRecognitionService(string recognizedText)
        {
            _recognizedText = recognizedText;
        }

        public Task<string> RecognizeAsync(VoiceRecordingResult recording, CancellationToken cancellationToken = default)
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
                Tts = "继续保胎两圈，等窗口再进站。",
                Summary = "胎速稳定",
                RecommendedAction = "暂不进",
                Confidence = "High",
                RiskLevel = "Low",
                AdviceType = "PitWindow"
            });
        }
    }

    private sealed class FailingAiAnalysisService(string errorMessage) : IAIAnalysisService
    {
        public Task<AIAnalysisResult> AnalyzeAsync(
            AIAnalysisContext context,
            AISettings settings,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AIAnalysisResult
            {
                IsSuccess = false,
                ErrorMessage = errorMessage
            });
        }
    }

    private sealed class BlockingAiAnalysisService : IAIAnalysisService
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<AIAnalysisResult> AnalyzeAsync(
            AIAnalysisContext context,
            AISettings settings,
            CancellationToken cancellationToken = default)
        {
            Started.TrySetResult();
            await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
            return new AIAnalysisResult { IsSuccess = true, Tts = "不应返回。" };
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
