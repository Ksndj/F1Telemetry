using F1Telemetry.Analytics.Laps;

namespace F1Telemetry.App.TrackMaps;

/// <summary>
/// Keeps live-session player Motion trajectories in memory for the corner analysis page.
/// </summary>
public sealed class InMemoryTrackMapTrajectoryStore : ITrackMapTrajectoryStore
{
    private readonly object _syncRoot = new();
    private readonly TrackMapBuilder _builder;
    private readonly Dictionary<TrackMapKey, TrackMapSnapshot> _snapshots = new();

    /// <summary>
    /// Initializes a new trajectory store.
    /// </summary>
    /// <param name="builder">The track-map builder.</param>
    public InMemoryTrackMapTrajectoryStore(TrackMapBuilder? builder = null)
    {
        _builder = builder ?? new TrackMapBuilder();
    }

    /// <inheritdoc />
    public void RecordCompletedLap(string sessionUid, int? trackId, int lapNumber, IReadOnlyList<LapSample> samples)
    {
        if (string.IsNullOrWhiteSpace(sessionUid) || lapNumber <= 0)
        {
            return;
        }

        var snapshot = _builder.BuildSnapshot(sessionUid, trackId, lapNumber, samples);
        var key = new TrackMapKey(sessionUid, trackId, lapNumber);
        lock (_syncRoot)
        {
            if (!_snapshots.TryGetValue(key, out var existing)
                || snapshot.Points.Count >= existing.Points.Count)
            {
                _snapshots[key] = snapshot;
            }
        }
    }

    /// <inheritdoc />
    public TrackMapSnapshot GetPreferredOrBest(string sessionUid, int? trackId, int? preferredLapNumber)
    {
        if (string.IsNullOrWhiteSpace(sessionUid))
        {
            return TrackMapBuilder.CreateEmptySnapshot(
                string.Empty,
                trackId,
                preferredLapNumber ?? 0,
                TrackMapStatus.WaitingMotionData,
                "等待 Motion 数据");
        }

        lock (_syncRoot)
        {
            var sessionSnapshots = _snapshots
                .Where(pair => string.Equals(pair.Key.SessionUid, sessionUid, StringComparison.Ordinal)
                    && (!trackId.HasValue || pair.Key.TrackId == trackId))
                .Select(pair => pair.Value)
                .ToArray();

            if (sessionSnapshots.Length == 0)
            {
                return TrackMapBuilder.CreateEmptySnapshot(
                    sessionUid,
                    trackId,
                    preferredLapNumber ?? 0,
                    TrackMapStatus.MissingMotionData,
                    "该会话缺少 Motion 坐标");
            }

            var preferred = preferredLapNumber is null
                ? null
                : sessionSnapshots.FirstOrDefault(snapshot => snapshot.LapNumber == preferredLapNumber.Value);
            if (preferred is not null && preferred.HasDrawableMap)
            {
                return preferred;
            }

            var best = sessionSnapshots
                .Where(snapshot => snapshot.HasDrawableMap)
                .OrderByDescending(snapshot => snapshot.Points.Count)
                .ThenBy(snapshot => snapshot.LapNumber)
                .FirstOrDefault();
            if (best is not null)
            {
                const string fallbackWarning = "当前圈缺少轨迹，已使用当前会话采样最完整圈";
                return best with
                {
                    WarningText = string.IsNullOrWhiteSpace(best.WarningText)
                        ? fallbackWarning
                        : $"{best.WarningText} · {fallbackWarning}"
                };
            }

            return preferred ?? sessionSnapshots
                .OrderByDescending(snapshot => snapshot.Points.Count)
                .First();
        }
    }

    private readonly record struct TrackMapKey(string SessionUid, int? TrackId, int LapNumber);
}
