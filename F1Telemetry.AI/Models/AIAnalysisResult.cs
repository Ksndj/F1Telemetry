using System.Text.Json.Serialization;

namespace F1Telemetry.AI.Models;

/// <summary>
/// Represents the fixed JSON analysis result returned by the AI service.
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
    /// Gets the tyre advice.
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
    /// Gets the short text-to-speech message.
    /// </summary>
    [JsonPropertyName("ttsText")]
    public string TtsText { get; init; } = "-";
}
