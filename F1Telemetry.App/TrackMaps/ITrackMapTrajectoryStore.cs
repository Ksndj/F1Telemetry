using F1Telemetry.Analytics.Laps;

namespace F1Telemetry.App.TrackMaps;

/// <summary>
/// Stores completed player Motion trajectories used by the corner analysis map.
/// </summary>
public interface ITrackMapTrajectoryStore
{
    /// <summary>
    /// Records a completed lap trajectory.
    /// </summary>
    /// <param name="sessionUid">The game session UID.</param>
    /// <param name="trackId">The track id when known.</param>
    /// <param name="lapNumber">The completed lap number.</param>
    /// <param name="samples">The lap samples captured from the live state.</param>
    void RecordCompletedLap(string sessionUid, int? trackId, int lapNumber, IReadOnlyList<LapSample> samples);

    /// <summary>
    /// Returns the preferred reference-lap map or falls back to the most complete lap map in the session.
    /// </summary>
    /// <param name="sessionUid">The game session UID.</param>
    /// <param name="trackId">The track id when known.</param>
    /// <param name="preferredLapNumber">The preferred reference lap number.</param>
    /// <returns>A drawable map or an explicit empty-state snapshot.</returns>
    TrackMapSnapshot GetPreferredOrBest(string sessionUid, int? trackId, int? preferredLapNumber);
}
