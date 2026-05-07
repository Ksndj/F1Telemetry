using System.Diagnostics;
using System.Text.Json;
using F1Telemetry.AI.Interfaces;
using F1Telemetry.AI.Models;

namespace F1Telemetry.AI.Services;

/// <summary>
/// Coordinates prompt creation, DeepSeek requests, and JSON result parsing for lap analysis.
/// </summary>
public sealed class DeepSeekAnalysisService : IAIAnalysisService
{
    private const string NonChineseTtsTextReplacement = "AI 播报暂不可用，请查看日志。";
    private const string RefuelingAdviceReplacement = "燃油偏低，请省油并控制油耗；维修区无法加燃油。";
    private static readonly string[] RefuelingAdviceSignals =
    [
        "进站加油",
        "进站补油",
        "进站加燃油",
        "回站加油",
        "回站补油",
        "补充燃油",
        "补油",
        "pit to refuel",
        "pit for fuel",
        "box to refuel",
        "box for fuel",
        "refuel",
        "refueling"
    ];

    private readonly DeepSeekClient _deepSeekClient;
    private readonly PromptBuilder _promptBuilder;

    /// <summary>
    /// Initializes a new AI analysis service.
    /// </summary>
    public DeepSeekAnalysisService(DeepSeekClient deepSeekClient, PromptBuilder promptBuilder)
    {
        _deepSeekClient = deepSeekClient ?? throw new ArgumentNullException(nameof(deepSeekClient));
        _promptBuilder = promptBuilder ?? throw new ArgumentNullException(nameof(promptBuilder));
    }

    /// <inheritdoc />
    public async Task<AIAnalysisResult> AnalyzeAsync(
        AIAnalysisContext context,
        AISettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(settings);

        if (!settings.AiEnabled)
        {
            return new AIAnalysisResult
            {
                IsSuccess = false,
                ErrorMessage = "AI analysis is disabled."
            };
        }

        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            return new AIAnalysisResult
            {
                IsSuccess = false,
                ErrorMessage = AIErrorMessageFormatter.MissingApiKey
            };
        }

        try
        {
            var prompt = _promptBuilder.BuildMessages(context);
            var request = new DeepSeekChatCompletionRequest
            {
                Model = string.IsNullOrWhiteSpace(settings.Model) ? "deepseek-chat" : settings.Model,
                Messages =
                [
                    new DeepSeekChatMessage("system", prompt.SystemMessage),
                    new DeepSeekChatMessage("user", prompt.UserMessage)
                ]
            };

            var content = await _deepSeekClient.CreateChatCompletionAsync(request, settings, cancellationToken);
            if (string.IsNullOrWhiteSpace(content))
            {
                return CreateFailure(AIErrorMessageFormatter.ParseFailure);
            }

            var result = JsonSerializer.Deserialize<AIAnalysisResult>(content);
            if (result is null)
            {
                return CreateFailure(AIErrorMessageFormatter.ParseFailure);
            }

            return result with
            {
                IsSuccess = true,
                ErrorMessage = string.Empty,
                Summary = NormalizeResultText(result.Summary),
                TyreAdvice = NormalizeResultText(result.TyreAdvice),
                FuelAdvice = NormalizeResultText(result.FuelAdvice),
                TrafficAdvice = NormalizeResultText(result.TrafficAdvice),
                TtsText = NormalizeTtsText(result.TtsText)
            };
        }
        catch (JsonException ex)
        {
            Debug.WriteLine($"AI response parse failed: {ex}");
            return CreateFailure(AIErrorMessageFormatter.ParseFailure);
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"AI request failed: {ex}");
            return CreateFailure(AIErrorMessageFormatter.FormatHttpFailure(ex));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException ex)
        {
            Debug.WriteLine($"AI request timed out: {ex}");
            return CreateFailure(AIErrorMessageFormatter.NetworkError);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AI request failed unexpectedly: {ex}");
            return CreateFailure(AIErrorMessageFormatter.NetworkError);
        }
    }

    private static string NormalizeResultText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "-";
        }

        var trimmed = text.Trim();
        return ContainsRefuelingAdvice(trimmed) ? RefuelingAdviceReplacement : trimmed;
    }

    private static string NormalizeTtsText(string? text)
    {
        var normalized = NormalizeResultText(text);
        return HasTooMuchEnglishForSpeech(normalized) ? NonChineseTtsTextReplacement : normalized;
    }

    private static bool HasTooMuchEnglishForSpeech(string text)
    {
        var asciiLetterCount = 0;
        var cjkLetterCount = 0;

        foreach (var character in text)
        {
            if (IsAsciiLetter(character))
            {
                asciiLetterCount++;
            }
            else if (IsCjkCharacter(character))
            {
                cjkLetterCount++;
            }
        }

        return asciiLetterCount >= 8 && asciiLetterCount > cjkLetterCount * 2;
    }

    private static bool IsAsciiLetter(char character)
    {
        return character is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
    }

    private static bool IsCjkCharacter(char character)
    {
        return character is >= '\u4E00' and <= '\u9FFF';
    }

    private static bool ContainsRefuelingAdvice(string text)
    {
        return RefuelingAdviceSignals.Any(signal => text.Contains(signal, StringComparison.OrdinalIgnoreCase));
    }

    private static AIAnalysisResult CreateFailure(string errorMessage)
    {
        return new AIAnalysisResult
        {
            IsSuccess = false,
            ErrorMessage = string.IsNullOrWhiteSpace(errorMessage)
                ? AIErrorMessageFormatter.NetworkError
                : errorMessage
        };
    }
}
