namespace F1Telemetry.Core.Models;

/// <summary>
/// Represents the most recently received collision event.
/// </summary>
public sealed record CollisionSnapshot
{
    /// <summary>
    /// Gets the first involved vehicle index.
    /// </summary>
    public byte Vehicle1Index { get; init; }

    /// <summary>
    /// Gets the second involved vehicle index.
    /// </summary>
    public byte Vehicle2Index { get; init; }

    /// <summary>
    /// Gets the raw collision severity.
    /// </summary>
    public byte Severity { get; init; }

    /// <summary>
    /// Gets the datagram receive timestamp.
    /// </summary>
    public DateTimeOffset CapturedAt { get; init; }
}
