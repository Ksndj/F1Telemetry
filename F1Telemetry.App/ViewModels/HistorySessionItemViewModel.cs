using System.Globalization;
using F1Telemetry.App.Formatting;
using F1Telemetry.Storage.Models;

namespace F1Telemetry.App.ViewModels;

/// <summary>
/// Represents a WPF-friendly row for a stored telemetry session.
/// </summary>
public sealed class HistorySessionItemViewModel
{
    private readonly StoredSession _session;

    /// <summary>
    /// Initializes a history session row from a stored session.
    /// </summary>
    /// <param name="session">The stored session to project.</param>
    public HistorySessionItemViewModel(StoredSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        _session = session;
        SessionId = _session.Id;
        SessionUid = string.IsNullOrWhiteSpace(_session.SessionUid) ? "-" : _session.SessionUid;
        TrackId = _session.TrackId;
        TrackText = FormatTrack(_session.TrackId);
        SessionTypeText = FormatSessionType(_session);
        StartedAtText = FormatTimestamp(_session.StartedAt);
        EndedAtText = _session.EndedAt is null ? "-" : FormatTimestamp(_session.EndedAt.Value);
        DurationText = FormatDuration(_session.StartedAt, _session.EndedAt);
        CanDelete = _session.EndedAt is not null;
        SummaryText = $"{TrackText} · {SessionTypeText} · {StartedAtText}";
    }

    /// <summary>
    /// Gets the storage session identifier.
    /// </summary>
    public string SessionId { get; }

    /// <summary>
    /// Gets the game session UID.
    /// </summary>
    public string SessionUid { get; }

    /// <summary>
    /// Gets the raw track identifier when available.
    /// </summary>
    public int? TrackId { get; }

    /// <summary>
    /// Gets the formatted track name.
    /// </summary>
    public string TrackText { get; }

    /// <summary>
    /// Gets the formatted session type.
    /// </summary>
    public string SessionTypeText { get; }

    /// <summary>
    /// Gets the formatted start time.
    /// </summary>
    public string StartedAtText { get; }

    /// <summary>
    /// Gets the formatted end time.
    /// </summary>
    public string EndedAtText { get; }

    /// <summary>
    /// Gets the formatted duration.
    /// </summary>
    public string DurationText { get; }

    /// <summary>
    /// Gets the compact session summary.
    /// </summary>
    public string SummaryText { get; }

    /// <summary>
    /// Gets a value indicating whether this stored session can be deleted from history.
    /// </summary>
    public bool CanDelete { get; }

    private static string FormatTrack(int? trackId)
    {
        if (trackId is null)
        {
            return TrackNameFormatter.Format(null);
        }

        if (trackId.Value < sbyte.MinValue || trackId.Value > sbyte.MaxValue)
        {
            return $"未知赛道（ID {trackId.Value}）";
        }

        return TrackNameFormatter.Format((sbyte)trackId.Value);
    }

    private static string FormatSessionType(StoredSession session)
    {
        var sessionType = session.SessionType;
        if (sessionType is null || sessionType.Value < byte.MinValue || sessionType.Value > byte.MaxValue)
        {
            return SessionTypeFormatter.Format(null);
        }

        return SessionTypeFormatter.Format(
            (byte)sessionType.Value,
            ToByte(session.TotalLaps),
            session.WeekendStructure);
    }

    private static byte? ToByte(int? value)
    {
        return value.HasValue && value.Value >= byte.MinValue && value.Value <= byte.MaxValue
            ? (byte)value.Value
            : null;
    }

    private static string FormatTimestamp(DateTimeOffset timestamp)
    {
        return timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    }

    private static string FormatDuration(DateTimeOffset startedAt, DateTimeOffset? endedAt)
    {
        if (endedAt is null)
        {
            return "进行中";
        }

        var duration = endedAt.Value - startedAt;
        if (duration < TimeSpan.Zero)
        {
            return "-";
        }

        if (duration.TotalHours >= 1)
        {
            return $"{(int)duration.TotalHours}h {duration.Minutes:00}m";
        }

        return duration.TotalMinutes >= 1
            ? $"{(int)duration.TotalMinutes}m {duration.Seconds:00}s"
            : $"{(int)duration.TotalSeconds}s";
    }
}
