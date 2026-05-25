namespace F1Telemetry.App.Logging;

/// <summary>
/// Describes the observable state of an asynchronous file logger.
/// </summary>
public sealed record LogWriterStatus
{
    /// <summary>
    /// Gets a value indicating whether the logger accepts new records.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Gets the directory where records are written.
    /// </summary>
    public string DirectoryPath { get; init; } = string.Empty;

    /// <summary>
    /// Gets the current or most recent log file path.
    /// </summary>
    public string CurrentFilePath { get; init; } = string.Empty;

    /// <summary>
    /// Gets the number of records written during this app run.
    /// </summary>
    public long WrittenCount { get; init; }

    /// <summary>
    /// Gets the number of records dropped before writing.
    /// </summary>
    public long DroppedCount { get; init; }

    /// <summary>
    /// Gets the latest non-fatal logger warning.
    /// </summary>
    public string LastWarning { get; init; } = string.Empty;
}
