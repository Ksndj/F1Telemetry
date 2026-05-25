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

    /// <summary>
    /// Gets the race-assistant advice type when the response is an interactive strategy answer.
    /// </summary>
    [JsonPropertyName("adviceType")]
    public string AdviceType { get; init; } = "-";

    /// <summary>
    /// Gets the race-assistant advice reason.
    /// </summary>
    [JsonPropertyName("reason")]
    public string Reason { get; init; } = "-";

    /// <summary>
    /// Gets the recommended action for an interactive strategy answer.
    /// </summary>
    [JsonPropertyName("recommendedAction")]
    public string RecommendedAction { get; init; } = "-";

    /// <summary>
    /// Gets the confidence band for an interactive strategy answer.
    /// </summary>
    [JsonPropertyName("confidence")]
    public string Confidence { get; init; } = "-";

    /// <summary>
    /// Gets the risk level for an interactive strategy answer.
    /// </summary>
    [JsonPropertyName("riskLevel")]
    public string RiskLevel { get; init; } = "-";

    /// <summary>
    /// Gets required data fields for an interactive strategy answer.
    /// </summary>
    [JsonPropertyName("requiredData")]
    public IReadOnlyList<string> RequiredData { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets missing data fields for an interactive strategy answer.
    /// </summary>
    [JsonPropertyName("missingData")]
    public IReadOnlyList<string> MissingData { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets parser or rule warnings for an interactive strategy answer.
    /// </summary>
    [JsonPropertyName("warnings")]
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}
