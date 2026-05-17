using System.Text.Json;
using F1Telemetry.Analytics.Interfaces;
using F1Telemetry.Core.Formatting;
using F1Telemetry.Core.Models;

namespace F1Telemetry.Analytics.Events;

/// <summary>
/// Detects reusable race events by comparing successive aggregate session-state snapshots.
/// </summary>
public sealed class EventDetectionService : IEventDetectionService
{
    private const int MaxPendingEvents = 200;
    private readonly object _gate = new();
    private readonly EventDetectionOptions _options;
    private readonly Queue<RaceEvent> _pendingEvents = new();
    private readonly Dictionary<string, DateTimeOffset> _lastRaisedAtByDedupKey = new(StringComparer.Ordinal);
    private readonly HashSet<string> _highTyreWearTyreKeys = new(StringComparer.Ordinal);
    private readonly HashSet<string> _activeTyreTemperatureAlertKeys = new(StringComparer.Ordinal);
    private readonly HashSet<string> _dataQualityWarningKeys = new(StringComparer.Ordinal);
    private readonly Dictionary<int, sbyte> _lastMarshalZoneFlags = new();
    private readonly Dictionary<int, PitCandidate> _pitCandidates = new();
    private byte? _lastSafetyCarStatus;
    private bool _attackWindowArmed = true;
    private bool _defenseWindowArmed = true;
    private bool _lowErsArmed = true;
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
            DetectSafetyCarStatus(sessionState);
            DetectMarshalZoneFlags(sessionState);

            if (previousState is not null)
            {
                if (IsRaceLikeSession(sessionState))
                {
                    DetectPitEvents(previousState, sessionState);
                    DetectRaceGapWindows(sessionState);
                    DetectLowErs(sessionState);
                    DetectMissingRaceTrendEvidence(sessionState);
                }

                DetectLightweightAdvice(sessionState);
                DetectPlayerLapInvalidated(previousState, sessionState);
                DetectLowFuel(previousState, sessionState);
                DetectHighTyreWear(previousState, sessionState);
                DetectTyreTemperature(sessionState);
                DetectPlayerDamage(previousState, sessionState);
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

    /// <inheritdoc />
    public void Reset()
    {
        lock (_gate)
        {
            _pendingEvents.Clear();
            _lastRaisedAtByDedupKey.Clear();
            _highTyreWearTyreKeys.Clear();
            _activeTyreTemperatureAlertKeys.Clear();
            _dataQualityWarningKeys.Clear();
            _lastMarshalZoneFlags.Clear();
            _pitCandidates.Clear();
            _lastSafetyCarStatus = null;
            _attackWindowArmed = true;
            _defenseWindowArmed = true;
            _lowErsArmed = true;
            _previousState = null;
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
            message: $"第 {lapNumber?.ToString() ?? "-"} 圈已无效。",
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

    private void DetectTyreTemperature(SessionState currentState)
    {
        var player = currentState.PlayerCar;
        if (player?.TyreCondition is null || currentState.TrackTemperature is null)
        {
            return;
        }

        var thresholds = BuildTyreTemperatureThresholds(currentState.TrackTemperature.Value);
        var wheels = EnumerateTyreTemperatureWheels(player.TyreCondition).ToArray();
        var hottestWheel = wheels
            .Where(wheel => IsHighTyreTemperatureCandidate(wheel, thresholds))
            .OrderByDescending(wheel => GetHighTyreTemperatureScore(wheel, thresholds))
            .FirstOrDefault();
        if (hottestWheel is not null)
        {
            DetectHighTyreTemperature(currentState, player, hottestWheel, thresholds);
        }
        else if (wheels.All(wheel => HasRecoveredFromHighTyreTemperature(wheel, thresholds)))
        {
            _activeTyreTemperatureAlertKeys.Remove("tyre-temp:high");
        }

        var coldestWheel = wheels
            .Where(wheel => IsLowTyreTemperatureCandidate(wheel, thresholds))
            .OrderByDescending(wheel => GetLowTyreTemperatureScore(wheel, thresholds))
            .FirstOrDefault();
        if (coldestWheel is not null)
        {
            DetectLowTyreTemperature(currentState, player, coldestWheel, thresholds);
        }
        else if (wheels.All(wheel => HasRecoveredFromLowTyreTemperature(wheel, thresholds)))
        {
            _activeTyreTemperatureAlertKeys.Remove("tyre-temp:low");
        }
    }

    private void DetectHighTyreTemperature(
        SessionState currentState,
        CarSnapshot player,
        TyreTemperatureWheelSample wheel,
        TyreTemperatureThresholds thresholds)
    {
        const string AlertKey = "tyre-temp:high";
        const string OppositeAlertKey = "tyre-temp:low";
        _activeTyreTemperatureAlertKeys.Remove(OppositeAlertKey);
        if (_activeTyreTemperatureAlertKeys.Contains(AlertKey))
        {
            return;
        }

        if (EmitEvent(
                eventType: EventType.HighTyreTemperature,
                severity: EventSeverity.Warning,
                timestamp: currentState.UpdatedAt,
                lapNumber: (int?)player.CurrentLapNumber,
                vehicleIdx: player.CarIndex,
                driverName: player.DriverName,
                message: $"{wheel.DisplayName}胎温偏高，表面 {wheel.SurfaceTemperatureCelsius}°C，内层 {wheel.InnerTemperatureCelsius}°C；赛道 {thresholds.TrackTemperatureCelsius:0}°C，保护轮胎避免连续打滑。",
                dedupKey: AlertKey,
                payload: BuildTyreTemperaturePayload(currentState, player, wheel, thresholds)))
        {
            _activeTyreTemperatureAlertKeys.Add(AlertKey);
        }
    }

    private void DetectLowTyreTemperature(
        SessionState currentState,
        CarSnapshot player,
        TyreTemperatureWheelSample wheel,
        TyreTemperatureThresholds thresholds)
    {
        const string AlertKey = "tyre-temp:low";
        const string OppositeAlertKey = "tyre-temp:high";
        _activeTyreTemperatureAlertKeys.Remove(OppositeAlertKey);
        if (_activeTyreTemperatureAlertKeys.Contains(AlertKey))
        {
            return;
        }

        if (EmitEvent(
                eventType: EventType.LowTyreTemperature,
                severity: EventSeverity.Warning,
                timestamp: currentState.UpdatedAt,
                lapNumber: (int?)player.CurrentLapNumber,
                vehicleIdx: player.CarIndex,
                driverName: player.DriverName,
                message: $"{wheel.DisplayName}胎温偏低，表面 {wheel.SurfaceTemperatureCelsius}°C，内层 {wheel.InnerTemperatureCelsius}°C；赛道 {thresholds.TrackTemperatureCelsius:0}°C，出弯前先把轮胎带起来。",
                dedupKey: AlertKey,
                payload: BuildTyreTemperaturePayload(currentState, player, wheel, thresholds)))
        {
            _activeTyreTemperatureAlertKeys.Add(AlertKey);
        }
    }

    private void DetectPlayerDamage(SessionState previousState, SessionState currentState)
    {
        var previousPlayer = previousState.PlayerCar;
        var currentPlayer = currentState.PlayerCar;
        var currentDamage = currentPlayer?.Damage;
        if (currentPlayer is null || currentDamage is null)
        {
            return;
        }

        var previousDamage = previousPlayer?.Damage;
        foreach (var pair in currentDamage.Components.OrderBy(pair => pair.Key))
        {
            var component = pair.Key;
            if (IsDrivetrainWearComponent(component))
            {
                continue;
            }

            var damagePercent = pair.Value;
            var currentSeverity = DamageSnapshot.Classify(damagePercent);
            var previousSeverity = previousDamage?.GetSeverity(component) ?? DamageSeverity.None;
            if (currentSeverity == DamageSeverity.None || currentSeverity <= previousSeverity)
            {
                continue;
            }

            EmitEvent(
                eventType: EventType.CarDamage,
                severity: MapDamageEventSeverity(currentSeverity),
                timestamp: currentState.UpdatedAt,
                lapNumber: (int?)currentPlayer.CurrentLapNumber,
                vehicleIdx: currentPlayer.CarIndex,
                driverName: currentPlayer.DriverName,
                message: $"{FormatDamageComponent(component)} {damagePercent}%（{FormatDamageSeverity(currentSeverity)}损伤）。",
                dedupKey: $"damage:{component}:{currentSeverity}",
                payload: new
                {
                    currentPlayer.CarIndex,
                    currentPlayer.CurrentLapNumber,
                    Component = component.ToString(),
                    DamagePercent = damagePercent,
                    Severity = currentSeverity.ToString(),
                    PreviousSeverity = previousSeverity.ToString()
                });
        }

        if (previousDamage?.DrsFault != true && currentDamage.DrsFault)
        {
            EmitFaultEvent(
                currentState,
                currentPlayer,
                EventType.DrsFault,
                "DRS 故障，避免依赖尾翼开启。",
                "drs",
                new { currentPlayer.CarIndex, currentPlayer.CurrentLapNumber, currentDamage.DrsFault });
        }

        if (previousDamage?.ErsFault != true && currentDamage.ErsFault)
        {
            EmitFaultEvent(
                currentState,
                currentPlayer,
                EventType.ErsFault,
                "ERS 故障，优先管理电量输出。",
                "ers",
                new { currentPlayer.CarIndex, currentPlayer.CurrentLapNumber, currentDamage.ErsFault });
        }

        if (previousDamage?.EngineBlown != true && currentDamage.EngineBlown)
        {
            EmitFaultEvent(
                currentState,
                currentPlayer,
                EventType.EngineFailure,
                "引擎爆缸，立即保护车辆。",
                "engine-blown",
                new { currentPlayer.CarIndex, currentPlayer.CurrentLapNumber, currentDamage.EngineBlown });
        }

        if (previousDamage?.EngineSeized != true && currentDamage.EngineSeized)
        {
            EmitFaultEvent(
                currentState,
                currentPlayer,
                EventType.EngineFailure,
                "引擎卡死，立即停车避险。",
                "engine-seized",
                new { currentPlayer.CarIndex, currentPlayer.CurrentLapNumber, currentDamage.EngineSeized });
        }
    }

    private void DetectSafetyCarStatus(SessionState currentState)
    {
        if (currentState.SafetyCarStatus is not { } safetyCarStatus)
        {
            return;
        }

        var previousSafetyCarStatus = _lastSafetyCarStatus;
        if (previousSafetyCarStatus == safetyCarStatus)
        {
            return;
        }

        _lastSafetyCarStatus = safetyCarStatus;
        if (IsRaceLikeSession(currentState) && IsActiveSafetyCarStatus(previousSafetyCarStatus) && safetyCarStatus == 0)
        {
            EmitEvent(
                eventType: EventType.SafetyCarRestart,
                severity: EventSeverity.Warning,
                timestamp: currentState.UpdatedAt,
                lapNumber: (int?)currentState.PlayerCar?.CurrentLapNumber,
                vehicleIdx: null,
                driverName: null,
                message: "安全车状态解除，准备重启，注意轮胎温度和前后车。",
                dedupKey: "advice:safety-car-restart",
                payload: new
                {
                    PreviousSafetyCarStatus = previousSafetyCarStatus,
                    SafetyCarStatus = safetyCarStatus
                },
                cooldownSeconds: _options.SafetyCarRestartCooldownSeconds);
        }

        switch (safetyCarStatus)
        {
            case 0:
            case 3:
                return;
            case 1:
                EmitEvent(
                    eventType: EventType.SafetyCar,
                    severity: EventSeverity.Warning,
                    timestamp: currentState.UpdatedAt,
                    lapNumber: (int?)currentState.PlayerCar?.CurrentLapNumber,
                    vehicleIdx: null,
                    driverName: null,
                    message: "安全车出动，保持 delta 并注意前车。",
                    dedupKey: "safety-car:full",
                    payload: new { SafetyCarStatus = safetyCarStatus });
                return;
            case 2:
                EmitEvent(
                    eventType: EventType.VirtualSafetyCar,
                    severity: EventSeverity.Warning,
                    timestamp: currentState.UpdatedAt,
                    lapNumber: (int?)currentState.PlayerCar?.CurrentLapNumber,
                    vehicleIdx: null,
                    driverName: null,
                    message: "虚拟安全车出动，保持 delta。",
                    dedupKey: "safety-car:virtual",
                    payload: new { SafetyCarStatus = safetyCarStatus });
                return;
            default:
                EmitDataQualityWarning(
                    key: $"safety-car-status:{safetyCarStatus}",
                    timestamp: currentState.UpdatedAt,
                    lapNumber: (int?)currentState.PlayerCar?.CurrentLapNumber,
                    message: $"未知安全车状态 {safetyCarStatus}，已跳过安全车播报。");
                return;
        }
    }

    private void DetectMarshalZoneFlags(SessionState currentState)
    {
        foreach (var pair in currentState.MarshalZoneFlags.OrderBy(pair => pair.Key))
        {
            var zoneIndex = pair.Key;
            var zoneFlag = pair.Value;
            if (!IsKnownMarshalZoneFlag(zoneFlag))
            {
                EmitDataQualityWarning(
                    key: $"marshal-zone:{zoneIndex}:{zoneFlag}",
                    timestamp: currentState.UpdatedAt,
                    lapNumber: (int?)currentState.PlayerCar?.CurrentLapNumber,
                    message: $"旗区 {zoneIndex} 出现未知旗帜状态 {zoneFlag}，已跳过旗帜播报。");
                continue;
            }

            var hadPreviousFlag = _lastMarshalZoneFlags.TryGetValue(zoneIndex, out var previousFlag);
            if (hadPreviousFlag && previousFlag == zoneFlag)
            {
                continue;
            }

            _lastMarshalZoneFlags[zoneIndex] = zoneFlag;
            switch (zoneFlag)
            {
                case 3:
                    EmitEvent(
                        eventType: EventType.YellowFlag,
                        severity: EventSeverity.Warning,
                        timestamp: currentState.UpdatedAt,
                        lapNumber: (int?)currentState.PlayerCar?.CurrentLapNumber,
                        vehicleIdx: null,
                        driverName: null,
                        message: $"黄旗，旗区 {zoneIndex}，注意减速。",
                        dedupKey: $"marshal-zone:{zoneIndex}:yellow",
                        payload: new { ZoneIndex = zoneIndex, ZoneFlag = zoneFlag });
                    break;
                case 4:
                    EmitEvent(
                        eventType: EventType.RedFlag,
                        severity: EventSeverity.Warning,
                        timestamp: currentState.UpdatedAt,
                        lapNumber: (int?)currentState.PlayerCar?.CurrentLapNumber,
                        vehicleIdx: null,
                        driverName: null,
                        message: $"红旗，旗区 {zoneIndex}，准备减速停车。",
                        dedupKey: $"marshal-zone:{zoneIndex}:red",
                        payload: new { ZoneIndex = zoneIndex, ZoneFlag = zoneFlag });
                    if (hadPreviousFlag && previousFlag != 4)
                    {
                        DetectRedFlagTyreChange(currentState, zoneIndex, previousFlag, zoneFlag);
                    }

                    break;
            }
        }
    }

    private void DetectRaceGapWindows(SessionState currentState)
    {
        var player = currentState.PlayerCar;
        if (player?.Position is null)
        {
            return;
        }

        DetectAttackWindow(currentState, player);
        DetectDefenseWindow(currentState, player);
    }

    private void DetectLightweightAdvice(SessionState currentState)
    {
        if (IsRaceLikeSession(currentState))
        {
            DetectFrontOldTyreRisk(currentState);
            DetectRearNewTyrePressure(currentState);
            DetectTrafficRisk(currentState);
            DetectRacePitWindow(currentState);
            return;
        }

        if (IsQualifyingLikeSession(currentState))
        {
            DetectTrafficRisk(currentState);
            DetectQualifyingCleanAirWindow(currentState);
        }
    }

    private void DetectFrontOldTyreRisk(SessionState currentState)
    {
        var player = currentState.PlayerCar;
        if (player is null || player.TyresAgeLaps is null)
        {
            return;
        }

        var frontCar = FindDirectFrontCar(currentState, player);
        if (frontCar is null || frontCar.TyresAgeLaps is null)
        {
            return;
        }

        if (!TryGetSameLapFrontGapMs(player, frontCar, out var gapFrontMs)
            || gapFrontMs > _options.TrafficRiskGapMs)
        {
            return;
        }

        var tyreAgeDeltaLaps = frontCar.TyresAgeLaps.Value - player.TyresAgeLaps.Value;
        if (tyreAgeDeltaLaps < _options.OldTyreAgeDeltaLapsThreshold)
        {
            return;
        }

        EmitEvent(
            eventType: EventType.FrontOldTyreRisk,
            severity: EventSeverity.Warning,
            timestamp: currentState.UpdatedAt,
            lapNumber: (int?)player.CurrentLapNumber,
            vehicleIdx: frontCar.CarIndex,
            driverName: frontCar.DriverName,
            message: $"前车轮胎比你旧 {tyreAgeDeltaLaps} 圈，差距 {gapFrontMs} 毫秒，留意其制动和牵引波动，等待稳定窗口。",
            dedupKey: "advice:front-old-tyre",
            payload: new
            {
                PlayerCarIndex = player.CarIndex,
                FrontCarIndex = frontCar.CarIndex,
                player.CurrentLapNumber,
                GapFrontMs = gapFrontMs,
                PlayerTyresAgeLaps = player.TyresAgeLaps,
                FrontTyresAgeLaps = frontCar.TyresAgeLaps,
                TyreAgeDeltaLaps = tyreAgeDeltaLaps,
                ThresholdLaps = _options.OldTyreAgeDeltaLapsThreshold
            },
            cooldownSeconds: _options.AdviceCooldownSeconds);
    }

    private void DetectRearNewTyrePressure(SessionState currentState)
    {
        var player = currentState.PlayerCar;
        if (player is null || player.TyresAgeLaps is null)
        {
            return;
        }

        var rearCar = FindDirectRearCar(currentState, player);
        if (rearCar is null || rearCar.TyresAgeLaps is null)
        {
            return;
        }

        if (!TryGetSameLapRearGapMs(player, rearCar, out var gapBehindMs)
            || gapBehindMs > _options.TrafficRiskGapMs)
        {
            return;
        }

        var tyreAgeDeltaLaps = player.TyresAgeLaps.Value - rearCar.TyresAgeLaps.Value;
        if (tyreAgeDeltaLaps < _options.NewTyrePressureAgeDeltaLapsThreshold)
        {
            return;
        }

        EmitEvent(
            eventType: EventType.RearNewTyrePressure,
            severity: EventSeverity.Warning,
            timestamp: currentState.UpdatedAt,
            lapNumber: (int?)player.CurrentLapNumber,
            vehicleIdx: rearCar.CarIndex,
            driverName: rearCar.DriverName,
            message: $"后车轮胎比你新 {tyreAgeDeltaLaps} 圈，差距 {gapBehindMs} 毫秒，注意防守线和出弯牵引。",
            dedupKey: "advice:rear-new-tyre",
            payload: new
            {
                PlayerCarIndex = player.CarIndex,
                RearCarIndex = rearCar.CarIndex,
                player.CurrentLapNumber,
                GapBehindMs = gapBehindMs,
                PlayerTyresAgeLaps = player.TyresAgeLaps,
                RearTyresAgeLaps = rearCar.TyresAgeLaps,
                TyreAgeDeltaLaps = tyreAgeDeltaLaps,
                ThresholdLaps = _options.NewTyrePressureAgeDeltaLapsThreshold
            },
            cooldownSeconds: _options.AdviceCooldownSeconds);
    }

    private void DetectTrafficRisk(SessionState currentState)
    {
        var player = currentState.PlayerCar;
        if (player is null)
        {
            return;
        }

        var frontCar = FindDirectFrontCar(currentState, player);
        if (frontCar is null
            || !TryGetSameLapFrontGapMs(player, frontCar, out var gapFrontMs)
            || gapFrontMs > _options.TrafficRiskGapMs)
        {
            return;
        }

        EmitEvent(
            eventType: EventType.TrafficRisk,
            severity: EventSeverity.Warning,
            timestamp: currentState.UpdatedAt,
            lapNumber: (int?)player.CurrentLapNumber,
            vehicleIdx: frontCar.CarIndex,
            driverName: frontCar.DriverName,
            message: $"前方同圈车辆差距 {gapFrontMs} 毫秒，注意交通风险并保留安全余量。",
            dedupKey: "advice:traffic",
            payload: new
            {
                PlayerCarIndex = player.CarIndex,
                FrontCarIndex = frontCar.CarIndex,
                player.CurrentLapNumber,
                GapFrontMs = gapFrontMs,
                ThresholdMs = _options.TrafficRiskGapMs,
                SessionMode = SessionModeFormatter.Resolve(currentState.SessionType).ToString()
            },
            cooldownSeconds: _options.AdviceCooldownSeconds);
    }

    private void DetectQualifyingCleanAirWindow(SessionState currentState)
    {
        var player = currentState.PlayerCar;
        if (player is null || player.Position is null || player.CurrentLapNumber is null)
        {
            return;
        }

        ushort? gapFrontMs = null;
        var frontCar = FindDirectFrontCar(currentState, player);
        if (frontCar is not null)
        {
            if (frontCar.CurrentLapNumber is null)
            {
                return;
            }

            if (frontCar.CurrentLapNumber == player.CurrentLapNumber)
            {
                if (player.DeltaToCarInFrontInMs is not { } knownGapFrontMs)
                {
                    return;
                }

                if (!IsUsableGapMs(knownGapFrontMs))
                {
                    return;
                }

                if (knownGapFrontMs <= _options.QualifyingCleanAirGapMs)
                {
                    return;
                }

                gapFrontMs = knownGapFrontMs;
            }
        }

        ushort? gapBehindMs = null;
        var rearCar = FindDirectRearCar(currentState, player);
        if (rearCar is not null)
        {
            if (rearCar.CurrentLapNumber is null)
            {
                return;
            }

            if (rearCar.CurrentLapNumber == player.CurrentLapNumber)
            {
                if (rearCar.DeltaToCarInFrontInMs is not { } knownGapBehindMs)
                {
                    return;
                }

                if (!IsUsableGapMs(knownGapBehindMs))
                {
                    return;
                }

                if (knownGapBehindMs <= _options.QualifyingCleanAirGapMs)
                {
                    return;
                }

                gapBehindMs = knownGapBehindMs;
            }
        }

        EmitEvent(
            eventType: EventType.QualifyingCleanAirWindow,
            severity: EventSeverity.Information,
            timestamp: currentState.UpdatedAt,
            lapNumber: (int?)player.CurrentLapNumber,
            vehicleIdx: player.CarIndex,
            driverName: player.DriverName,
            message: $"前后 {_options.QualifyingCleanAirGapMs} 毫秒内没有同圈车辆，当前是清洁空气窗口，专注完成本圈。",
            dedupKey: "advice:qualifying-clean-air",
            payload: new
            {
                player.CarIndex,
                player.CurrentLapNumber,
                GapFrontMs = gapFrontMs,
                GapBehindMs = gapBehindMs,
                ThresholdMs = _options.QualifyingCleanAirGapMs,
                SessionMode = SessionModeFormatter.Resolve(currentState.SessionType).ToString()
            },
            cooldownSeconds: _options.AdviceCooldownSeconds);
    }

    private void DetectRacePitWindow(SessionState currentState)
    {
        var player = currentState.PlayerCar;
        if (player is null)
        {
            return;
        }

        var tyreAgeTriggered = player.TyresAgeLaps.HasValue
            && player.TyresAgeLaps.Value >= _options.RacePitWindowTyreAgeLapsThreshold;
        var tyreWearTriggered = player.TyreWear.HasValue
            && player.TyreWear.Value >= _options.RacePitWindowTyreWearThreshold;
        if (!tyreAgeTriggered && !tyreWearTriggered)
        {
            return;
        }

        EmitEvent(
            eventType: EventType.RacePitWindow,
            severity: EventSeverity.Information,
            timestamp: currentState.UpdatedAt,
            lapNumber: (int?)player.CurrentLapNumber,
            vehicleIdx: player.CarIndex,
            driverName: player.DriverName,
            message: $"{FormatTyreState(player.TyresAgeLaps, player.TyreWear)}，已接近进站窗口，准备或考虑进站时机。",
            dedupKey: "advice:race-pit-window",
            payload: new
            {
                player.CarIndex,
                player.CurrentLapNumber,
                player.TyresAgeLaps,
                player.TyreWear,
                TyreAgeThresholdLaps = _options.RacePitWindowTyreAgeLapsThreshold,
                TyreWearThreshold = _options.RacePitWindowTyreWearThreshold,
                TyreAgeTriggered = tyreAgeTriggered,
                TyreWearTriggered = tyreWearTriggered
            },
            cooldownSeconds: _options.AdviceCooldownSeconds);
    }

    private void DetectAttackWindow(SessionState currentState, CarSnapshot player)
    {
        var playerPosition = player.Position.GetValueOrDefault();
        if (playerPosition <= 1)
        {
            _attackWindowArmed = true;
            return;
        }

        if (player.DeltaToCarInFrontInMs is not { } gapFrontMs)
        {
            _attackWindowArmed = true;
            EmitDataQualityWarning(
                key: "gap-front-missing",
                timestamp: currentState.UpdatedAt,
                lapNumber: (int?)player.CurrentLapNumber,
                message: "前车差距数据不可用，已跳过攻击窗口播报。");
            return;
        }

        if (!IsUsableGapMs(gapFrontMs))
        {
            _attackWindowArmed = true;
            return;
        }

        if (gapFrontMs > _options.GapWindowResetThresholdMs)
        {
            _attackWindowArmed = true;
            return;
        }

        if (gapFrontMs <= _options.AttackDefenseGapThresholdMs && _attackWindowArmed)
        {
            EmitEvent(
                eventType: EventType.AttackWindow,
                severity: EventSeverity.Warning,
                timestamp: currentState.UpdatedAt,
                lapNumber: (int?)player.CurrentLapNumber,
                vehicleIdx: player.CarIndex,
                driverName: player.DriverName,
                message: $"前车差距 {gapFrontMs} 毫秒，进入攻击窗口。",
                dedupKey: "gap:attack-window",
                payload: new
                {
                    player.CarIndex,
                    player.CurrentLapNumber,
                    GapFrontMs = gapFrontMs,
                    ThresholdMs = _options.AttackDefenseGapThresholdMs
                },
                cooldownSeconds: _options.RaceWindowCooldownSeconds);
            _attackWindowArmed = false;
        }
    }

    private void DetectDefenseWindow(SessionState currentState, CarSnapshot player)
    {
        if (player.Position is null || player.CurrentLapNumber is null)
        {
            _defenseWindowArmed = true;
            return;
        }

        var playerPosition = player.Position.Value;
        var activeCarCount = currentState.ActiveCarCount.HasValue
            ? currentState.ActiveCarCount.Value
            : currentState.Cars.Count;
        if (activeCarCount > 0 && playerPosition >= activeCarCount)
        {
            _defenseWindowArmed = true;
            return;
        }

        var rearPosition = playerPosition + 1;
        var rearCar = currentState.Cars.FirstOrDefault(car => car.Position.HasValue && car.Position.Value == rearPosition);
        if (rearCar is null)
        {
            _defenseWindowArmed = true;
            EmitDataQualityWarning(
                key: "gap-behind-missing-adjacent",
                timestamp: currentState.UpdatedAt,
                lapNumber: (int?)player.CurrentLapNumber,
                message: "后车数据不可用，已跳过防守窗口播报。");
            return;
        }

        if (rearCar.CurrentLapNumber != player.CurrentLapNumber)
        {
            _defenseWindowArmed = true;
            EmitDataQualityWarning(
                key: "gap-behind-lap-mismatch",
                timestamp: currentState.UpdatedAt,
                lapNumber: (int?)player.CurrentLapNumber,
                message: "后车不在同一圈，已跳过防守窗口播报。");
            return;
        }

        if (rearCar.DeltaToCarInFrontInMs is not { } gapBehindMs)
        {
            _defenseWindowArmed = true;
            EmitDataQualityWarning(
                key: "gap-behind-timing-missing",
                timestamp: currentState.UpdatedAt,
                lapNumber: (int?)player.CurrentLapNumber,
                message: "后车差距数据不可用，已跳过防守窗口播报。");
            return;
        }

        if (!IsUsableGapMs(gapBehindMs))
        {
            _defenseWindowArmed = true;
            return;
        }

        if (gapBehindMs > _options.GapWindowResetThresholdMs)
        {
            _defenseWindowArmed = true;
            return;
        }

        if (gapBehindMs <= _options.AttackDefenseGapThresholdMs && _defenseWindowArmed)
        {
            EmitEvent(
                eventType: EventType.DefenseWindow,
                severity: EventSeverity.Warning,
                timestamp: currentState.UpdatedAt,
                lapNumber: (int?)player.CurrentLapNumber,
                vehicleIdx: rearCar.CarIndex,
                driverName: rearCar.DriverName,
                message: $"后车差距 {gapBehindMs} 毫秒，注意防守。",
                dedupKey: "gap:defense-window",
                payload: new
                {
                    PlayerCarIndex = player.CarIndex,
                    RearCarIndex = rearCar.CarIndex,
                    player.CurrentLapNumber,
                    GapBehindMs = gapBehindMs,
                    ThresholdMs = _options.AttackDefenseGapThresholdMs
                },
                cooldownSeconds: _options.RaceWindowCooldownSeconds);
            _defenseWindowArmed = false;
        }
    }

    private void DetectLowErs(SessionState currentState)
    {
        var player = currentState.PlayerCar;
        if (player is null)
        {
            return;
        }

        if (player.ErsStoreEnergy is not { } ersStoreEnergy)
        {
            _lowErsArmed = true;
            EmitDataQualityWarning(
                key: "ers-store-missing",
                timestamp: currentState.UpdatedAt,
                lapNumber: (int?)player.CurrentLapNumber,
                message: "ERS 电量数据不可用，已跳过低 ERS 播报。");
            return;
        }

        if (ersStoreEnergy >= _options.LowErsStoreEnergyThresholdJoules)
        {
            _lowErsArmed = true;
            return;
        }

        if (!_lowErsArmed)
        {
            return;
        }

        EmitEvent(
            eventType: EventType.LowErs,
            severity: EventSeverity.Warning,
            timestamp: currentState.UpdatedAt,
            lapNumber: (int?)player.CurrentLapNumber,
            vehicleIdx: player.CarIndex,
            driverName: player.DriverName,
            message: $"ERS 剩余 {ersStoreEnergy:0} 焦耳，注意省电。",
            dedupKey: "low-ers",
            payload: new
            {
                player.CarIndex,
                player.CurrentLapNumber,
                player.ErsStoreEnergy,
                ThresholdJoules = _options.LowErsStoreEnergyThresholdJoules
            },
            cooldownSeconds: _options.RaceWindowCooldownSeconds);
        _lowErsArmed = false;
    }

    private void DetectMissingRaceTrendEvidence(SessionState currentState)
    {
        var player = currentState.PlayerCar;
        if (player is null)
        {
            return;
        }

        if (player.FuelRemainingLaps is null)
        {
            EmitDataQualityWarning(
                key: "fuel-remaining-missing",
                timestamp: currentState.UpdatedAt,
                lapNumber: (int?)player.CurrentLapNumber,
                message: "剩余燃油圈数不可用，已跳过低油风险播报。");
        }

        if (player.TyreWear is null)
        {
            EmitDataQualityWarning(
                key: "tyre-wear-missing",
                timestamp: currentState.UpdatedAt,
                lapNumber: (int?)player.CurrentLapNumber,
                message: "轮胎磨损数据不可用，已跳过高胎磨风险播报。");
        }
    }

    private void DetectRedFlagTyreChange(SessionState currentState, int zoneIndex, sbyte previousFlag, sbyte zoneFlag)
    {
        if (!IsRaceLikeSession(currentState))
        {
            return;
        }

        var player = currentState.PlayerCar;
        if (player is null || (player.TyresAgeLaps is null && player.TyreWear is null))
        {
            return;
        }

        EmitEvent(
            eventType: EventType.RedFlagTyreChange,
            severity: EventSeverity.Information,
            timestamp: currentState.UpdatedAt,
            lapNumber: (int?)player.CurrentLapNumber,
            vehicleIdx: player.CarIndex,
            driverName: player.DriverName,
            message: $"红旗，旗区 {zoneIndex}。{FormatTyreState(player.TyresAgeLaps, player.TyreWear)}，复盘是否有换胎机会。",
            dedupKey: $"advice:red-flag-tyre-change:{zoneIndex}:{player.CurrentLapNumber?.ToString() ?? "-"}",
            payload: new
            {
                ZoneIndex = zoneIndex,
                PreviousFlag = previousFlag,
                ZoneFlag = zoneFlag,
                player.CarIndex,
                player.CurrentLapNumber,
                player.TyresAgeLaps,
                player.TyreWear
            },
            cooldownSeconds: _options.AdviceCooldownSeconds);
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
        object payload,
        int? cooldownSeconds = null)
    {
        var effectiveCooldownSeconds = Math.Max(1, cooldownSeconds ?? _options.EventCooldownSeconds);
        if (_lastRaisedAtByDedupKey.TryGetValue(dedupKey, out var lastRaisedAt)
            && timestamp - lastRaisedAt < TimeSpan.FromSeconds(effectiveCooldownSeconds))
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
        while (_pendingEvents.Count > MaxPendingEvents)
        {
            _pendingEvents.Dequeue();
        }

        _lastRaisedAtByDedupKey[dedupKey] = timestamp;
        return true;
    }

    private void EmitDataQualityWarning(string key, DateTimeOffset timestamp, int? lapNumber, string message)
    {
        if (!_dataQualityWarningKeys.Add(key))
        {
            return;
        }

        EmitEvent(
            eventType: EventType.DataQualityWarning,
            severity: EventSeverity.Information,
            timestamp: timestamp,
            lapNumber: lapNumber,
            vehicleIdx: null,
            driverName: null,
            message: message,
            dedupKey: $"data-quality:{key}",
            payload: new
            {
                Key = key,
                Message = message
            },
            cooldownSeconds: _options.RaceWindowCooldownSeconds);
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

    private TyreTemperatureThresholds BuildTyreTemperatureThresholds(sbyte trackTemperatureCelsius)
    {
        var trackDelta = Math.Clamp(
            trackTemperatureCelsius - _options.TyreTemperatureBaselineTrackCelsius,
            -20.0f,
            20.0f);

        return new TyreTemperatureThresholds(
            TrackTemperatureCelsius: trackTemperatureCelsius,
            HighSurfaceCelsius: _options.HighTyreSurfaceTemperatureBaselineCelsius + (trackDelta * 0.25f),
            HighInnerCelsius: _options.HighTyreInnerTemperatureBaselineCelsius + (trackDelta * 0.15f),
            LowSurfaceCelsius: _options.LowTyreSurfaceTemperatureBaselineCelsius + (trackDelta * 0.20f),
            LowInnerCelsius: _options.LowTyreInnerTemperatureBaselineCelsius + (trackDelta * 0.10f));
    }

    private static IEnumerable<TyreTemperatureWheelSample> EnumerateTyreTemperatureWheels(TyreConditionSnapshot tyreCondition)
    {
        yield return new TyreTemperatureWheelSample(
            "rear-left",
            "后左轮",
            tyreCondition.SurfaceTemperatureCelsius.RearLeft,
            tyreCondition.InnerTemperatureCelsius.RearLeft,
            tyreCondition.PressurePsi.RearLeft);
        yield return new TyreTemperatureWheelSample(
            "rear-right",
            "后右轮",
            tyreCondition.SurfaceTemperatureCelsius.RearRight,
            tyreCondition.InnerTemperatureCelsius.RearRight,
            tyreCondition.PressurePsi.RearRight);
        yield return new TyreTemperatureWheelSample(
            "front-left",
            "前左轮",
            tyreCondition.SurfaceTemperatureCelsius.FrontLeft,
            tyreCondition.InnerTemperatureCelsius.FrontLeft,
            tyreCondition.PressurePsi.FrontLeft);
        yield return new TyreTemperatureWheelSample(
            "front-right",
            "前右轮",
            tyreCondition.SurfaceTemperatureCelsius.FrontRight,
            tyreCondition.InnerTemperatureCelsius.FrontRight,
            tyreCondition.PressurePsi.FrontRight);
    }

    private static object BuildTyreTemperaturePayload(
        SessionState currentState,
        CarSnapshot player,
        TyreTemperatureWheelSample wheel,
        TyreTemperatureThresholds thresholds)
    {
        return new
        {
            player.CarIndex,
            player.CurrentLapNumber,
            Wheel = wheel.Key,
            WheelName = wheel.DisplayName,
            SurfaceTemperatureCelsius = wheel.SurfaceTemperatureCelsius,
            InnerTemperatureCelsius = wheel.InnerTemperatureCelsius,
            PressurePsi = wheel.PressurePsi,
            TrackTemperatureCelsius = currentState.TrackTemperature,
            thresholds.HighSurfaceCelsius,
            thresholds.HighInnerCelsius,
            thresholds.LowSurfaceCelsius,
            thresholds.LowInnerCelsius
        };
    }

    private static bool IsHighTyreTemperatureCandidate(
        TyreTemperatureWheelSample wheel,
        TyreTemperatureThresholds thresholds)
    {
        return wheel.SurfaceTemperatureCelsius >= thresholds.HighSurfaceCelsius &&
               wheel.InnerTemperatureCelsius >= thresholds.HighInnerCelsius;
    }

    private bool HasRecoveredFromHighTyreTemperature(
        TyreTemperatureWheelSample wheel,
        TyreTemperatureThresholds thresholds)
    {
        return wheel.SurfaceTemperatureCelsius <= thresholds.HighSurfaceCelsius - _options.TyreTemperatureRecoveryHysteresisCelsius &&
               wheel.InnerTemperatureCelsius <= thresholds.HighInnerCelsius - _options.TyreTemperatureRecoveryHysteresisCelsius;
    }

    private static float GetHighTyreTemperatureScore(
        TyreTemperatureWheelSample wheel,
        TyreTemperatureThresholds thresholds)
    {
        return (wheel.SurfaceTemperatureCelsius - thresholds.HighSurfaceCelsius) +
               (wheel.InnerTemperatureCelsius - thresholds.HighInnerCelsius);
    }

    private static bool IsLowTyreTemperatureCandidate(
        TyreTemperatureWheelSample wheel,
        TyreTemperatureThresholds thresholds)
    {
        return wheel.SurfaceTemperatureCelsius <= thresholds.LowSurfaceCelsius &&
               wheel.InnerTemperatureCelsius <= thresholds.LowInnerCelsius;
    }

    private bool HasRecoveredFromLowTyreTemperature(
        TyreTemperatureWheelSample wheel,
        TyreTemperatureThresholds thresholds)
    {
        return wheel.SurfaceTemperatureCelsius >= thresholds.LowSurfaceCelsius + _options.TyreTemperatureRecoveryHysteresisCelsius &&
               wheel.InnerTemperatureCelsius >= thresholds.LowInnerCelsius + _options.TyreTemperatureRecoveryHysteresisCelsius;
    }

    private static float GetLowTyreTemperatureScore(
        TyreTemperatureWheelSample wheel,
        TyreTemperatureThresholds thresholds)
    {
        return (thresholds.LowSurfaceCelsius - wheel.SurfaceTemperatureCelsius) +
               (thresholds.LowInnerCelsius - wheel.InnerTemperatureCelsius);
    }

    private void EmitFaultEvent(
        SessionState currentState,
        CarSnapshot currentPlayer,
        EventType eventType,
        string message,
        string faultKey,
        object payload)
    {
        EmitEvent(
            eventType: eventType,
            severity: EventSeverity.Warning,
            timestamp: currentState.UpdatedAt,
            lapNumber: (int?)currentPlayer.CurrentLapNumber,
            vehicleIdx: currentPlayer.CarIndex,
            driverName: currentPlayer.DriverName,
            message: message,
            dedupKey: $"damage:fault:{faultKey}",
            payload: payload);
    }

    private static EventSeverity MapDamageEventSeverity(DamageSeverity severity)
    {
        return severity >= DamageSeverity.Moderate ? EventSeverity.Warning : EventSeverity.Information;
    }

    private static string FormatDamageComponent(DamageComponent component)
    {
        return component switch
        {
            DamageComponent.TyreDamage => "轮胎",
            DamageComponent.BrakeDamage => "刹车",
            DamageComponent.TyreBlister => "轮胎起泡",
            DamageComponent.FrontLeftWing => "前翼左侧",
            DamageComponent.FrontRightWing => "前翼右侧",
            DamageComponent.RearWing => "尾翼",
            DamageComponent.Floor => "底板",
            DamageComponent.Diffuser => "扩散器",
            DamageComponent.Sidepod => "侧箱",
            DamageComponent.Gearbox => "变速箱",
            DamageComponent.Engine => "引擎",
            DamageComponent.EngineMguhWear => "MGU-H",
            DamageComponent.EngineEsWear => "电池",
            DamageComponent.EngineCeWear => "电控",
            DamageComponent.EngineIceWear => "内燃机",
            DamageComponent.EngineMgukWear => "MGU-K",
            DamageComponent.EngineTcWear => "涡轮",
            _ => component.ToString()
        };
    }

    private static bool IsDrivetrainWearComponent(DamageComponent component)
    {
        return component is DamageComponent.Gearbox
            or DamageComponent.Engine
            or DamageComponent.EngineMguhWear
            or DamageComponent.EngineEsWear
            or DamageComponent.EngineCeWear
            or DamageComponent.EngineIceWear
            or DamageComponent.EngineMgukWear
            or DamageComponent.EngineTcWear;
    }

    private static string FormatDamageSeverity(DamageSeverity severity)
    {
        return severity switch
        {
            DamageSeverity.Minor => "轻微",
            DamageSeverity.Light => "轻度",
            DamageSeverity.Moderate => "中度",
            DamageSeverity.Severe => "严重",
            DamageSeverity.Critical => "危急",
            _ => "无"
        };
    }

    private static bool IsKnownMarshalZoneFlag(sbyte zoneFlag)
    {
        return zoneFlag is >= -1 and <= 4;
    }

    private static bool IsActiveSafetyCarStatus(byte? safetyCarStatus)
    {
        return safetyCarStatus is 1 or 2;
    }

    private static bool IsRaceLikeSession(SessionState sessionState)
    {
        return SessionModeFormatter.Resolve(sessionState.SessionType) is SessionMode.Race or SessionMode.SprintRace;
    }

    private static bool IsQualifyingLikeSession(SessionState sessionState)
    {
        return SessionModeFormatter.Resolve(sessionState.SessionType) is SessionMode.Qualifying
            or SessionMode.SprintQualifying
            or SessionMode.TimeTrial;
    }

    private static CarSnapshot? FindDirectFrontCar(SessionState currentState, CarSnapshot player)
    {
        if (player.Position is not { } playerPosition || playerPosition <= 1)
        {
            return null;
        }

        var frontPosition = playerPosition - 1;
        return currentState.Cars.FirstOrDefault(car => !car.IsPlayer
            && car.Position.HasValue
            && car.Position.Value == frontPosition);
    }

    private static CarSnapshot? FindDirectRearCar(SessionState currentState, CarSnapshot player)
    {
        if (player.Position is not { } playerPosition)
        {
            return null;
        }

        var activeCarCount = currentState.ActiveCarCount.HasValue
            ? currentState.ActiveCarCount.Value
            : currentState.Cars.Count;
        if (activeCarCount > 0 && playerPosition >= activeCarCount)
        {
            return null;
        }

        var rearPosition = playerPosition + 1;
        return currentState.Cars.FirstOrDefault(car => !car.IsPlayer
            && car.Position.HasValue
            && car.Position.Value == rearPosition);
    }

    private static bool TryGetSameLapFrontGapMs(CarSnapshot player, CarSnapshot frontCar, out ushort gapFrontMs)
    {
        gapFrontMs = 0;
        if (!IsSameLap(player, frontCar) || player.DeltaToCarInFrontInMs is not { } currentGapFrontMs)
        {
            return false;
        }

        if (!IsUsableGapMs(currentGapFrontMs))
        {
            return false;
        }

        gapFrontMs = currentGapFrontMs;
        return true;
    }

    private static bool TryGetSameLapRearGapMs(CarSnapshot player, CarSnapshot rearCar, out ushort gapBehindMs)
    {
        gapBehindMs = 0;
        if (!IsSameLap(player, rearCar) || rearCar.DeltaToCarInFrontInMs is not { } currentGapBehindMs)
        {
            return false;
        }

        if (!IsUsableGapMs(currentGapBehindMs))
        {
            return false;
        }

        gapBehindMs = currentGapBehindMs;
        return true;
    }

    private static bool IsUsableGapMs(ushort gapMs)
    {
        return gapMs > 0;
    }

    private static bool IsSameLap(CarSnapshot player, CarSnapshot otherCar)
    {
        return player.CurrentLapNumber is { } playerLap
            && otherCar.CurrentLapNumber is { } otherLap
            && playerLap == otherLap;
    }

    private static string FormatTyreState(byte? tyreAgeLaps, float? tyreWear)
    {
        if (tyreAgeLaps.HasValue && tyreWear.HasValue)
        {
            return $"当前胎龄 {tyreAgeLaps.Value} 圈、平均磨损 {tyreWear.Value:0.0}%";
        }

        if (tyreAgeLaps.HasValue)
        {
            return $"当前胎龄 {tyreAgeLaps.Value} 圈";
        }

        if (tyreWear.HasValue)
        {
            return $"当前平均磨损 {tyreWear.Value:0.0}%";
        }

        return "当前轮胎状态未知";
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

    private sealed record TyreTemperatureWheelSample(
        string Key,
        string DisplayName,
        byte SurfaceTemperatureCelsius,
        byte InnerTemperatureCelsius,
        float PressurePsi);

    private sealed record TyreTemperatureThresholds(
        float TrackTemperatureCelsius,
        float HighSurfaceCelsius,
        float HighInnerCelsius,
        float LowSurfaceCelsius,
        float LowInnerCelsius);
}
