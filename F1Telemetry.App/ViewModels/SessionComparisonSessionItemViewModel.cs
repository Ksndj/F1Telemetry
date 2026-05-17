using System.Globalization;
using F1Telemetry.App.Formatting;
using F1Telemetry.Core.Abstractions;
using F1Telemetry.Storage.Models;

namespace F1Telemetry.App.ViewModels;

/// <summary>
/// Represents a checkbox-friendly session row for comparison selection.
/// </summary>
public sealed class SessionComparisonSessionItemViewModel : ViewModelBase
{
    private bool _isSelected;

    /// <summary>
    /// Initializes a comparison session row from a stored session.
    /// </summary>
    /// <param name="session">The stored session to project.</param>
    public SessionComparisonSessionItemViewModel(StoredSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        SessionId = session.Id;
        SessionUid = string.IsNullOrWhiteSpace(session.SessionUid) ? "-" : session.SessionUid;
        TrackText = FormatTrack(session.TrackId);
        SessionTypeText = FormatSessionType(session);
        StartedAtText = FormatTimestamp(session.StartedAt);
        CanDelete = session.EndedAt is not null;
        SummaryText = $"{TrackText} · {SessionTypeText} · {StartedAtText}";
        ComparisonLabel = $"{SessionTypeText} · {StartedAtText}";
    }

    /// <summary>
    /// Occurs when the selected state changes.
    /// </summary>
    public event Action<SessionComparisonSessionItemViewModel, bool>? SelectionChanged;

    /// <summary>
    /// Gets the storage session identifier.
    /// </summary>
    public string SessionId { get; }

    /// <summary>
    /// Gets the game session UID.
    /// </summary>
    public string SessionUid { get; }

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
    /// Gets the compact session summary.
    /// </summary>
    public string SummaryText { get; }

    /// <summary>
    /// Gets the chart legend label for this session.
    /// </summary>
    public string ComparisonLabel { get; }

    /// <summary>
    /// Gets a value indicating whether this stored session can be deleted from history.
    /// </summary>
    public bool CanDelete { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the session is selected.
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                SelectionChanged?.Invoke(this, value);
            }
        }
    }

    internal void SetIsSelectedSilently(bool value)
    {
        SetProperty(ref _isSelected, value, nameof(IsSelected));
    }

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
}
