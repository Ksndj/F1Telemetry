using F1Telemetry.AI.Interfaces;
using F1Telemetry.AI.Models;
using F1Telemetry.AI.Services;
using F1Telemetry.Analytics.Strategy;
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
        TtsQueue ttsQueue)
    {
        _speechRecognitionService = speechRecognitionService ?? throw new ArgumentNullException(nameof(speechRecognitionService));
        _aiAnalysisService = aiAnalysisService ?? throw new ArgumentNullException(nameof(aiAnalysisService));
        _ttsMessageFactory = ttsMessageFactory ?? throw new ArgumentNullException(nameof(ttsMessageFactory));
        _ttsQueue = ttsQueue ?? throw new ArgumentNullException(nameof(ttsQueue));
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

        var strategyContext = ResolveStrategyContext(question, request);
        if (strategyContext is null)
        {
            return await AskLegacyAsync(question, request, cancellationToken);
        }

        if (!request.AiSettings.AiEnabled)
        {
            return CreateRaceAssistantSuccess(
                question,
                strategyContext,
                _fallbackAdviceService.BuildFallback(strategyContext, "AI 未启用"),
                request,
                wasQueued: false);
        }

        StrategyAdviceResult advice;
        if (string.IsNullOrWhiteSpace(request.AiSettings.ApiKey))
        {
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
                var result = await _aiAnalysisService.AnalyzeAsync(aiContext, request.AiSettings, cancellationToken);
                if (!result.IsSuccess && IsInvalidAiFormat(result.ErrorMessage))
                {
                    return CreateRaceAssistantFailure(question, strategyContext, result.ErrorMessage);
                }

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
            return new VoiceAiQueryResult
            {
                IsSuccess = false,
                RecognizedQuestion = question.Trim(),
                SessionUid = strategyContext.SessionUid,
                Intent = strategyContext.Intent,
                Mode = strategyContext.Mode,
                Advice = advice,
                ErrorMessage = "会话已变化，已忽略旧回答",
                WasIgnoredBecauseSessionChanged = true
            };
        }

        var wasQueued = false;
        if (request.EnableTtsAnswer)
        {
            wasQueued = TryQueueSpeech(request, advice.Tts);
        }

        return CreateRaceAssistantSuccess(question, strategyContext, advice, request, wasQueued);
    }

    private async Task<VoiceAiQueryResult> AskLegacyAsync(
        string question,
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

    private static bool IsInvalidAiFormat(string? errorMessage)
    {
        return !string.IsNullOrWhiteSpace(errorMessage) &&
               errorMessage.Contains("格式无效", StringComparison.Ordinal);
    }

    private static VoiceAiQueryResult CreateRaceAssistantFailure(
        string question,
        StrategyQuestionContext context,
        string errorMessage)
    {
        return new VoiceAiQueryResult
        {
            IsSuccess = false,
            RecognizedQuestion = question.Trim(),
            SessionUid = context.SessionUid,
            Intent = context.Intent,
            Mode = context.Mode,
            ErrorMessage = string.IsNullOrWhiteSpace(errorMessage) ? AIErrorMessageFormatter.ParseFailure : errorMessage.Trim()
        };
    }

    private static VoiceAiQueryResult CreateRaceAssistantSuccess(
        string question,
        StrategyQuestionContext context,
        StrategyAdviceResult advice,
        VoiceAiQueryRequest request,
        bool wasQueued)
    {
        var speechText = StrategyAdviceJsonParser.CompressTts(advice.Tts);
        var answer = request.MaxAnswerLength > 0 && speechText.Length > request.MaxAnswerLength
            ? speechText[..request.MaxAnswerLength]
            : speechText;
        return new VoiceAiQueryResult
        {
            IsSuccess = true,
            RecognizedQuestion = question.Trim(),
            SessionUid = context.SessionUid,
            Intent = context.Intent,
            Mode = context.Mode,
            Advice = advice with { Tts = speechText },
            SpeechText = answer,
            WasQueuedForSpeech = wasQueued
        };
    }

    private static string BuildRealtimeInstruction(string question)
    {
        return $"车手通过方向盘绑定按键语音提问：{question.Trim()}。回答要适合驾驶中收听，优先给一条可执行建议；如果问题询问比赛数据，直接用当前状态回答；数据不足时明确说明。";
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
