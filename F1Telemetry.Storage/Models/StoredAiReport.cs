namespace F1Telemetry.Storage.Models;

/// <summary>
/// Represents a persisted AI analysis row.
/// </summary>
public sealed record StoredAiReport
{
    /// <summary>
    /// Gets the auto-incremented row identifier.
    /// </summary>
    public long Id { get; init; }

    /// <summary>
    /// Gets the associated session identifier.
    /// </summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the analyzed lap number.
    /// </summary>
    public int LapNumber { get; init; }

    /// <summary>
    /// Gets the summary text.
    /// </summary>
    public string Summary { get; init; } = "-";

    /// <summary>
    /// Gets the tyre advice text.
    /// </summary>
    public string TyreAdvice { get; init; } = "-";

    /// <summary>
    /// Gets the fuel advice text.
    /// </summary>
    public string FuelAdvice { get; init; } = "-";

    /// <summary>
    /// Gets the traffic advice text.
    /// </summary>
    public string TrafficAdvice { get; init; } = "-";

    /// <summary>
    /// Gets the TTS text.
    /// </summary>
    public string TtsText { get; init; } = "-";

    /// <summary>
    /// Gets a value indicating whether analysis succeeded.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Gets the stored error message.
    /// </summary>
    public string ErrorMessage { get; init; } = "-";

    /// <summary>
    /// Gets the creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }
}
