using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using F1Telemetry.AI.Interfaces;
using F1Telemetry.AI.Models;
using F1Telemetry.Core.Formatting;

namespace F1Telemetry.AI.Services;

/// <summary>
/// Coordinates prompt creation, DeepSeek requests, and JSON result parsing for lap analysis.
/// </summary>
public sealed class DeepSeekAnalysisService : IAIAnalysisService
{
    private const string NonChineseTtsTextReplacement = "AI 播报暂不可用，请查看日志。";
    private const string RefuelingAdviceReplacement = "燃油偏低，请省油并控制油耗；维修区无法加燃油。";
    private const int MaxTtsCharacters = 35;
    private static readonly Regex LargeJouleTextPattern = new(
        @"(?<value>\d{5,})(?:\.\d+)?\s*(?:焦耳|J|joules?)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

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

            var ttsText = string.IsNullOrWhiteSpace(result.Tts) || result.Tts.Trim() == "-"
                ? result.TtsText
                : result.Tts;
            var normalizedTtsText = NormalizeTtsText(ttsText);
            var strategyReview = NormalizeResultText(result.StrategyReview);
            var tyreReview = NormalizeResultText(result.TyreReview == "-" ? result.TyreAdvice : result.TyreReview);
            var ersFuelReview = NormalizeResultText(result.ErsFuelReview == "-" ? result.FuelAdvice : result.ErsFuelReview);
            var opponentReview = NormalizeResultText(result.OpponentReview == "-" ? result.TrafficAdvice : result.OpponentReview);

            return result with
            {
                IsSuccess = true,
                ErrorMessage = string.Empty,
                Summary = NormalizeResultText(result.Summary),
                KeyProblems = NormalizeList(result.KeyProblems),
                StrategyReview = strategyReview,
                TyreReview = tyreReview,
                ErsFuelReview = ersFuelReview,
                OpponentReview = opponentReview,
                Improvements = EnsureMinimumImprovements(result.Improvements, strategyReview, tyreReview, ersFuelReview, opponentReview),
                TyreAdvice = tyreReview,
                FuelAdvice = ersFuelReview,
                TrafficAdvice = opponentReview,
                Tts = normalizedTtsText,
                TtsText = normalizedTtsText
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

        var trimmed = NormalizeEnergyText(text.Trim());
        return ContainsRefuelingAdvice(trimmed) ? RefuelingAdviceReplacement : trimmed;
    }

    private static string NormalizeTtsText(string? text)
    {
        var normalized = NormalizeResultText(text);
        if (HasTooMuchEnglishForSpeech(normalized))
        {
            return NonChineseTtsTextReplacement;
        }

        return normalized.Length <= MaxTtsCharacters
            ? normalized
            : normalized[..(MaxTtsCharacters - 3)].TrimEnd() + "...";
    }

    private static IReadOnlyList<string> NormalizeList(IReadOnlyList<string>? values)
    {
        if (values is null || values.Count == 0)
        {
            return Array.Empty<string>();
        }

        return values
            .Select(NormalizeResultText)
            .Where(value => value != "-")
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> EnsureMinimumImprovements(
        IReadOnlyList<string>? improvements,
        params string[] fallbackReviews)
    {
        var normalized = NormalizeList(improvements).ToList();
        foreach (var fallback in fallbackReviews.Select(NormalizeResultText).Where(value => value != "-"))
        {
            if (normalized.Count >= 3)
            {
                break;
            }

            normalized.Add($"下次复盘：{fallback}");
        }

        while (normalized.Count < 3)
        {
            normalized.Add(normalized.Count switch
            {
                0 => "下次优先稳定长距离节奏。",
                1 => "下次复盘轮胎衰退与进站窗口。",
                _ => "下次关注 ERS、燃油和前后车压力。"
            });
        }

        return normalized.Distinct(StringComparer.Ordinal).Take(3).ToArray();
    }

    private static string NormalizeEnergyText(string text)
    {
        return LargeJouleTextPattern.Replace(
            text,
            match =>
            {
                var valueText = match.Groups["value"].Value;
                return float.TryParse(valueText, out var joules)
                    ? EnergyFormatter.FormatMegaJoules(joules)
                    : match.Value;
            });
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
