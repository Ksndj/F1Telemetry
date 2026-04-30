using System.Threading;
using F1Telemetry.Core.Models;

namespace F1Telemetry.Analytics.State;

/// <summary>
/// Stores session-wide metadata and composes the current aggregate state snapshot.
/// </summary>
public sealed class SessionStateStore
{
    private SessionMetadataState _metadata = new();

    /// <summary>
    /// Initializes a new session state store.
    /// </summary>
    /// <param name="carStateStore">The underlying per-car store.</param>
    public SessionStateStore(CarStateStore carStateStore)
    {
        CarStateStore = carStateStore ?? throw new ArgumentNullException(nameof(carStateStore));
    }

    /// <summary>
    /// Gets the underlying per-car state store.
    /// </summary>
    public CarStateStore CarStateStore { get; }

    /// <summary>
    /// Captures the latest aggregate session state.
    /// </summary>
    public SessionState CaptureState()
    {
        var metadata = Volatile.Read(ref _metadata);

        return new SessionState
        {
            PlayerCarIndex = metadata.PlayerCarIndex,
            TrackId = metadata.TrackId,
            SessionType = metadata.SessionType,
            Weather = metadata.Weather,
            TrackTemperature = metadata.TrackTemperature,
            AirTemperature = metadata.AirTemperature,
            TotalLaps = metadata.TotalLaps,
            SessionTimeLeft = metadata.SessionTimeLeft,
            SessionDuration = metadata.SessionDuration,
            PitSpeedLimit = metadata.PitSpeedLimit,
            SafetyCarStatus = metadata.SafetyCarStatus,
            MarshalZoneFlags = new Dictionary<int, sbyte>(metadata.MarshalZoneFlags),
            ActiveCarCount = metadata.ActiveCarCount,
            LastEventCode = metadata.LastEventCode,
            PlayerCar = CarStateStore.CapturePlayerCar(),
            Opponents = CarStateStore.CaptureOpponents(),
            Cars = CarStateStore.CaptureAllCars(),
            UpdatedAt = metadata.UpdatedAt
        };
    }

    /// <summary>
    /// Clears session metadata and all tracked car snapshots for a fresh session boundary.
    /// </summary>
    public void Reset()
    {
        CarStateStore.Reset();
        Volatile.Write(ref _metadata, new SessionMetadataState());
    }

    internal void SetPlayerCarIndex(byte playerCarIndex, DateTimeOffset updatedAt)
    {
        CarStateStore.SetPlayerCarIndex(playerCarIndex, updatedAt);
        UpdateMetadata(metadata => metadata with
        {
            PlayerCarIndex = playerCarIndex,
            UpdatedAt = updatedAt
        });
    }

    internal void ApplySessionSnapshot(
        sbyte trackId,
        byte sessionType,
        byte weather,
        sbyte trackTemperature,
        sbyte airTemperature,
        byte totalLaps,
        ushort sessionTimeLeft,
        ushort sessionDuration,
        byte pitSpeedLimit,
        byte safetyCarStatus,
        IReadOnlyDictionary<int, sbyte> marshalZoneFlags,
        DateTimeOffset updatedAt)
    {
        UpdateMetadata(metadata => metadata with
        {
            TrackId = trackId,
            SessionType = sessionType,
            Weather = weather,
            TrackTemperature = trackTemperature,
            AirTemperature = airTemperature,
            TotalLaps = totalLaps,
            SessionTimeLeft = sessionTimeLeft,
            SessionDuration = sessionDuration,
            PitSpeedLimit = pitSpeedLimit,
            SafetyCarStatus = safetyCarStatus,
            MarshalZoneFlags = new Dictionary<int, sbyte>(marshalZoneFlags),
            UpdatedAt = updatedAt
        });
    }

    internal void SetActiveCarCount(byte activeCarCount, DateTimeOffset updatedAt)
    {
        UpdateMetadata(metadata => metadata with
        {
            ActiveCarCount = activeCarCount,
            UpdatedAt = updatedAt
        });
    }

    internal void SetLastEventCode(string? lastEventCode, DateTimeOffset updatedAt)
    {
        UpdateMetadata(metadata => metadata with
        {
            LastEventCode = lastEventCode,
            UpdatedAt = updatedAt
        });
    }

    private void UpdateMetadata(Func<SessionMetadataState, SessionMetadataState> updater)
    {
        var current = Volatile.Read(ref _metadata);
        var updated = updater(current);
        Volatile.Write(ref _metadata, updated);
    }

    private sealed record SessionMetadataState
    {
        public byte? PlayerCarIndex { get; init; }

        public sbyte? TrackId { get; init; }

        public byte? SessionType { get; init; }

        public byte? Weather { get; init; }

        public sbyte? TrackTemperature { get; init; }

        public sbyte? AirTemperature { get; init; }

        public byte? TotalLaps { get; init; }

        public ushort? SessionTimeLeft { get; init; }

        public ushort? SessionDuration { get; init; }

        public byte? PitSpeedLimit { get; init; }

        public byte? SafetyCarStatus { get; init; }

        public IReadOnlyDictionary<int, sbyte> MarshalZoneFlags { get; init; } = new Dictionary<int, sbyte>();

        public byte? ActiveCarCount { get; init; }

        public string? LastEventCode { get; init; }

        public DateTimeOffset UpdatedAt { get; init; }
    }
}
