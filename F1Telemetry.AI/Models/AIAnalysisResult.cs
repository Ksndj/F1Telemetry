using System.Text.Json.Serialization;

namespace F1Telemetry.AI.Models;

/// <summary>
/// Represents the JSON post-race analysis result returned by the AI service.
/// </summary>
public sealed record AIAnalysisResult
{
    /// <summary>
    /// Gets a value indicating whether the analysis succeeded.
    /// </summary>
    [JsonPropertyName("isSuccess")]
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Gets the short error message when analysis fails.
    /// </summary>
    [JsonPropertyName("errorMessage")]
    public string ErrorMessage { get; init; } = "-";

    /// <summary>
    /// Gets the general summary.
    /// </summary>
    [JsonPropertyName("summary")]
    public string Summary { get; init; } = "-";

    /// <summary>
    /// Gets the main race problems identified by the AI.
    /// </summary>
    [JsonPropertyName("keyProblems")]
    public IReadOnlyList<string> KeyProblems { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the strategy review text.
    /// </summary>
    [JsonPropertyName("strategyReview")]
    public string StrategyReview { get; init; } = "-";

    /// <summary>
    /// Gets the tyre and stint review text.
    /// </summary>
    [JsonPropertyName("tyreReview")]
    public string TyreReview { get; init; } = "-";

    /// <summary>
    /// Gets the ERS and fuel review text.
    /// </summary>
    [JsonPropertyName("ersFuelReview")]
    public string ErsFuelReview { get; init; } = "-";

    /// <summary>
    /// Gets the front and rear opponent review text.
    /// </summary>
    [JsonPropertyName("opponentReview")]
    public string OpponentReview { get; init; } = "-";

    /// <summary>
    /// Gets concrete next-race improvement suggestions.
    /// </summary>
    [JsonPropertyName("improvements")]
    public IReadOnlyList<string> Improvements { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the legacy tyre advice.
    /// </summary>
    [JsonPropertyName("tyreAdvice")]
    public string TyreAdvice { get; init; } = "-";

    /// <summary>
    /// Gets the fuel advice.
    /// </summary>
    [JsonPropertyName("fuelAdvice")]
    public string FuelAdvice { get; init; } = "-";

    /// <summary>
    /// Gets the traffic advice.
    /// </summary>
    [JsonPropertyName("trafficAdvice")]
    public string TrafficAdvice { get; init; } = "-";

    /// <summary>
    /// Gets the short text-to-speech message from the v3.0.2 detailed contract.
    /// </summary>
    [JsonPropertyName("tts")]
    public string Tts { get; init; } = "-";

    /// <summary>
    /// Gets the legacy short text-to-speech message.
    /// </summary>
    [JsonPropertyName("ttsText")]
    public string TtsText { get; init; } = "-";
}
