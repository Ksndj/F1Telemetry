namespace F1Telemetry.Core.Models;

/// <summary>
/// Represents the F1 26 session regulation values reported by a session packet.
/// </summary>
public sealed record SessionRegulations2026Snapshot
{
    /// <summary>
    /// Gets the raw active-aero track status.
    /// </summary>
    public byte ActiveAeroTrackStatus { get; init; }

    /// <summary>
    /// Gets the full active-aero zones.
    /// </summary>
    public IReadOnlyList<LapFractionZoneSnapshot> FullActiveAeroZones { get; init; } = Array.Empty<LapFractionZoneSnapshot>();

    /// <summary>
    /// Gets the partial active-aero zones.
    /// </summary>
    public IReadOnlyList<LapFractionZoneSnapshot> PartialActiveAeroZones { get; init; } = Array.Empty<LapFractionZoneSnapshot>();

    /// <summary>
    /// Gets the DRS zones.
    /// </summary>
    public IReadOnlyList<LapFractionZoneSnapshot> DrsZones { get; init; } = Array.Empty<LapFractionZoneSnapshot>();

    /// <summary>
    /// Gets the raw start reaction time.
    /// </summary>
    public float StartReactionTime { get; init; }

    /// <summary>
    /// Gets the anti-lock brakes assist setting.
    /// </summary>
    public byte AntiLockBrakesAssist { get; init; }

    /// <summary>
    /// Gets the traction control assist setting.
    /// </summary>
    public byte TractionControlAssist { get; init; }

    /// <summary>
    /// Gets the high-visibility dynamic racing line setting.
    /// </summary>
    public byte DynamicRacingLineHiVis { get; init; }

    /// <summary>
    /// Gets the colour-blind dynamic racing line setting.
    /// </summary>
    public byte DynamicRacingLineColourBlind { get; init; }

    /// <summary>
    /// Gets the recurring rewind prompt setting.
    /// </summary>
    public byte RecurringRewindPrompt { get; init; }
}
