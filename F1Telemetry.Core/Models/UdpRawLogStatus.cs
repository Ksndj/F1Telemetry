namespace F1Telemetry.Core.Models;

/// <summary>
/// Describes the current raw UDP log writer status for UI display.
/// </summary>
public sealed record UdpRawLogStatus
{
    /// <summary>
    /// Gets a value indicating whether raw UDP recording is enabled.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Gets the directory where raw UDP logs are written.
    /// </summary>
    public string DirectoryPath { get; init; } = string.Empty;

    /// <summary>
    /// Gets the current or most recent raw UDP JSONL file path.
    /// </summary>
    public string CurrentFilePath { get; init; } = string.Empty;

    /// <summary>
    /// Gets the total number of raw UDP packets written during this app session.
    /// </summary>
    public long WrittenPacketCount { get; init; }

    /// <summary>
    /// Gets the total number of raw UDP packets dropped because the queue was full.
    /// </summary>
    public long DroppedPacketCount { get; init; }

    /// <summary>
    /// Gets the latest writer error shown to the user.
    /// </summary>
    public string LastError { get; init; } = string.Empty;
}
