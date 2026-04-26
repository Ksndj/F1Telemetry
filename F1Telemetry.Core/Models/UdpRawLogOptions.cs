namespace F1Telemetry.Core.Models;

/// <summary>
/// Configures optional raw UDP packet recording.
/// </summary>
public sealed record UdpRawLogOptions
{
    /// <summary>
    /// Gets a value indicating whether raw UDP JSONL recording is enabled.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Gets the directory where raw UDP JSONL files are written.
    /// </summary>
    public string DirectoryPath { get; init; } = string.Empty;

    /// <summary>
    /// Gets the maximum number of raw packets buffered before dropping new packets.
    /// </summary>
    public int QueueCapacity { get; init; } = 4096;
}
