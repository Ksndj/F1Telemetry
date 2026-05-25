using System.Diagnostics;
using F1Telemetry.AI.Interfaces;
using F1Telemetry.AI.Models;
using F1Telemetry.AI.Services;
using F1Telemetry.Analytics.Strategy;
using F1Telemetry.App.Logging;
using F1Telemetry.Core.Interfaces;
using F1Telemetry.TTS.Services;

namespace F1Telemetry.App.Services;

/// <summary>
/// Coordinates microphone recognition, live race-context AI requests, and optional TTS playback.
/// </summary>
public sealed class VoiceAiQueryService
{
    private readonly ISpeechRecognitionService _speechRecognitionService;
    private readonly IAIAnalysisService _aiAnalysisService;
    private readonly TtsMessageFactory _ttsMessageFactory;
    private readonly TtsQueue _ttsQueue;
    private readonly RuleBasedFallbackAdviceService _fallbackAdviceService;
    private readonly StrategyRuleConflictResolver _conflictResolver;
    private readonly RaceAssistantAuditLogger? _raceAssistantAuditLogger;

    /// <summary>
    /// Initializes a voice AI query service.
    /// </summary>
    /// <param name="speechRecognitionService">The microphone speech recognizer.</param>
    /// <param name="aiAnalysisService">The AI analysis service.</param>
    /// <param name="ttsMessageFactory">The TTS message factory.</param>
    /// <param name="ttsQueue">The TTS queue.</param>
    public VoiceAiQueryService(
        ISpeechRecognitionService speechRecognitionService,
        IAIAnalysisService aiAnalysisService,
        TtsMessageFactory ttsMessageFactory,
        TtsQueue ttsQueue,
        RaceAssistantAuditLogger? raceAssistantAuditLogger = null)
    {
        _speechRecognitionService = speechRecognitionService ?? throw new ArgumentNullException(nameof(speechRecognitionService));
        _aiAnalysisService = aiAnalysisService ?? throw new ArgumentNullException(nameof(aiAnalysisService));
        _ttsMessageFactory = ttsMessageFactory ?? throw new ArgumentNullException(nameof(ttsMessageFactory));
        _ttsQueue = ttsQueue ?? throw new ArgumentNullException(nameof(ttsQueue));
        _raceAssistantAuditLogger = raceAssistantAuditLogger;
        _fallbackAdviceService = new RuleBasedFallbackAdviceService();
        _conflictResolver = new StrategyRuleConflictResolver();
    }

    /// <summary>
    /// Recognizes one driver question, asks AI with the supplied race context, and queues the short answer for speech.
    /// </summary>
    /// <param name="request">The live voice query request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<VoiceAiQueryResult> AskAsync(
        VoiceAiQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string question;
        try
        {
            if (!request.Recording.HasInput || request.Recording.WaveBytes.Length == 0)
            {
                return CreateFailure("未检测到语音输入");
            }

            question = await _speechRecognitionService.RecognizeAsync(request.Recording, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return CreateFailure($"麦克风识别失败：{ex.Message}");
        }

        if (string.IsNullOrWhiteSpace(question))
        {
            return CreateFailure("未识别到语音问题");
        }

        return await AskQuestionCoreAsync(question, request, cancellationToken);
    }

    /// <summary>
    /// Asks the race assistant with a typed fallback question.
    /// </summary>
    /// <param name="request">The typed question request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<VoiceAiQueryResult> AskTextAsync(
        VoiceAiQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.QuestionText))
        {
            return Task.FromResult(CreateFailure("请输入问题"));
        }

        return AskQuestionCoreAsync(request.QuestionText, request, cancellationToken);
    }

    private async Task<VoiceAiQueryResult> AskQuestionCoreAsync(
        string question,
        VoiceAiQueryRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var questionId = CreateQuestionId();
        var aiStopwatch = Stopwatch.StartNew();
        var aiCalled = false;
        var aiSucceeded = false;
        var usedFallback = false;
        var failureReason = string.Empty;
        var strategyContext = ResolveStrategyContext(question, request);
        if (strategyContext is null)
        {
            return await AskLegacyAsync(question, questionId, request, cancellationToken);
        }

        if (!request.AiSettings.AiEnabled)
        {
            usedFallback = true;
            failureReason = "AI 未启用";
            var aiDisabledSpeechSkippedReason = request.EnableTtsAnswer && !CanQueueRaceAssistantSpeech(strategyContext)
                ? "缺少实时遥测"
                : string.Empty;
            var result = CreateRaceAssistantSuccess(
                question,
                questionId,
                strategyContext,
                _fallbackAdviceService.BuildFallback(strategyContext, "AI 未启用"),
                request,
                wasQueued: false,
                speechSkippedReason: aiDisabledSpeechSkippedReason);
            AuditRaceAssistant(
                question,
                questionId,
                request,
                strategyContext,
                result,
                usedFallback,
                failureReason,
                aiCalled,
                aiSucceeded,
                aiStopwatch.ElapsedMilliseconds);
            return result;
        }

        StrategyAdviceResult advice;
        if (string.IsNullOrWhiteSpace(request.AiSettings.ApiKey))
        {
            usedFallback = true;
            failureReason = AIErrorMessageFormatter.MissingApiKey;
            advice = _fallbackAdviceService.BuildFallback(strategyContext, AIErrorMessageFormatter.MissingApiKey);
        }
        else
        {
            try
            {
                var aiContext = request.BaseContext with
                {
                    SessionFocusText = "语音问答：直接回答车手当前问题，优先使用压缩策略摘要；只输出中文结构化 JSON。",
                    RealtimeEngineerAdviceSummary = BuildRealtimeInstruction(question),
                    StrategyQuestionContext = strategyContext
                };
                aiCalled = true;
                var result = await _aiAnalysisService.AnalyzeAsync(aiContext, request.AiSettings, cancellationToken);
                aiSucceeded = result.IsSuccess;
                if (!result.IsSuccess && IsInvalidAiFormat(result.ErrorMessage))
                {
                    failureReason = result.ErrorMessage;
                    var failure = CreateRaceAssistantFailure(question, questionId, strategyContext, result.ErrorMessage);
                    AuditRaceAssistant(
                        question,
                        questionId,
                        request,
                        strategyContext,
                        failure,
                        usedFallback: false,
                        failureReason,
                        aiCalled,
                        aiSucceeded,
                        aiStopwatch.ElapsedMilliseconds);
                    return failure;
                }

                usedFallback = !result.IsSuccess;
                failureReason = result.IsSuccess ? string.Empty : result.ErrorMessage;
                advice = result.IsSuccess
                    ? ConvertAiResultToAdvice(result, strategyContext)
                    : _fallbackAdviceService.BuildFallback(strategyContext, result.ErrorMessage);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                usedFallback = true;
                failureReason = ex.Message;
                advice = _fallbackAdviceService.BuildFallback(strategyContext, ex.Message);
            }
        }

        var resolvedAdvice = _conflictResolver.Resolve(advice, strategyContext);
        advice = resolvedAdvice with
        {
            Tts = StrategyAdviceJsonParser.CompressTts(resolvedAdvice.Tts)
        };

        cancellationToken.ThrowIfCancellationRequested();
        var currentSessionUid = request.CaptureCurrentSessionUid?.Invoke();
        if (strategyContext.SessionUid is not null &&
            currentSessionUid is not null &&
            strategyContext.SessionUid != currentSessionUid)
        {
            var staleResult = new VoiceAiQueryResult
            {
                IsSuccess = false,
                RecognizedQuestion = question.Trim(),
                QuestionId = questionId,
                SessionUid = strategyContext.SessionUid,
                Intent = strategyContext.Intent,
                Mode = strategyContext.Mode,
                Advice = advice,
                ErrorMessage = "会话已变化，已忽略旧回答",
                WasIgnoredBecauseSessionChanged = true
            };
            AuditRaceAssistant(
                question,
                questionId,
                request,
                strategyContext,
                staleResult,
                usedFallback,
                "会话已变化，已忽略旧回答",
                aiCalled,
                aiSucceeded,
                aiStopwatch.ElapsedMilliseconds);
            return staleResult;
        }

        var wasQueued = false;
        var speechSkippedReason = string.Empty;
        if (request.EnableTtsAnswer && CanQueueRaceAssistantSpeech(strategyContext))
        {
            wasQueued = TryQueueSpeech(request, advice.Tts);
        }
        else if (request.EnableTtsAnswer)
        {
            speechSkippedReason = "缺少实时遥测";
        }

        var success = CreateRaceAssistantSuccess(question, questionId, strategyContext, advice, request, wasQueued, speechSkippedReason);
        AuditRaceAssistant(
            question,
            questionId,
            request,
            strategyContext,
            success,
            usedFallback,
            failureReason,
            aiCalled,
            aiSucceeded,
            aiStopwatch.ElapsedMilliseconds);
        return success;
    }

    private async Task<VoiceAiQueryResult> AskLegacyAsync(
        string question,
        string questionId,
        VoiceAiQueryRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.AiSettings.AiEnabled)
        {
            return CreateFailure("AI 未启用");
        }

        if (string.IsNullOrWhiteSpace(request.AiSettings.ApiKey))
        {
            return CreateFailure(AIErrorMessageFormatter.MissingApiKey);
        }

        var context = request.BaseContext with
        {
            SessionFocusText = "语音问答：直接回答车手当前问题，优先使用当前比赛数据、轮胎、燃油、ERS、位置和天气；只输出中文。",
            RealtimeEngineerAdviceSummary = BuildRealtimeInstruction(question)
        };
        var result = await _aiAnalysisService.AnalyzeAsync(context, request.AiSettings, cancellationToken);
        if (!result.IsSuccess)
        {
            return CreateFailure(result.ErrorMessage);
        }

        var speechText = SelectSpeechText(result);
        var wasQueued = request.EnableTtsAnswer && TryQueueSpeech(request, speechText);

        return new VoiceAiQueryResult
        {
            IsSuccess = true,
            RecognizedQuestion = question.Trim(),
            QuestionId = questionId,
            SpeechText = speechText,
            WasQueuedForSpeech = wasQueued
        };
    }

    private StrategyQuestionContext? ResolveStrategyContext(string question, VoiceAiQueryRequest request)
    {
        if (request.StrategyQuestionContext is not null)
        {
            return request.StrategyQuestionContext;
        }

        return request.BuildStrategyQuestionContext?.Invoke(question.Trim());
    }

    private static StrategyAdviceResult ConvertAiResultToAdvice(
        AIAnalysisResult result,
        StrategyQuestionContext context)
    {
        var confidence = ParseEnum(result.Confidence, StrategyAdviceConfidence.Low);
        var tts = StrategyAdviceJsonParser.CompressTts(SelectSpeechText(result));
        return new StrategyAdviceResult
        {
            AdviceType = ParseEnum(result.AdviceType, RaceAssistantAdviceType.Unknown),
            Summary = TrimOrFallback(result.Summary, tts),
            Reason = result.Reason?.Trim() ?? string.Empty,
            RecommendedAction = TrimOrFallback(result.RecommendedAction, tts),
            Confidence = confidence,
            RiskLevel = ParseEnum(result.RiskLevel, StrategyRiskLevel.Unknown),
            RequiredData = result.RequiredData.Count > 0 ? result.RequiredData : context.RequiredData,
            MissingData = result.MissingData.Count > 0 ? result.MissingData : context.MissingData,
            Tts = tts,
            Warnings = result.Warnings
        };
    }

    private static T ParseEnum<T>(string? value, T fallback)
        where T : struct, Enum
    {
        return Enum.TryParse<T>(value, ignoreCase: true, out var parsed) ? parsed : fallback;
    }

    private static string TrimOrFallback(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private bool TryQueueSpeech(VoiceAiQueryRequest request, string speechText)
    {
        var message = _ttsMessageFactory.CreateForEngineerAdvice(
            string.IsNullOrWhiteSpace(request.AdviceKey) ? $"voice-ai:{Guid.NewGuid():N}" : request.AdviceKey,
            speechText,
            request.TtsOptions);
        return message is not null && _ttsQueue.TryEnqueue(message);
    }

    private static bool CanQueueRaceAssistantSpeech(StrategyQuestionContext context)
    {
        return context.Mode is not RaceAssistantMode.NoTelemetry and not RaceAssistantMode.WaitingForTelemetry &&
               !context.Snapshot.Quality.IsStale &&
               !context.MissingData.Contains("fresh-snapshot", StringComparer.Ordinal);
    }

    private static bool IsInvalidAiFormat(string? errorMessage)
    {
        return !string.IsNullOrWhiteSpace(errorMessage) &&
               errorMessage.Contains("格式无效", StringComparison.Ordinal);
    }

    private static VoiceAiQueryResult CreateRaceAssistantFailure(
        string question,
        string questionId,
        StrategyQuestionContext context,
        string errorMessage)
    {
        return new VoiceAiQueryResult
        {
            IsSuccess = false,
            RecognizedQuestion = question.Trim(),
            QuestionId = questionId,
            SessionUid = context.SessionUid,
            Intent = context.Intent,
            Mode = context.Mode,
            ErrorMessage = string.IsNullOrWhiteSpace(errorMessage) ? AIErrorMessageFormatter.ParseFailure : errorMessage.Trim()
        };
    }

    private static VoiceAiQueryResult CreateRaceAssistantSuccess(
        string question,
        string questionId,
        StrategyQuestionContext context,
        StrategyAdviceResult advice,
        VoiceAiQueryRequest request,
        bool wasQueued,
        string speechSkippedReason = "")
    {
        var speechText = StrategyAdviceJsonParser.CompressTts(advice.Tts);
        var answer = request.MaxAnswerLength > 0 && speechText.Length > request.MaxAnswerLength
            ? speechText[..request.MaxAnswerLength]
            : speechText;
        return new VoiceAiQueryResult
        {
            IsSuccess = true,
            RecognizedQuestion = question.Trim(),
            QuestionId = questionId,
            SessionUid = context.SessionUid,
            Intent = context.Intent,
            Mode = context.Mode,
            Advice = advice with { Tts = speechText },
            SpeechText = answer,
            WasQueuedForSpeech = wasQueued,
            SpeechSkippedReason = speechSkippedReason
        };
    }

    private void AuditRaceAssistant(
        string question,
        string questionId,
        VoiceAiQueryRequest request,
        StrategyQuestionContext context,
        VoiceAiQueryResult result,
        bool usedFallback,
        string failureReason,
        bool aiCalled,
        bool aiSucceeded,
        long aiLatencyMs)
    {
        if (_raceAssistantAuditLogger is null)
        {
            return;
        }

        try
        {
            var timestamp = DateTimeOffset.Now;
            var runContext = _raceAssistantAuditLogger.RunContext;
            _raceAssistantAuditLogger.TryEnqueue(new RaceAssistantAuditRecord
            {
                SchemaVersion = 1,
                RunId = runContext.RunId,
                QuestionId = questionId,
                Timestamp = timestamp,
                ElapsedMsSinceRunStart = runContext.GetElapsedMilliseconds(timestamp),
                SessionUid = context.SessionUid,
                Track = request.Track,
                SessionType = request.SessionType,
                Lap = context.Snapshot.CurrentLap,
                UdpRawLogFile = request.UdpRawLogFile,
                Question = context.Question,
                RecognizedText = question,
                Intent = context.Intent.ToString(),
                IntentDisplayName = string.IsNullOrWhiteSpace(context.IntentDisplayName)
                    ? context.Intent.ToString()
                    : context.IntentDisplayName,
                Mode = context.Mode.ToString(),
                ModeDisplayName = string.IsNullOrWhiteSpace(context.ModeDisplayName)
                    ? context.Mode.ToString()
                    : context.ModeDisplayName,
                SnapshotAgeMs = context.Snapshot.Quality.AgeSeconds is null
                    ? null
                    : context.Snapshot.Quality.AgeSeconds.Value * 1000,
                MissingData = context.MissingData,
                RuleSignals = context.Snapshot.RuleSignals.Select(ToAuditSignal).ToArray(),
                PitDecisionSignal = ToAuditSignal(context.Snapshot.PitDecision.Signal),
                SafetyCarPitOpportunitySignal = ToAuditSignal(context.Snapshot.SafetyCarPitOpportunity.Signal),
                Result = ToAuditResult(result.Advice),
                UsedFallback = usedFallback || result.Advice?.IsFallback == true,
                FailureReason = string.IsNullOrWhiteSpace(failureReason) ? result.ErrorMessage : failureReason,
                TtsQueued = result.WasQueuedForSpeech,
                SpeechSkippedReason = result.SpeechSkippedReason,
                AiLatencyMs = aiCalled ? aiLatencyMs : null
            });
        }
        catch
        {
            // Audit logging must never affect the race-assistant answer path.
        }
    }

    private static RaceAssistantAuditSignal ToAuditSignal(StrategyRuleSignal signal)
    {
        return new RaceAssistantAuditSignal
        {
            SignalType = signal.SignalType,
            AdviceType = signal.AdviceType.ToString(),
            Summary = signal.Summary,
            RecommendedAction = signal.RecommendedAction,
            Confidence = signal.Confidence.ToString(),
            RiskLevel = signal.RiskLevel.ToString(),
            MissingData = signal.MissingData
        };
    }

    private static RaceAssistantAuditResult? ToAuditResult(StrategyAdviceResult? advice)
    {
        return advice is null
            ? null
            : new RaceAssistantAuditResult
            {
                AdviceType = advice.AdviceType.ToString(),
                Summary = advice.Summary,
                Reason = advice.Reason,
                RecommendedAction = advice.RecommendedAction,
                Confidence = advice.Confidence.ToString(),
                RiskLevel = advice.RiskLevel.ToString(),
                MissingData = advice.MissingData,
                Tts = advice.Tts
            };
    }

    private static string BuildRealtimeInstruction(string question)
    {
        return $"车手通过方向盘绑定按键语音提问：{question.Trim()}。回答要适合驾驶中收听，优先给一条可执行建议；如果问题询问比赛数据，直接用当前状态回答；数据不足时明确说明。";
    }

    private static string CreateQuestionId()
    {
        return $"q_{Guid.NewGuid():N}";
    }

    private static string SelectSpeechText(AIAnalysisResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.TtsText) && result.TtsText.Trim() != "-")
        {
            return result.TtsText.Trim();
        }

        if (!string.IsNullOrWhiteSpace(result.Tts) && result.Tts.Trim() != "-")
        {
            return result.Tts.Trim();
        }

        return string.IsNullOrWhiteSpace(result.Summary)
            ? "AI 已完成分析，请查看日志。"
            : result.Summary.Trim();
    }

    private static VoiceAiQueryResult CreateFailure(string? message)
    {
        return new VoiceAiQueryResult
        {
            IsSuccess = false,
            ErrorMessage = string.IsNullOrWhiteSpace(message) ? AIErrorMessageFormatter.NetworkError : message.Trim()
        };
    }
}
