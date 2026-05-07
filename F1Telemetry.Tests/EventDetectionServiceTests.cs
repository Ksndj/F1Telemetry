using F1Telemetry.Analytics.Events;
using F1Telemetry.Core.Models;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies that race events are detected from aggregate session state changes.
/// </summary>
public sealed class EventDetectionServiceTests
{
    /// <summary>
    /// Verifies that a front-car pit sequence emits a front-car pit event.
    /// </summary>
    [Fact]
    public void Observe_FrontCarPitSignals_EmitsFrontCarPittedEvent()
    {
        var service = new EventDetectionService(new EventDetectionOptions());

        service.Observe(CreateState(
            CreatePlayerCar(carIndex: 3, position: 2, lapNumber: 10, fuelLapsRemaining: 6.0f, tyreWear: 35f, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19),
            CreateOpponent(carIndex: 2, driverName: "Front Runner", position: 1, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19),
            CreateOpponent(carIndex: 4, driverName: "Rear Runner", position: 3, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19)));

        service.Observe(CreateState(
            CreatePlayerCar(carIndex: 3, position: 2, lapNumber: 10, fuelLapsRemaining: 5.8f, tyreWear: 36f, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19),
            CreateOpponent(carIndex: 2, driverName: "Front Runner", position: 1, pitStatus: 1, numPitStops: 1, visualTyreCompound: 16, actualTyreCompound: 19),
            CreateOpponent(carIndex: 4, driverName: "Rear Runner", position: 3, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19)));

        var detectedEvents = service.DrainPendingEvents();
        var raceEvent = Assert.Single(detectedEvents);

        Assert.Equal(EventType.FrontCarPitted, raceEvent.EventType);
        Assert.Equal(10, raceEvent.LapNumber);
        Assert.Equal(2, raceEvent.VehicleIdx);
        Assert.Equal("Front Runner", raceEvent.DriverName);
    }

    /// <summary>
    /// Verifies that crossing the configured low-fuel threshold emits an event.
    /// </summary>
    [Fact]
    public void Observe_FuelDropsBelowThreshold_EmitsLowFuelEvent()
    {
        var service = new EventDetectionService(new EventDetectionOptions
        {
            LowFuelLapsThreshold = 3.0f
        });

        service.Observe(CreateState(
            CreatePlayerCar(carIndex: 3, position: 5, lapNumber: 12, fuelLapsRemaining: 3.2f, tyreWear: 40f, pitStatus: 0, numPitStops: 1, visualTyreCompound: 17, actualTyreCompound: 20)));
        service.Observe(CreateState(
            CreatePlayerCar(carIndex: 3, position: 5, lapNumber: 12, fuelLapsRemaining: 2.8f, tyreWear: 41f, pitStatus: 0, numPitStops: 1, visualTyreCompound: 17, actualTyreCompound: 20)));

        var raceEvent = Assert.Single(service.DrainPendingEvents());
        Assert.Equal(EventType.LowFuel, raceEvent.EventType);
        Assert.Equal(12, raceEvent.LapNumber);
        Assert.Equal(3, raceEvent.VehicleIdx);
    }

    /// <summary>
    /// Verifies that tyre wear crossing the configured threshold emits an event.
    /// </summary>
    [Fact]
    public void Observe_TyreWearCrossesThreshold_EmitsHighTyreWearEvent()
    {
        var service = new EventDetectionService(new EventDetectionOptions
        {
            HighTyreWearThreshold = 70f
        });

        service.Observe(CreateState(
            CreatePlayerCar(carIndex: 3, position: 6, lapNumber: 15, fuelLapsRemaining: 8.0f, tyreWear: 69f, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19)));
        service.Observe(CreateState(
            CreatePlayerCar(carIndex: 3, position: 6, lapNumber: 15, fuelLapsRemaining: 7.9f, tyreWear: 70f, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19)));

        var raceEvent = Assert.Single(service.DrainPendingEvents());
        Assert.Equal(EventType.HighTyreWear, raceEvent.EventType);
        Assert.Equal(15, raceEvent.LapNumber);
        Assert.Equal(3, raceEvent.VehicleIdx);
    }

    /// <summary>
    /// Verifies that crossing into a higher damage severity emits a player-car damage event.
    /// </summary>
    [Fact]
    public void Observe_PlayerDamageCrossesSeverity_EmitsCarDamageEvent()
    {
        var service = new EventDetectionService(new EventDetectionOptions());

        service.Observe(CreateState(
            CreatePlayerCar(carIndex: 3, position: 6, lapNumber: 15, fuelLapsRemaining: 8.0f, tyreWear: 35f, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19)
                with { Damage = CreateDamageSnapshot(frontLeftWingDamage: 0) }));
        service.Observe(CreateState(
            CreatePlayerCar(carIndex: 3, position: 6, lapNumber: 15, fuelLapsRemaining: 7.9f, tyreWear: 36f, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19)
                with { Damage = CreateDamageSnapshot(frontLeftWingDamage: 30) }));

        var raceEvent = Assert.Single(service.DrainPendingEvents());
        Assert.Equal(EventType.CarDamage, raceEvent.EventType);
        Assert.Equal(EventSeverity.Warning, raceEvent.Severity);
        Assert.Equal(15, raceEvent.LapNumber);
        Assert.Equal(3, raceEvent.VehicleIdx);
        Assert.Contains("前翼左侧", raceEvent.Message, StringComparison.Ordinal);
        Assert.Contains("中度", raceEvent.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that repeated damage observations within the same severity band do not emit duplicates.
    /// </summary>
    [Fact]
    public void Observe_RepeatedSameDamageSeverity_DeduplicatesEvent()
    {
        var service = new EventDetectionService(new EventDetectionOptions());

        service.Observe(CreateState(
            CreatePlayerCar(carIndex: 3, position: 6, lapNumber: 15, fuelLapsRemaining: 8.0f, tyreWear: 35f, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19)
                with { Damage = CreateDamageSnapshot(frontLeftWingDamage: 0) }));
        service.Observe(CreateState(
            CreatePlayerCar(carIndex: 3, position: 6, lapNumber: 15, fuelLapsRemaining: 7.9f, tyreWear: 36f, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19)
                with { Damage = CreateDamageSnapshot(frontLeftWingDamage: 30) }));
        service.Observe(CreateState(
            CreatePlayerCar(carIndex: 3, position: 6, lapNumber: 15, fuelLapsRemaining: 7.8f, tyreWear: 37f, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19)
                with { Damage = CreateDamageSnapshot(frontLeftWingDamage: 35) }));

        var detectedEvents = service.DrainPendingEvents();

        var raceEvent = Assert.Single(detectedEvents);
        Assert.Equal(EventType.CarDamage, raceEvent.EventType);
    }

    /// <summary>
    /// Verifies that DRS and ERS fault flags emit fault events once when they first appear.
    /// </summary>
    [Fact]
    public void Observe_DrsAndErsFaults_EmitFaultEventsOnce()
    {
        var service = new EventDetectionService(new EventDetectionOptions());

        service.Observe(CreateState(
            CreatePlayerCar(carIndex: 3, position: 6, lapNumber: 15, fuelLapsRemaining: 8.0f, tyreWear: 35f, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19)
                with { Damage = CreateDamageSnapshot(drsFault: false, ersFault: false) }));
        service.Observe(CreateState(
            CreatePlayerCar(carIndex: 3, position: 6, lapNumber: 15, fuelLapsRemaining: 7.9f, tyreWear: 36f, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19)
                with { Damage = CreateDamageSnapshot(drsFault: true, ersFault: true) }));
        service.Observe(CreateState(
            CreatePlayerCar(carIndex: 3, position: 6, lapNumber: 15, fuelLapsRemaining: 7.8f, tyreWear: 37f, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19)
                with { Damage = CreateDamageSnapshot(drsFault: true, ersFault: true) }));

        var detectedEvents = service.DrainPendingEvents();

        Assert.Contains(detectedEvents, raceEvent => raceEvent.EventType == EventType.DrsFault);
        Assert.Contains(detectedEvents, raceEvent => raceEvent.EventType == EventType.ErsFault);
        Assert.Equal(2, detectedEvents.Count);
    }

    /// <summary>
    /// Verifies power-unit wear does not produce an immediate spoken damage event.
    /// </summary>
    [Fact]
    public void Observe_PowerUnitWearCrossesSeverity_DoesNotEmitImmediateDamageEvent()
    {
        var service = new EventDetectionService(new EventDetectionOptions());

        service.Observe(CreateState(
            CreatePlayerCar(carIndex: 3, position: 6, lapNumber: 15, fuelLapsRemaining: 8.0f, tyreWear: 35f, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19)
                with { Damage = CreateDamageSnapshot(engineDamage: 0, gearboxDamage: 0) }));
        service.Observe(CreateState(
            CreatePlayerCar(carIndex: 3, position: 6, lapNumber: 15, fuelLapsRemaining: 7.9f, tyreWear: 36f, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19)
                with { Damage = CreateDamageSnapshot(engineDamage: 68, gearboxDamage: 36) }));

        Assert.Empty(service.DrainPendingEvents());
    }

    /// <summary>
    /// Verifies that repeated low-fuel observations on the same lap do not emit duplicates.
    /// </summary>
    [Fact]
    public void Observe_RepeatedLowFuelOnSameLap_DeduplicatesEvent()
    {
        var service = new EventDetectionService(new EventDetectionOptions
        {
            LowFuelLapsThreshold = 3.0f
        });

        service.Observe(CreateState(
            CreatePlayerCar(carIndex: 3, position: 4, lapNumber: 18, fuelLapsRemaining: 3.1f, tyreWear: 50f, pitStatus: 0, numPitStops: 1, visualTyreCompound: 17, actualTyreCompound: 20)));
        service.Observe(CreateState(
            CreatePlayerCar(carIndex: 3, position: 4, lapNumber: 18, fuelLapsRemaining: 2.9f, tyreWear: 51f, pitStatus: 0, numPitStops: 1, visualTyreCompound: 17, actualTyreCompound: 20)));
        service.Observe(CreateState(
            CreatePlayerCar(carIndex: 3, position: 4, lapNumber: 18, fuelLapsRemaining: 2.5f, tyreWear: 52f, pitStatus: 0, numPitStops: 1, visualTyreCompound: 17, actualTyreCompound: 20)));

        var detectedEvents = service.DrainPendingEvents();

        var raceEvent = Assert.Single(detectedEvents);
        Assert.Equal(EventType.LowFuel, raceEvent.EventType);
    }

    /// <summary>
    /// Verifies that resetting the detector clears pending state and allows the same threshold crossing in a new session.
    /// </summary>
    [Fact]
    public void Reset_ClearsPendingStateAndAllowsFreshDetection()
    {
        var service = new EventDetectionService(new EventDetectionOptions
        {
            LowFuelLapsThreshold = 3.0f
        });

        service.Observe(CreateState(
            CreatePlayerCar(carIndex: 3, position: 4, lapNumber: 1, fuelLapsRemaining: 3.1f, tyreWear: 50f, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19)));
        service.Observe(CreateState(
            CreatePlayerCar(carIndex: 3, position: 4, lapNumber: 1, fuelLapsRemaining: 2.9f, tyreWear: 51f, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19)));

        service.Reset();

        Assert.Empty(service.DrainPendingEvents());

        service.Observe(CreateState(
            CreatePlayerCar(carIndex: 3, position: 4, lapNumber: 1, fuelLapsRemaining: 3.2f, tyreWear: 52f, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19)));
        service.Observe(CreateState(
            CreatePlayerCar(carIndex: 3, position: 4, lapNumber: 1, fuelLapsRemaining: 2.8f, tyreWear: 53f, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19)));

        var raceEvent = Assert.Single(service.DrainPendingEvents());
        Assert.Equal(EventType.LowFuel, raceEvent.EventType);
        Assert.Equal(1, raceEvent.LapNumber);
    }

    /// <summary>
    /// Verifies safety-car status transitions emit only key status-change events.
    /// </summary>
    [Fact]
    public void Observe_SafetyCarStatusChanges_EmitsSafetyEventsOnce()
    {
        var service = new EventDetectionService(new EventDetectionOptions());

        service.Observe(CreateStateWithMetadata(
            sessionType: 15,
            safetyCarStatus: 0,
            marshalZoneFlags: null,
            activeCarCount: null,
            CreatePlayerCar(carIndex: 3, position: 4, lapNumber: 8, fuelLapsRemaining: 6.0f, tyreWear: 35f, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19)));
        service.Observe(CreateStateWithMetadata(
            sessionType: 15,
            safetyCarStatus: 1,
            marshalZoneFlags: null,
            activeCarCount: null,
            CreatePlayerCar(carIndex: 3, position: 4, lapNumber: 8, fuelLapsRemaining: 5.9f, tyreWear: 35f, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19)));
        service.Observe(CreateStateWithMetadata(
            sessionType: 15,
            safetyCarStatus: 1,
            marshalZoneFlags: null,
            activeCarCount: null,
            CreatePlayerCar(carIndex: 3, position: 4, lapNumber: 8, fuelLapsRemaining: 5.8f, tyreWear: 36f, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19)));

        var detectedEvents = service.DrainPendingEvents();

        var raceEvent = Assert.Single(detectedEvents);
        Assert.Equal(EventType.SafetyCar, raceEvent.EventType);
        Assert.Equal("安全车出动，保持 delta 并注意前车。", raceEvent.Message);
        Assert.DoesNotContain("Safety car", raceEvent.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies marshal-zone flags emit only confirmed flags and keep unknown values as data quality warnings.
    /// </summary>
    [Fact]
    public void Observe_MarshalZoneFlags_EmitsKnownFlagsAndWarnsForUnknownValues()
    {
        var service = new EventDetectionService(new EventDetectionOptions());
        var player = CreatePlayerCar(carIndex: 3, position: 4, lapNumber: 8, fuelLapsRemaining: 6.0f, tyreWear: 35f, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19);

        service.Observe(CreateStateWithMetadata(15, 0, new Dictionary<int, sbyte> { [0] = 0 }, null, player));
        service.Observe(CreateStateWithMetadata(15, 0, new Dictionary<int, sbyte> { [0] = 3, [1] = 9 }, null, player));
        service.Observe(CreateStateWithMetadata(15, 0, new Dictionary<int, sbyte> { [0] = 3, [1] = 9 }, null, player));

        var detectedEvents = service.DrainPendingEvents();

        var yellowFlag = Assert.Single(detectedEvents, raceEvent => raceEvent.EventType == EventType.YellowFlag);
        Assert.Contains("黄旗", yellowFlag.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Yellow flag", yellowFlag.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(detectedEvents, raceEvent => raceEvent.EventType == EventType.DataQualityWarning);
        Assert.Equal(2, detectedEvents.Count);
    }

    /// <summary>
    /// Verifies attack and defense windows are emitted on window entry, not every observation tick.
    /// </summary>
    [Fact]
    public void Observe_GapWindows_EmitOnEntryAndRearmAfterRecovery()
    {
        var service = new EventDetectionService(new EventDetectionOptions
        {
            RaceWindowCooldownSeconds = 1
        });
        var timestamp = DateTimeOffset.UtcNow;

        service.Observe(CreateStateWithMetadata(
            sessionType: 15,
            safetyCarStatus: 0,
            marshalZoneFlags: null,
            activeCarCount: 3,
            CreatePlayerCar(carIndex: 3, position: 2, lapNumber: 8, fuelLapsRemaining: 6.0f, tyreWear: 35f, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19, gapToFrontMs: 1_600, ersStoreEnergy: 2_000_000f, updatedAt: timestamp),
            CreateOpponent(carIndex: 4, driverName: "Rear Runner", position: 3, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19, lapNumber: 8, gapToFrontMs: 1_600, updatedAt: timestamp)));
        service.Observe(CreateStateWithMetadata(
            sessionType: 15,
            safetyCarStatus: 0,
            marshalZoneFlags: null,
            activeCarCount: 3,
            CreatePlayerCar(carIndex: 3, position: 2, lapNumber: 8, fuelLapsRemaining: 5.9f, tyreWear: 36f, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19, gapToFrontMs: 900, ersStoreEnergy: 2_000_000f, updatedAt: timestamp.AddMilliseconds(100)),
            CreateOpponent(carIndex: 4, driverName: "Rear Runner", position: 3, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19, lapNumber: 8, gapToFrontMs: 900, updatedAt: timestamp.AddMilliseconds(100))));
        service.Observe(CreateStateWithMetadata(
            sessionType: 15,
            safetyCarStatus: 0,
            marshalZoneFlags: null,
            activeCarCount: 3,
            CreatePlayerCar(carIndex: 3, position: 2, lapNumber: 8, fuelLapsRemaining: 5.8f, tyreWear: 37f, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19, gapToFrontMs: 800, ersStoreEnergy: 2_000_000f, updatedAt: timestamp.AddMilliseconds(200)),
            CreateOpponent(carIndex: 4, driverName: "Rear Runner", position: 3, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19, lapNumber: 8, gapToFrontMs: 800, updatedAt: timestamp.AddMilliseconds(200))));

        var firstWindowEvents = service.DrainPendingEvents();
        Assert.Equal(2, firstWindowEvents.Count(raceEvent => raceEvent.EventType is EventType.AttackWindow or EventType.DefenseWindow));
        Assert.Contains(firstWindowEvents, raceEvent => raceEvent.EventType == EventType.AttackWindow && raceEvent.Message.Contains("攻击窗口", StringComparison.Ordinal));
        Assert.Contains(firstWindowEvents, raceEvent => raceEvent.EventType == EventType.DefenseWindow && raceEvent.Message.Contains("注意防守", StringComparison.Ordinal));
        Assert.DoesNotContain(firstWindowEvents, raceEvent => raceEvent.Message.Contains("Attack window", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(firstWindowEvents, raceEvent => raceEvent.Message.Contains("Defense window", StringComparison.OrdinalIgnoreCase));

        service.Observe(CreateStateWithMetadata(
            sessionType: 15,
            safetyCarStatus: 0,
            marshalZoneFlags: null,
            activeCarCount: 3,
            CreatePlayerCar(carIndex: 3, position: 2, lapNumber: 8, fuelLapsRemaining: 5.7f, tyreWear: 38f, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19, gapToFrontMs: 1_700, ersStoreEnergy: 2_000_000f, updatedAt: timestamp.AddSeconds(2)),
            CreateOpponent(carIndex: 4, driverName: "Rear Runner", position: 3, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19, lapNumber: 8, gapToFrontMs: 1_700, updatedAt: timestamp.AddSeconds(2))));
        service.Observe(CreateStateWithMetadata(
            sessionType: 15,
            safetyCarStatus: 0,
            marshalZoneFlags: null,
            activeCarCount: 3,
            CreatePlayerCar(carIndex: 3, position: 2, lapNumber: 8, fuelLapsRemaining: 5.6f, tyreWear: 39f, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19, gapToFrontMs: 900, ersStoreEnergy: 2_000_000f, updatedAt: timestamp.AddSeconds(3)),
            CreateOpponent(carIndex: 4, driverName: "Rear Runner", position: 3, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19, lapNumber: 8, gapToFrontMs: 900, updatedAt: timestamp.AddSeconds(3))));

        var rearmedEvents = service.DrainPendingEvents();
        Assert.Equal(2, rearmedEvents.Count(raceEvent => raceEvent.EventType is EventType.AttackWindow or EventType.DefenseWindow));
    }

    /// <summary>
    /// Verifies low-ERS risk speech uses a Chinese broadcast message.
    /// </summary>
    [Fact]
    public void Observe_LowErs_EmitsChineseBroadcastText()
    {
        var service = new EventDetectionService(new EventDetectionOptions
        {
            LowErsStoreEnergyThresholdJoules = 1_000_000f
        });

        service.Observe(CreateStateWithMetadata(
            sessionType: 15,
            safetyCarStatus: 0,
            marshalZoneFlags: null,
            activeCarCount: 1,
            CreatePlayerCar(carIndex: 3, position: 1, lapNumber: 8, fuelLapsRemaining: 6.0f, tyreWear: 35f, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19, gapToFrontMs: null, ersStoreEnergy: 1_500_000f)));
        service.Observe(CreateStateWithMetadata(
            sessionType: 15,
            safetyCarStatus: 0,
            marshalZoneFlags: null,
            activeCarCount: 1,
            CreatePlayerCar(carIndex: 3, position: 1, lapNumber: 8, fuelLapsRemaining: 5.9f, tyreWear: 36f, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19, gapToFrontMs: null, ersStoreEnergy: 500_000f)));

        var raceEvent = Assert.Single(service.DrainPendingEvents());
        Assert.Equal(EventType.LowErs, raceEvent.EventType);
        Assert.Contains("ERS 剩余 500000 焦耳", raceEvent.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Low ERS", raceEvent.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies race strategy windows are not emitted for qualifying sessions.
    /// </summary>
    [Fact]
    public void Observe_QualifyingSession_DoesNotEmitRaceStrategyWindowEvents()
    {
        var service = new EventDetectionService(new EventDetectionOptions());

        service.Observe(CreateStateWithMetadata(
            sessionType: 5,
            safetyCarStatus: 0,
            marshalZoneFlags: null,
            activeCarCount: 3,
            CreatePlayerCar(carIndex: 3, position: 2, lapNumber: 8, fuelLapsRemaining: 6.0f, tyreWear: 35f, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19, gapToFrontMs: 900, ersStoreEnergy: 500_000f),
            CreateOpponent(carIndex: 2, driverName: "Front Runner", position: 1, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19, lapNumber: 8),
            CreateOpponent(carIndex: 4, driverName: "Rear Runner", position: 3, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19, lapNumber: 8, gapToFrontMs: 900)));
        service.Observe(CreateStateWithMetadata(
            sessionType: 5,
            safetyCarStatus: 0,
            marshalZoneFlags: null,
            activeCarCount: 3,
            CreatePlayerCar(carIndex: 3, position: 2, lapNumber: 8, fuelLapsRemaining: 5.9f, tyreWear: 36f, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19, gapToFrontMs: 850, ersStoreEnergy: 400_000f),
            CreateOpponent(carIndex: 2, driverName: "Front Runner", position: 1, pitStatus: 1, numPitStops: 1, visualTyreCompound: 16, actualTyreCompound: 19, lapNumber: 8),
            CreateOpponent(carIndex: 4, driverName: "Rear Runner", position: 3, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19, lapNumber: 8, gapToFrontMs: 850)));

        var detectedEvents = service.DrainPendingEvents();

        Assert.DoesNotContain(detectedEvents, raceEvent => raceEvent.EventType is EventType.FrontCarPitted or EventType.AttackWindow or EventType.DefenseWindow or EventType.LowErs);
    }

    /// <summary>
    /// Verifies missing race trend data creates one data-quality warning per evidence domain.
    /// </summary>
    [Fact]
    public void Observe_MissingRaceTrendData_EmitsDeduplicatedDataQualityWarnings()
    {
        var service = new EventDetectionService(new EventDetectionOptions());

        service.Observe(CreateState(
            CreatePlayerCar(carIndex: 3, position: 2, lapNumber: 8, fuelLapsRemaining: null, tyreWear: null, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19, gapToFrontMs: null, ersStoreEnergy: null),
            CreateOpponent(carIndex: 4, driverName: "Rear Runner", position: 3, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19, lapNumber: 8, gapToFrontMs: null)));
        service.Observe(CreateState(
            CreatePlayerCar(carIndex: 3, position: 2, lapNumber: 8, fuelLapsRemaining: null, tyreWear: null, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19, gapToFrontMs: null, ersStoreEnergy: null),
            CreateOpponent(carIndex: 4, driverName: "Rear Runner", position: 3, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19, lapNumber: 8, gapToFrontMs: null)));
        service.DrainPendingEvents();

        service.Observe(CreateState(
            CreatePlayerCar(carIndex: 3, position: 2, lapNumber: 8, fuelLapsRemaining: null, tyreWear: null, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19, gapToFrontMs: null, ersStoreEnergy: null),
            CreateOpponent(carIndex: 4, driverName: "Rear Runner", position: 3, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19, lapNumber: 8, gapToFrontMs: null)));

        Assert.Empty(service.DrainPendingEvents());
    }

    private static SessionState CreateState(params CarSnapshot[] cars)
    {
        return CreateStateWithMetadata(
            sessionType: 15,
            safetyCarStatus: 0,
            marshalZoneFlags: null,
            activeCarCount: null,
            cars);
    }

    private static SessionState CreateStateWithMetadata(
        byte sessionType,
        byte? safetyCarStatus,
        IReadOnlyDictionary<int, sbyte>? marshalZoneFlags,
        byte? activeCarCount,
        params CarSnapshot[] cars)
    {
        var playerCar = cars.Single(car => car.IsPlayer);
        var orderedCars = cars.OrderBy(car => car.Position ?? byte.MaxValue).ToArray();

        return new SessionState
        {
            PlayerCarIndex = (byte)playerCar.CarIndex,
            SessionType = sessionType,
            SafetyCarStatus = safetyCarStatus,
            MarshalZoneFlags = marshalZoneFlags ?? new Dictionary<int, sbyte>(),
            ActiveCarCount = activeCarCount ?? (byte)orderedCars.Length,
            PlayerCar = playerCar,
            Cars = orderedCars,
            Opponents = orderedCars.Where(car => !car.IsPlayer).ToArray(),
            UpdatedAt = playerCar.UpdatedAt
        };
    }

    private static CarSnapshot CreatePlayerCar(
        int carIndex,
        byte position,
        byte lapNumber,
        float? fuelLapsRemaining,
        float? tyreWear,
        byte pitStatus,
        byte numPitStops,
        byte visualTyreCompound,
        byte actualTyreCompound,
        ushort? gapToFrontMs = 2_000,
        float? ersStoreEnergy = 2_000_000f,
        DateTimeOffset? updatedAt = null)
    {
        return CreateCar(
            carIndex,
            driverName: "Player",
            isPlayer: true,
            position: position,
            lapNumber: lapNumber,
            fuelLapsRemaining: fuelLapsRemaining,
            tyreWear: tyreWear,
            pitStatus: pitStatus,
            numPitStops: numPitStops,
            visualTyreCompound: visualTyreCompound,
            actualTyreCompound: actualTyreCompound,
            gapToFrontMs: gapToFrontMs,
            ersStoreEnergy: ersStoreEnergy,
            updatedAt: updatedAt);
    }

    private static CarSnapshot CreateOpponent(
        int carIndex,
        string driverName,
        byte position,
        byte pitStatus,
        byte numPitStops,
        byte visualTyreCompound,
        byte actualTyreCompound,
        byte lapNumber = 10,
        ushort? gapToFrontMs = 2_000,
        DateTimeOffset? updatedAt = null)
    {
        return CreateCar(
            carIndex,
            driverName: driverName,
            isPlayer: false,
            position: position,
            lapNumber: lapNumber,
            fuelLapsRemaining: 9.5f,
            tyreWear: 42f,
            pitStatus: pitStatus,
            numPitStops: numPitStops,
            visualTyreCompound: visualTyreCompound,
            actualTyreCompound: actualTyreCompound,
            gapToFrontMs: gapToFrontMs,
            ersStoreEnergy: 2_000_000f,
            updatedAt: updatedAt);
    }

    private static CarSnapshot CreateCar(
        int carIndex,
        string driverName,
        bool isPlayer,
        byte position,
        byte lapNumber,
        float? fuelLapsRemaining,
        float? tyreWear,
        byte pitStatus,
        byte numPitStops,
        byte visualTyreCompound,
        byte actualTyreCompound,
        ushort? gapToFrontMs,
        float? ersStoreEnergy,
        DateTimeOffset? updatedAt)
    {
        return new CarSnapshot
        {
            CarIndex = carIndex,
            DriverName = driverName,
            IsPlayer = isPlayer,
            Position = position,
            CurrentLapNumber = lapNumber,
            FuelRemainingLaps = fuelLapsRemaining,
            TyreWear = tyreWear,
            PitStatus = pitStatus,
            NumPitStops = numPitStops,
            DeltaToCarInFrontInMs = gapToFrontMs,
            ErsStoreEnergy = ersStoreEnergy,
            VisualTyreCompound = visualTyreCompound,
            ActualTyreCompound = actualTyreCompound,
            IsCurrentLapValid = true,
            UpdatedAt = updatedAt ?? DateTimeOffset.UtcNow
        };
    }

    private static DamageSnapshot CreateDamageSnapshot(
        byte frontLeftWingDamage = 0,
        byte engineDamage = 0,
        byte gearboxDamage = 0,
        bool drsFault = false,
        bool ersFault = false)
    {
        return new DamageSnapshot
        {
            Components = new Dictionary<DamageComponent, byte>
            {
                [DamageComponent.FrontLeftWing] = frontLeftWingDamage,
                [DamageComponent.Engine] = engineDamage,
                [DamageComponent.Gearbox] = gearboxDamage
            },
            DrsFault = drsFault,
            ErsFault = ersFault
        };
    }
}
