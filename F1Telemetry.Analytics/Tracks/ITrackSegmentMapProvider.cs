namespace F1Telemetry.Analytics.Tracks;

/// <summary>
/// Provides track segment maps for F1 game track identifiers.
/// </summary>
public interface ITrackSegmentMapProvider
{
    /// <summary>
    /// Gets the best available segment map for the supplied track identifier.
    /// </summary>
    /// <param name="trackId">The F1 game track identifier, or <c>null</c> when unavailable.</param>
    /// <returns>A supported, estimated, or unsupported map result.</returns>
    TrackSegmentMap GetMap(sbyte? trackId);
}
