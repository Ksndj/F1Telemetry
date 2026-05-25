using System.Text.Json;
using F1Telemetry.AI.Models;
using F1Telemetry.Analytics.Strategy;

namespace F1Telemetry.AI.Services;

/// <summary>
/// Parses strict strategy-advice JSON returned by the race assistant.
/// </summary>
public sealed class StrategyAdviceJsonParser
{
    private const int MaxTtsCharacters = 35;

    /// <summary>
    /// Parses a strategy advice JSON object.
    /// </summary>
    /// <param name="content">The assistant response content.</param>
    public StrategyAdviceParseResult Parse(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return Invalid();
        }

        try
        {
            using var document = JsonDocument.Parse(content);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Invalid();
            }

            var root = document.RootElement;
            var warnings = new List<string>();
            var confidence = ReadEnum(root, "confidence", StrategyAdviceConfidence.Low, out var confidenceMissing);
            if (confidenceMissing)
            {
                warnings.Add("confidence missing; defaulted to Low.");
            }

            var advice = new StrategyAdviceResult
            {
                AdviceType = ReadEnum(root, "adviceType", RaceAssistantAdviceType.Unknown, out _),
                Summary = ReadString(root, "summary"),
                Reason = ReadString(root, "reason"),
                RecommendedAction = ReadString(root, "recommendedAction"),
                Confidence = confidence,
                RiskLevel = ReadEnum(root, "riskLevel", StrategyRiskLevel.Unknown, out _),
                RequiredData = ReadStringArray(root, "requiredData"),
                MissingData = ReadStringArray(root, "missingData"),
                Tts = CompressTts(ReadString(root, "tts")),
                Warnings = warnings
            };

            return new StrategyAdviceParseResult
            {
                IsSuccess = true,
                Advice = advice
            };
        }
        catch (JsonException)
        {
            return Invalid();
        }
    }

    /// <summary>
    /// Compresses race-assistant TTS to the maximum allowed short Chinese sentence length.
    /// </summary>
    /// <param name="text">The raw TTS text.</param>
    public static string CompressTts(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalized = text.Trim().Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\n", string.Empty, StringComparison.Ordinal);
        return normalized.Length <= MaxTtsCharacters
            ? normalized
            : normalized[..(MaxTtsCharacters - 1)].TrimEnd('，', '。', '；', ',', '.', ';', ' ') + "。";
    }

    private static StrategyAdviceParseResult Invalid()
    {
        return new StrategyAdviceParseResult
        {
            IsSuccess = false,
            ErrorMessage = "AI 返回格式无效"
        };
    }

    private static string ReadString(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim() ?? string.Empty
            : string.Empty;
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString()?.Trim() ?? string.Empty)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static TEnum ReadEnum<TEnum>(JsonElement root, string name, TEnum fallback, out bool missing)
        where TEnum : struct, Enum
    {
        missing = !root.TryGetProperty(name, out var value);
        if (missing)
        {
            return fallback;
        }

        if (value.ValueKind == JsonValueKind.String &&
            Enum.TryParse<TEnum>(value.GetString(), ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        if (value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt32(out var numeric) &&
            Enum.IsDefined(typeof(TEnum), numeric))
        {
            return (TEnum)Enum.ToObject(typeof(TEnum), numeric);
        }

        return fallback;
    }
}
