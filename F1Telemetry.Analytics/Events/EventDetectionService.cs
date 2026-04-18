using System.Text.Json;
using F1Telemetry.Analytics.Interfaces;
using F1Telemetry.Core.Models;

namespace F1Telemetry.Analytics.Events;

/// <summary>
/// Detects reusable race events by comparing successive aggregate session-state snapshots.
/// </summary>
public sealed class EventDetectionService : IEventDetectionService
{
    private readonly object _gate = new();
    private readonly EventDetectionOptions _options;
    private readonly Queue<RaceEvent> _pendingEvents = new();
    private readonly Dictionary<string, DateTimeOffset> _lastRaisedAtByDedupKey = new(StringComparer.Ordinal);
    private readonly HashSet<string> _highTyreWearTyreKeys = new(StringComparer.Ordinal);
    private readonly Dictionary<int, PitCandidate> _pitCandidates = new();
    private SessionState? _previousState;

    /// <summary>
    /// Initializes a new event detection service.
    /// </summary>
    /// <param name="options">The detection thresholds and cooldown configuration.</param>
    public EventDetectionService(EventDetectionOptions? options = null)
    {
        _options = options ?? new EventDetectionOptions();
    }

    /// <inheritdoc />
    public void Observe(SessionState sessionState)
    {
        ArgumentNullException.ThrowIfNull(sessionState);

        lock (_gate)
        {
            var previousState = _previousState;
            if (previousState is not null)
            {
                DetectPitEvents(previousState, sessionState);
                DetectPlayerLapInvalidated(previousState, sessionState);
                DetectLowFuel(previousState, sessionState);
                DetectHighTyreWear(previousState, sessionState);
            }

            CleanupPitCandidates(sessionState.UpdatedAt);
            _previousState = sessionState;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<RaceEvent> DrainPendingEvents()
    {
        lock (_gate)
        {
            if (_pendingEvents.Count == 0)
            {
                return Array.Empty<RaceEvent>();
            }

            var drained = new List<RaceEvent>(_pendingEvents.Count);
            while (_pendingEvents.Count > 0)
            {
                drained.Add(_pendingEvents.Dequeue());
            }

            return drained;
        }
    }

    private void DetectPitEvents(SessionState previousState, SessionState currentState)
    {
        var previousPlayer = previousState.PlayerCar;
        var currentPlayer = currentState.PlayerCar;
        if (previousPlayer?.Position is null || currentPlayer is null)
        {
            return;
        }

        var previousCarsByIndex = previousState.Cars.ToDictionary(car => car.CarIndex);
        var currentCarsByIndex = currentState.Cars.ToDictionary(car => car.CarIndex);

        var previousFront = previousState.Cars.FirstOrDefault(car => car.Position == previousPlayer.Position - 1);
        var previousRear = previousState.Cars.FirstOrDefault(car => car.Position == previousPlayer.Position + 1);

        RegisterPitCandidate(previousFront, PitRelation.Front, currentState.UpdatedAt);
        RegisterPitCandidate(previousRear, PitRelation.Rear, currentState.UpdatedAt);

        foreach (var candidateEntry in _pitCandidates.ToArray())
        {
            if (!previousCarsByIndex.TryGetValue(candidateEntry.Key, out var previousCar)
                || !currentCarsByIndex.TryGetValue(candidateEntry.Key, out var currentCar))
            {
                continue;
            }

            var pitStatusEntered = IsPitStatusEntered(previousCar, currentCar);
            var numPitStopsIncreased = (currentCar.NumPitStops ?? 0) > (previousCar.NumPitStops ?? 0);
            var tyreChanged = HasTyreCompoundChanged(previousCar, currentCar);

            if (pitStatusEntered)
            {
                candidateEntry.Value.PitStatusEntered = true;
            }

            if (numPitStopsIncreased)
            {
                candidateEntry.Value.NumPitStopsIncreased = true;
            }

            if (tyreChanged)
            {
                candidateEntry.Value.TyreChanged = true;
            }

            if (candidateEntry.Value.PitStatusEntered
                && (candidateEntry.Value.NumPitStopsIncreased || candidateEntry.Value.TyreChanged))
            {
                EmitPitEvent(candidateEntry.Value.Relation, currentCar, currentState.UpdatedAt);
                _pitCandidates.Remove(candidateEntry.Key);
            }
        }
    }

    private void DetectPlayerLapInvalidated(SessionState previousState, SessionState currentState)
    {
        var previousPlayer = previousState.PlayerCar;
        var currentPlayer = currentState.PlayerCar;
        if (previousPlayer is null || currentPlayer is null)
        {
            return;
        }

        var sameLap = previousPlayer.CurrentLapNumber == currentPlayer.CurrentLapNumber;
        var becameInvalid = sameLap
            && previousPlayer.IsCurrentLapValid == true
            && currentPlayer.IsCurrentLapValid == false;

        if (!becameInvalid)
        {
            return;
        }

        var lapNumber = (int?)currentPlayer.CurrentLapNumber;
        EmitEvent(
            eventType: EventType.PlayerLapInvalidated,
            severity: EventSeverity.Warning,
            timestamp: currentState.UpdatedAt,
            lapNumber: lapNumber,
            vehicleIdx: currentPlayer.CarIndex,
            driverName: currentPlayer.DriverName,
            message: $"Lap {lapNumber?.ToString() ?? "-"} 已无效。",
            dedupKey: $"lap-invalid:{lapNumber}",
            payload: new
            {
                currentPlayer.CarIndex,
                currentPlayer.CurrentLapNumber,
                currentPlayer.IsCurrentLapValid
            });
    }

    private void DetectLowFuel(SessionState previousState, SessionState currentState)
    {
        var previousPlayer = previousState.PlayerCar;
        var currentPlayer = currentState.PlayerCar;
        if (previousPlayer?.FuelRemainingLaps is null || currentPlayer?.FuelRemainingLaps is null)
        {
            return;
        }

        var crossedThreshold = previousPlayer.FuelRemainingLaps.Value >= _options.LowFuelLapsThreshold
            && currentPlayer.FuelRemainingLaps.Value < _options.LowFuelLapsThreshold;

        if (!crossedThreshold)
        {
            return;
        }

        var lapNumber = (int?)currentPlayer.CurrentLapNumber;
        EmitEvent(
            eventType: EventType.LowFuel,
            severity: EventSeverity.Warning,
            timestamp: currentState.UpdatedAt,
            lapNumber: lapNumber,
            vehicleIdx: currentPlayer.CarIndex,
            driverName: currentPlayer.DriverName,
            message: $"燃油预估仅剩 {currentPlayer.FuelRemainingLaps.Value:0.0} 圈。",
            dedupKey: $"low-fuel:{lapNumber}",
            payload: new
            {
                currentPlayer.CarIndex,
                currentPlayer.CurrentLapNumber,
                currentPlayer.FuelRemainingLaps,
                Threshold = _options.LowFuelLapsThreshold
            });
    }

    private void DetectHighTyreWear(SessionState previousState, SessionState currentState)
    {
        var previousPlayer = previousState.PlayerCar;
        var currentPlayer = currentState.PlayerCar;
        if (currentPlayer?.TyreWear is null)
        {
            return;
        }

        var tyreKey = BuildTyreDedupKey(currentPlayer);
        if (string.IsNullOrWhiteSpace(tyreKey))
        {
            return;
        }

        if (HasTyreCompoundChanged(previousPlayer, currentPlayer))
        {
            _highTyreWearTyreKeys.Remove(tyreKey);
        }

        var crossedThreshold = previousPlayer?.TyreWear is not null
            && previousPlayer.TyreWear.Value < _options.HighTyreWearThreshold
            && currentPlayer.TyreWear.Value >= _options.HighTyreWearThreshold;

        if (!crossedThreshold || _highTyreWearTyreKeys.Contains(tyreKey))
        {
            return;
        }

        if (EmitEvent(
                eventType: EventType.HighTyreWear,
                severity: EventSeverity.Warning,
                timestamp: currentState.UpdatedAt,
                lapNumber: (int?)currentPlayer.CurrentLapNumber,
                vehicleIdx: currentPlayer.CarIndex,
                driverName: currentPlayer.DriverName,
                message: $"轮胎平均磨损已达到 {currentPlayer.TyreWear.Value:0.0}%。",
                dedupKey: $"high-tyre-wear:{tyreKey}",
                payload: new
                {
                    currentPlayer.CarIndex,
                    currentPlayer.CurrentLapNumber,
                    currentPlayer.TyreWear,
                    currentPlayer.VisualTyreCompound,
                    currentPlayer.ActualTyreCompound,
                    currentPlayer.NumPitStops,
                    Threshold = _options.HighTyreWearThreshold
                }))
        {
            _highTyreWearTyreKeys.Add(tyreKey);
        }
    }

    private void RegisterPitCandidate(CarSnapshot? snapshot, PitRelation relation, DateTimeOffset detectedAt)
    {
        if (snapshot is null || snapshot.IsPlayer)
        {
            return;
        }

        if (!_pitCandidates.TryGetValue(snapshot.CarIndex, out var existingCandidate)
            || existingCandidate.Relation != relation)
        {
            _pitCandidates[snapshot.CarIndex] = new PitCandidate(relation, detectedAt);
        }
    }

    private void EmitPitEvent(PitRelation relation, CarSnapshot carSnapshot, DateTimeOffset detectedAt)
    {
        var eventType = relation == PitRelation.Front ? EventType.FrontCarPitted : EventType.RearCarPitted;
        var messagePrefix = relation == PitRelation.Front ? "前车" : "后车";

        EmitEvent(
            eventType: eventType,
            severity: EventSeverity.Information,
            timestamp: detectedAt,
            lapNumber: (int?)carSnapshot.CurrentLapNumber,
            vehicleIdx: carSnapshot.CarIndex,
            driverName: carSnapshot.DriverName,
            message: $"{messagePrefix} {carSnapshot.DriverName ?? $"车辆 {carSnapshot.CarIndex}"} 已进站。",
            dedupKey: $"pit:{relation}:{carSnapshot.CarIndex}:{carSnapshot.NumPitStops ?? 0}",
            payload: new
            {
                carSnapshot.CarIndex,
                carSnapshot.DriverName,
                carSnapshot.CurrentLapNumber,
                carSnapshot.PitStatus,
                carSnapshot.NumPitStops,
                carSnapshot.VisualTyreCompound,
                carSnapshot.ActualTyreCompound
            });
    }

    private bool EmitEvent(
        EventType eventType,
        EventSeverity severity,
        DateTimeOffset timestamp,
        int? lapNumber,
        int? vehicleIdx,
        string? driverName,
        string message,
        string dedupKey,
        object payload)
    {
        if (_lastRaisedAtByDedupKey.TryGetValue(dedupKey, out var lastRaisedAt)
            && timestamp - lastRaisedAt < TimeSpan.FromSeconds(_options.EventCooldownSeconds))
        {
            return false;
        }

        var raceEvent = new RaceEvent
        {
            EventType = eventType,
            Timestamp = timestamp,
            LapNumber = lapNumber,
            VehicleIdx = vehicleIdx,
            DriverName = driverName,
            Severity = severity,
            Message = message,
            DedupKey = dedupKey,
            PayloadJson = JsonSerializer.Serialize(payload)
        };

        _pendingEvents.Enqueue(raceEvent);
        _lastRaisedAtByDedupKey[dedupKey] = timestamp;
        return true;
    }

    private void CleanupPitCandidates(DateTimeOffset now)
    {
        var staleBefore = now - TimeSpan.FromSeconds(Math.Max(_options.EventCooldownSeconds, 10));

        foreach (var candidate in _pitCandidates.Where(entry => entry.Value.DetectedAt < staleBefore).ToArray())
        {
            _pitCandidates.Remove(candidate.Key);
        }
    }

    private static bool IsPitStatusEntered(CarSnapshot previousCar, CarSnapshot currentCar)
    {
        return (previousCar.PitStatus ?? 0) == 0 && (currentCar.PitStatus ?? 0) > 0;
    }

    private static bool HasTyreCompoundChanged(CarSnapshot? previousCar, CarSnapshot currentCar)
    {
        if (previousCar is null)
        {
            return false;
        }

        return previousCar.VisualTyreCompound != currentCar.VisualTyreCompound
            || previousCar.ActualTyreCompound != currentCar.ActualTyreCompound;
    }

    private static string BuildTyreDedupKey(CarSnapshot carSnapshot)
    {
        return $"{carSnapshot.CarIndex}:{carSnapshot.NumPitStops ?? 0}:{carSnapshot.VisualTyreCompound?.ToString() ?? "-"}:{carSnapshot.ActualTyreCompound?.ToString() ?? "-"}";
    }

    private sealed class PitCandidate
    {
        public PitCandidate(PitRelation relation, DateTimeOffset detectedAt)
        {
            Relation = relation;
            DetectedAt = detectedAt;
        }

        public PitRelation Relation { get; }

        public DateTimeOffset DetectedAt { get; }

        public bool PitStatusEntered { get; set; }

        public bool NumPitStopsIncreased { get; set; }

        public bool TyreChanged { get; set; }
    }

    private enum PitRelation
    {
        Front,
        Rear
    }
}
