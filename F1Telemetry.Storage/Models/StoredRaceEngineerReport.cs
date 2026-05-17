namespace F1Telemetry.Storage.Models;

/// <summary>
/// Represents a persisted race-engineer report generated for a session or lap.
/// </summary>
public sealed record StoredRaceEngineerReport
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
    /// Gets the associated lap number when the report is lap-specific.
    /// </summary>
    public int? LapNumber { get; init; }

    /// <summary>
    /// Gets the report category.
    /// </summary>
    public string ReportType { get; init; } = "-";

    /// <summary>
    /// Gets the report summary text.
    /// </summary>
    public string Summary { get; init; } = "-";

    /// <summary>
    /// Gets the text intended for race-engineer speech output.
    /// </summary>
    public string SpokenText { get; init; } = "-";

    /// <summary>
    /// Gets optional structured report details.
    /// </summary>
    public string? DetailJson { get; init; }

    /// <summary>
    /// Gets a value indicating whether report generation succeeded.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Gets the stored error message.
    /// </summary>
    public string ErrorMessage { get; init; } = "-";

    /// <summary>
    /// Gets the row creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }
}
