using F1Telemetry.AI.Interfaces;
using F1Telemetry.AI.Models;
using F1Telemetry.AI.Services;
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

        if (!request.AiSettings.AiEnabled)
        {
            return CreateFailure("AI 未启用");
        }

        if (string.IsNullOrWhiteSpace(request.AiSettings.ApiKey))
        {
            return CreateFailure(AIErrorMessageFormatter.MissingApiKey);
        }

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
        var wasQueued = false;
        var message = _ttsMessageFactory.CreateForEngineerAdvice(
            string.IsNullOrWhiteSpace(request.AdviceKey) ? $"voice-ai:{Guid.NewGuid():N}" : request.AdviceKey,
            speechText,
            request.TtsOptions);
        if (message is not null)
        {
            wasQueued = _ttsQueue.TryEnqueue(message);
        }

        return new VoiceAiQueryResult
        {
            IsSuccess = true,
            RecognizedQuestion = question.Trim(),
            SpeechText = speechText,
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
