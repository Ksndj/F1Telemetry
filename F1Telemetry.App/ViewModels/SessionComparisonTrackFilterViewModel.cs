namespace F1Telemetry.App.ViewModels;

/// <summary>
/// Represents one track group available for historical session comparison.
/// </summary>
public sealed record SessionComparisonTrackFilterViewModel
{
    /// <summary>
    /// Initializes a track filter row.
    /// </summary>
    /// <param name="trackId">The stored track identifier.</param>
    /// <param name="displayName">The user-facing track name.</param>
    /// <param name="sessionCount">The number of recent sessions in the group.</param>
    public SessionComparisonTrackFilterViewModel(
        int? trackId,
        string displayName,
        int sessionCount)
    {
        TrackId = trackId;
        DisplayName = displayName;
        SessionCount = sessionCount;
        SummaryText = $"{displayName} · {sessionCount} 个历史会话";
    }

    /// <summary>
    /// Gets the stored track identifier.
    /// </summary>
    public int? TrackId { get; }

    /// <summary>
    /// Gets the user-facing track name.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets the number of sessions in this track group.
    /// </summary>
    public int SessionCount { get; }

    /// <summary>
    /// Gets the compact track group summary.
    /// </summary>
    public string SummaryText { get; }
}
