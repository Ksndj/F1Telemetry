using F1Telemetry.Analytics.Interfaces;
using F1Telemetry.Analytics.State;
using F1Telemetry.Core.Models;
using F1Telemetry.Udp.Packets;

namespace F1Telemetry.Analytics.Services;

/// <summary>
/// Translates parsed UDP packets into the central real-time session state.
/// </summary>
public sealed class StateAggregator : IStateAggregator
{
    private readonly ILapAnalyzer? _lapAnalyzer;
    private readonly IEventDetectionService? _eventDetectionService;

    /// <summary>
    /// Initializes a new aggregator with a dedicated in-memory state store.
    /// </summary>
    public StateAggregator()
        : this(new SessionStateStore(new CarStateStore()))
    {
    }

    /// <summary>
    /// Initializes a new aggregator for the specified state store.
    /// </summary>
    /// <param name="sessionStateStore">The state store to update.</param>
    public StateAggregator(SessionStateStore sessionStateStore)
        : this(sessionStateStore, null, null)
    {
    }

    /// <summary>
    /// Initializes a new aggregator for the specified state store and lap analyzer.
    /// </summary>
    /// <param name="sessionStateStore">The state store to update.</param>
    /// <param name="lapAnalyzer">The optional lap analyzer that consumes the aggregated player state.</param>
    public StateAggregator(SessionStateStore sessionStateStore, ILapAnalyzer? lapAnalyzer)
        : this(sessionStateStore, lapAnalyzer, null)
    {
    }

    /// <summary>
    /// Initializes a new aggregator for the specified state store, lap analyzer, and event detection service.
    /// </summary>
    /// <param name="sessionStateStore">The state store to update.</param>
    /// <param name="lapAnalyzer">The optional lap analyzer that consumes the aggregated player state.</param>
    /// <param name="eventDetectionService">The optional event detection service that consumes aggregate session state.</param>
    public StateAggregator(
        SessionStateStore sessionStateStore,
        ILapAnalyzer? lapAnalyzer,
        IEventDetectionService? eventDetectionService)
    {
        SessionStateStore = sessionStateStore ?? throw new ArgumentNullException(nameof(sessionStateStore));
        _lapAnalyzer = lapAnalyzer;
        _eventDetectionService = eventDetectionService;
    }

    /// <inheritdoc />
    public SessionStateStore SessionStateStore { get; }

    /// <inheritdoc />
    public void ApplyPacket(ParsedPacket parsedPacket)
    {
        ArgumentNullException.ThrowIfNull(parsedPacket);

        var receivedAt = parsedPacket.Datagram.ReceivedAt;
        SessionStateStore.SetPlayerCarIndex(parsedPacket.Header.PlayerCarIndex, receivedAt);

        switch (parsedPacket.Packet)
        {
            case SessionPacket packet:
                ApplySession(packet, parsedPacket.Header.PlayerCarIndex, receivedAt);
                break;
            case ParticipantsPacket packet:
                ApplyParticipants(packet, receivedAt);
                break;
            case LapDataPacket packet:
                ApplyLapData(packet, receivedAt);
                break;
            case CarTelemetryPacket packet:
                ApplyCarTelemetry(packet, receivedAt);
                break;
            case CarStatusPacket packet:
                ApplyCarStatus(packet, receivedAt);
                break;
            case CarDamagePacket packet:
                ApplyCarDamage(packet, receivedAt);
                break;
            case MotionPacket packet:
                ApplyMotion(packet, receivedAt);
                break;
            case EventPacket packet:
                ApplyEvent(packet, receivedAt);
                break;
            case TyreSetsPacket packet:
                ApplyTyreSets(packet, receivedAt);
                break;
            case SessionHistoryPacket packet:
                ApplySessionHistory(packet, receivedAt);
                break;
            case FinalClassificationPacket:
            case LapPositionsPacket:
            case MotionExPacket:
                break;
        }

        if (_lapAnalyzer is null && _eventDetectionService is null)
        {
            return;
        }

        var sessionState = SessionStateStore.CaptureState();
        _lapAnalyzer?.Observe(parsedPacket, sessionState);
        _eventDetectionService?.Observe(sessionState);
    }

    private void ApplySession(SessionPacket packet, byte playerCarIndex, DateTimeOffset receivedAt)
    {
        SessionStateStore.SetPlayerCarIndex(playerCarIndex, receivedAt);
        SessionStateStore.ApplySessionSnapshot(
            trackId: packet.TrackId,
            sessionType: packet.SessionType,
            weather: packet.Weather,
            trackTemperature: packet.TrackTemperature,
            airTemperature: packet.AirTemperature,
            totalLaps: packet.TotalLaps,
            sessionTimeLeft: packet.SessionTimeLeft,
            sessionDuration: packet.SessionDuration,
            pitSpeedLimit: packet.PitSpeedLimit,
            safetyCarStatus: packet.SafetyCarStatus,
            marshalZoneFlags: BuildMarshalZoneFlags(packet),
            updatedAt: receivedAt);
    }

    private void ApplyParticipants(ParticipantsPacket packet, DateTimeOffset receivedAt)
    {
        SessionStateStore.SetActiveCarCount(packet.NumActiveCars, receivedAt);

        for (var carIndex = 0; carIndex < packet.Participants.Length; carIndex++)
        {
            var participant = packet.Participants[carIndex];
            SessionStateStore.CarStateStore.SetParticipantTelemetryRestriction(
                carIndex,
                isRestricted: !participant.YourTelemetry,
                updatedAt: receivedAt);

            SessionStateStore.CarStateStore.UpdateCar(
                carIndex,
                snapshot => snapshot with
                {
                    IsAiControlled = participant.IsAiControlled,
                    DriverName = string.IsNullOrWhiteSpace(participant.Name) ? null : participant.Name,
                    RaceNumber = participant.RaceNumber,
                    TeamId = participant.TeamId,
                    Nationality = participant.Nationality
                },
                receivedAt);
        }
    }

    private void ApplyLapData(LapDataPacket packet, DateTimeOffset receivedAt)
    {
        for (var carIndex = 0; carIndex < packet.Cars.Length; carIndex++)
        {
            var car = packet.Cars[carIndex];
            SessionStateStore.CarStateStore.UpdateCar(
                carIndex,
                snapshot => snapshot with
                {
                    Position = car.CarPosition,
                    CurrentLapNumber = car.CurrentLapNumber,
                    LastLapTimeInMs = car.LastLapTimeInMs,
                    CurrentLapTimeInMs = car.CurrentLapTimeInMs,
                    PitStatus = car.PitStatus,
                    NumPitStops = car.NumPitStops,
                    LapDistance = car.LapDistance,
                    TotalDistance = car.TotalDistance,
                    DeltaToCarInFrontInMs = car.DeltaToCarInFrontInMs,
                    DeltaToRaceLeaderInMs = car.DeltaToRaceLeaderInMs,
                    IsCurrentLapValid = !car.IsCurrentLapInvalid
                },
                receivedAt);
        }
    }

    private void ApplyCarTelemetry(CarTelemetryPacket packet, DateTimeOffset receivedAt)
    {
        for (var carIndex = 0; carIndex < packet.Cars.Length; carIndex++)
        {
            if (!SessionStateStore.CarStateStore.HasTelemetryAccess(carIndex))
            {
                SessionStateStore.CarStateStore.ClearRestrictedTelemetry(carIndex, receivedAt);
                continue;
            }

            var car = packet.Cars[carIndex];
            SessionStateStore.CarStateStore.UpdateCar(
                carIndex,
                snapshot => snapshot with
                {
                    Telemetry = new TelemetrySnapshot(
                        Timestamp: receivedAt,
                        LapNumber: snapshot.CurrentLapNumber ?? 0,
                        SpeedKph: car.Speed,
                        Throttle: car.Throttle,
                        Brake: car.Brake,
                        TrackName: null),
                    SteeringInput = car.Steer,
                    Gear = car.Gear,
                    EngineRpm = car.EngineRpm,
                    IsDrsEnabled = car.Drs
                },
                receivedAt);
        }
    }

    private void ApplyCarStatus(CarStatusPacket packet, DateTimeOffset receivedAt)
    {
        for (var carIndex = 0; carIndex < packet.Cars.Length; carIndex++)
        {
            if (!SessionStateStore.CarStateStore.HasTelemetryAccess(carIndex))
            {
                SessionStateStore.CarStateStore.ClearRestrictedTelemetry(carIndex, receivedAt);
                continue;
            }

            var car = packet.Cars[carIndex];
            SessionStateStore.CarStateStore.UpdateCar(
                carIndex,
                snapshot => snapshot with
                {
                    FuelInTank = car.FuelInTank,
                    FuelRemainingLaps = car.FuelRemainingLaps,
                    ErsStoreEnergy = car.ErsStoreEnergy,
                    ActualTyreCompound = car.ActualTyreCompound,
                    VisualTyreCompound = car.VisualTyreCompound,
                    TyresAgeLaps = car.TyresAgeLaps
                },
                receivedAt);
        }
    }

    private void ApplySessionHistory(SessionHistoryPacket packet, DateTimeOffset receivedAt)
    {
        var bestLapIndex = packet.BestLapTimeLapNumber - 1;
        if (bestLapIndex >= packet.LapHistory.Length)
        {
            return;
        }

        var bestLapTimeInMs = packet.BestLapTimeLapNumber == 0
            ? (uint?)null
            : packet.LapHistory[bestLapIndex].LapTimeInMs;

        SessionStateStore.CarStateStore.UpdateCar(
            packet.CarIndex,
            snapshot => snapshot with
            {
                BestLapTimeInMs = bestLapTimeInMs
            },
            receivedAt);
    }

    private void ApplyCarDamage(CarDamagePacket packet, DateTimeOffset receivedAt)
    {
        for (var carIndex = 0; carIndex < packet.Cars.Length; carIndex++)
        {
            if (!SessionStateStore.CarStateStore.HasTelemetryAccess(carIndex))
            {
                SessionStateStore.CarStateStore.ClearRestrictedTelemetry(carIndex, receivedAt);
                continue;
            }

            var car = packet.Cars[carIndex];
            SessionStateStore.CarStateStore.UpdateCar(
                carIndex,
                snapshot => snapshot with
                {
                    TyreWear = (car.TyreWear.RearLeft + car.TyreWear.RearRight + car.TyreWear.FrontLeft + car.TyreWear.FrontRight) / 4f,
                    FrontLeftWingDamage = car.FrontLeftWingDamage,
                    FrontRightWingDamage = car.FrontRightWingDamage,
                    RearWingDamage = car.RearWingDamage
                },
                receivedAt);
        }
    }

    private void ApplyMotion(MotionPacket packet, DateTimeOffset receivedAt)
    {
        for (var carIndex = 0; carIndex < packet.Cars.Length; carIndex++)
        {
            if (!SessionStateStore.CarStateStore.HasTelemetryAccess(carIndex))
            {
                SessionStateStore.CarStateStore.ClearRestrictedTelemetry(carIndex, receivedAt);
                continue;
            }

            var car = packet.Cars[carIndex];
            SessionStateStore.CarStateStore.UpdateCar(
                carIndex,
                snapshot => snapshot with
                {
                    WorldPositionX = car.WorldPosition.X,
                    WorldPositionY = car.WorldPosition.Y,
                    WorldPositionZ = car.WorldPosition.Z
                },
                receivedAt);
        }
    }

    private void ApplyEvent(EventPacket packet, DateTimeOffset receivedAt)
    {
        SessionStateStore.SetLastEventCode(packet.RawEventCode, receivedAt);
    }

    private void ApplyTyreSets(TyreSetsPacket packet, DateTimeOffset receivedAt)
    {
        var carIndex = packet.CarIndex;
        if (carIndex >= 22 || !SessionStateStore.CarStateStore.HasTelemetryAccess(carIndex))
        {
            return;
        }

        var fittedIndex = packet.FittedIndex;
        if (fittedIndex >= packet.TyreSets.Length)
        {
            return;
        }

        var tyreSet = packet.TyreSets[fittedIndex];
        SessionStateStore.CarStateStore.UpdateCar(
            carIndex,
            snapshot => snapshot with
            {
                ActualTyreCompound = tyreSet.ActualTyreCompound,
                VisualTyreCompound = tyreSet.VisualTyreCompound
            },
            receivedAt);
    }

    private static IReadOnlyDictionary<int, sbyte> BuildMarshalZoneFlags(SessionPacket packet)
    {
        var zoneCount = Math.Min(packet.NumMarshalZones, packet.MarshalZones.Length);
        var flags = new Dictionary<int, sbyte>(zoneCount);
        for (var index = 0; index < zoneCount; index++)
        {
            flags[index] = packet.MarshalZones[index].ZoneFlag;
        }

        return flags;
    }
}
