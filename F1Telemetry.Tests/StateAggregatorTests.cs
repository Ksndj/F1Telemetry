using System.Net;
using F1Telemetry.Analytics.Services;
using F1Telemetry.Core.Models;
using F1Telemetry.Udp.Packets;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies that parsed UDP packets are projected into the central session state.
/// </summary>
public sealed class StateAggregatorTests
{
    /// <summary>
    /// Verifies that session, participant, lap and telemetry packets populate the player car state.
    /// </summary>
    [Fact]
    public void ApplyPacket_AggregatesSessionAndPlayerCarState()
    {
        var aggregator = new StateAggregator();

        aggregator.ApplyPacket(CreateParsedPacket(CreateSessionPacket(), playerCarIndex: 3));
        aggregator.ApplyPacket(CreateParsedPacket(
            new ParticipantsPacket(22, BuildParticipants(playerIndex: 3, restrictedOpponentIndex: 4)),
            playerCarIndex: 3));
        aggregator.ApplyPacket(CreateParsedPacket(
            new LapDataPacket(BuildLapDataCars(), TimeTrialPersonalBestCarIndex: 255, TimeTrialRivalCarIndex: 255),
            playerCarIndex: 3));
        aggregator.ApplyPacket(CreateParsedPacket(
            new SessionHistoryPacket(
                CarIndex: 3,
                NumLaps: 3,
                NumTyreStints: 0,
                BestLapTimeLapNumber: 2,
                BestSector1LapNumber: 0,
                BestSector2LapNumber: 0,
                BestSector3LapNumber: 0,
                LapHistory: BuildSessionHistory(),
                TyreStints: Array.Empty<TyreStintHistoryData>()),
            playerCarIndex: 3));
        aggregator.ApplyPacket(CreateParsedPacket(
            new CarTelemetryPacket(
                BuildTelemetryCars(),
                MfdPanelIndex: 255,
                MfdPanelIndexSecondaryPlayer: 255,
                SuggestedGear: 0),
            playerCarIndex: 3));

        var state = aggregator.SessionStateStore.CaptureState();

        Assert.Equal((byte)3, state.PlayerCarIndex);
        Assert.Equal((sbyte)10, state.TrackId);
        Assert.Equal((byte)12, state.SessionType);
        Assert.Equal((byte)2, state.Weather);
        Assert.Equal((sbyte)31, state.TrackTemperature);
        Assert.Equal((byte)22, state.ActiveCarCount);
        Assert.NotNull(state.PlayerCar);
        Assert.Equal(3, state.PlayerCar!.CarIndex);
        Assert.True(state.PlayerCar.IsPlayer);
        Assert.Equal("Driver 3", state.PlayerCar.DriverName);
        Assert.Equal((byte)4, state.PlayerCar.Position);
        Assert.Equal((byte)0, state.PlayerCar.PitStatus);
        Assert.Equal((uint)88000, state.PlayerCar.BestLapTimeInMs);
        Assert.NotNull(state.PlayerCar.Telemetry);
        Assert.Equal(203d, state.PlayerCar.Telemetry!.SpeedKph, 3);
        Assert.Equal((sbyte)6, state.PlayerCar.Gear);
        Assert.NotNull(state.PlayerCar.TyreCondition);
        Assert.Equal((byte)90, state.PlayerCar.TyreCondition!.SurfaceTemperatureCelsius.RearLeft);
        Assert.Equal((byte)80, state.PlayerCar.TyreCondition.InnerTemperatureCelsius.FrontRight);
        Assert.Equal(22f, state.PlayerCar.TyreCondition.PressurePsi.RearRight, 3);
    }

    /// <summary>
    /// Verifies that telemetry-restricted opponents keep identity fields but hide sensitive telemetry fields.
    /// </summary>
    [Fact]
    public void ApplyPacket_ClearsSensitiveFieldsForRestrictedOpponents()
    {
        var aggregator = new StateAggregator();

        aggregator.ApplyPacket(CreateParsedPacket(
            new ParticipantsPacket(22, BuildParticipants(playerIndex: 3, restrictedOpponentIndex: 4)),
            playerCarIndex: 3));
        aggregator.ApplyPacket(CreateParsedPacket(
            new CarTelemetryPacket(
                BuildTelemetryCars(),
                MfdPanelIndex: 255,
                MfdPanelIndexSecondaryPlayer: 255,
                SuggestedGear: 0),
            playerCarIndex: 3));
        aggregator.ApplyPacket(CreateParsedPacket(
            new CarStatusPacket(BuildStatusCars()),
            playerCarIndex: 3));

        var state = aggregator.SessionStateStore.CaptureState();
        var restrictedOpponent = Assert.Single(state.Opponents, car => car.CarIndex == 4);
        var visibleOpponent = Assert.Single(state.Opponents, car => car.CarIndex == 5);

        Assert.True(restrictedOpponent.IsTelemetryRestricted);
        Assert.Null(restrictedOpponent.Telemetry);
        Assert.Null(restrictedOpponent.TyreCondition);
        Assert.Null(restrictedOpponent.FuelInTank);
        Assert.Null(restrictedOpponent.ActualTyreCompound);

        Assert.False(visibleOpponent.IsTelemetryRestricted);
        Assert.NotNull(visibleOpponent.Telemetry);
        Assert.NotNull(visibleOpponent.TyreCondition);
        Assert.Equal(205d, visibleOpponent.Telemetry!.SpeedKph, 3);
        Assert.NotNull(visibleOpponent.FuelInTank);
        Assert.Equal(20.5f, visibleOpponent.FuelInTank!.Value, 3);
        Assert.NotNull(visibleOpponent.ErsStoreEnergy);
        Assert.Equal(1f, visibleOpponent.ErsStoreEnergy!.Value, 3);
    }

    /// <summary>
    /// Verifies that CarDamage packets create a detailed damage snapshot for the player car only.
    /// </summary>
    [Fact]
    public void ApplyPacket_CarDamageStoresPlayerDamageSnapshotOnly()
    {
        var aggregator = new StateAggregator();

        aggregator.ApplyPacket(CreateParsedPacket(
            new ParticipantsPacket(22, BuildParticipants(playerIndex: 3, restrictedOpponentIndex: 4)),
            playerCarIndex: 3));
        aggregator.ApplyPacket(CreateParsedPacket(
            new CarDamagePacket(BuildDamageCars(playerIndex: 3, visibleOpponentIndex: 5)),
            playerCarIndex: 3));

        var state = aggregator.SessionStateStore.CaptureState();
        var player = Assert.IsType<CarSnapshot>(state.PlayerCar);
        var visibleOpponent = Assert.Single(state.Opponents, car => car.CarIndex == 5);

        Assert.NotNull(player.Damage);
        Assert.Equal(30, player.Damage!.GetDamage(DamageComponent.FrontLeftWing));
        Assert.Equal(DamageSeverity.Moderate, player.Damage.GetSeverity(DamageComponent.FrontLeftWing));
        Assert.Equal(12, player.Damage.GetDamage(DamageComponent.BrakeDamage));
        Assert.Equal(82, player.Damage.GetDamage(DamageComponent.EngineIceWear));
        Assert.True(player.Damage.DrsFault);
        Assert.True(player.Damage.ErsFault);
        Assert.False(player.Damage.EngineBlown);

        Assert.Null(visibleOpponent.Damage);
        Assert.Equal(30f, player.TyreWear);
        Assert.Equal((byte)30, player.FrontLeftWingDamage);
    }

    /// <summary>
    /// Verifies that resetting the session store clears metadata and cached car snapshots between sessions.
    /// </summary>
    [Fact]
    public void SessionStateStore_Reset_ClearsMetadataAndCars()
    {
        var aggregator = new StateAggregator();

        aggregator.ApplyPacket(CreateParsedPacket(CreateSessionPacket(), playerCarIndex: 3));
        aggregator.ApplyPacket(CreateParsedPacket(
            new ParticipantsPacket(22, BuildParticipants(playerIndex: 3, restrictedOpponentIndex: 4)),
            playerCarIndex: 3));
        aggregator.ApplyPacket(CreateParsedPacket(
            new LapDataPacket(BuildLapDataCars(), TimeTrialPersonalBestCarIndex: 255, TimeTrialRivalCarIndex: 255),
            playerCarIndex: 3));

        aggregator.SessionStateStore.Reset();

        var state = aggregator.SessionStateStore.CaptureState();
        Assert.Null(state.PlayerCarIndex);
        Assert.Null(state.TrackId);
        Assert.Null(state.ActiveCarCount);
        Assert.Null(state.PlayerCar);
        Assert.Empty(state.Cars);
        Assert.Empty(state.Opponents);
    }

    /// <summary>
    /// Verifies that final classification marks the session as completed for post-race AI gating.
    /// </summary>
    [Fact]
    public void ApplyPacket_FinalClassification_MarksSessionComplete()
    {
        var aggregator = new StateAggregator();

        aggregator.ApplyPacket(CreateParsedPacket(CreateSessionPacket(), playerCarIndex: 3));
        aggregator.ApplyPacket(CreateParsedPacket(CreateFinalClassificationPacket(playerIndex: 3), playerCarIndex: 3));

        var state = aggregator.SessionStateStore.CaptureState();

        Assert.True(state.HasFinalClassification);
        Assert.NotNull(state.FinalClassificationReceivedAt);
        Assert.Equal((byte)4, state.PlayerFinalClassificationPosition);
        Assert.Equal((byte)29, state.PlayerFinalClassificationLaps);
        Assert.Equal((byte)3, state.PlayerFinalClassificationStatus);
        Assert.Equal((uint)1, state.SeasonLinkIdentifier);
        Assert.Equal((uint)1, state.WeekendLinkIdentifier);
        Assert.Equal((uint)1, state.SessionLinkIdentifier);
    }

    private static ParsedPacket CreateParsedPacket(IUdpPacket packet, byte playerCarIndex)
    {
        var header = new PacketHeader(
            PacketFormat: 2025,
            GameYear: 25,
            GameMajorVersion: 1,
            GameMinorVersion: 0,
            PacketVersion: 1,
            RawPacketId: GetPacketId(packet),
            SessionUid: 1,
            SessionTime: 1,
            FrameIdentifier: 1,
            OverallFrameIdentifier: 1,
            PlayerCarIndex: playerCarIndex,
            SecondaryPlayerCarIndex: 255);

        var datagram = new UdpDatagram(Array.Empty<byte>(), new IPEndPoint(IPAddress.Loopback, 20777), DateTimeOffset.UtcNow);
        return new ParsedPacket((PacketId)header.RawPacketId, header, packet, datagram);
    }

    private static byte GetPacketId(IUdpPacket packet)
    {
        return packet switch
        {
            SessionPacket => (byte)PacketId.Session,
            ParticipantsPacket => (byte)PacketId.Participants,
            LapDataPacket => (byte)PacketId.LapData,
            SessionHistoryPacket => (byte)PacketId.SessionHistory,
            CarTelemetryPacket => (byte)PacketId.CarTelemetry,
            CarStatusPacket => (byte)PacketId.CarStatus,
            CarDamagePacket => (byte)PacketId.CarDamage,
            FinalClassificationPacket => (byte)PacketId.FinalClassification,
            _ => throw new ArgumentOutOfRangeException(nameof(packet))
        };
    }

    private static FinalClassificationPacket CreateFinalClassificationPacket(int playerIndex)
    {
        var cars = new FinalClassificationData[22];
        for (var index = 0; index < cars.Length; index++)
        {
            cars[index] = new FinalClassificationData(
                Position: (byte)(index + 1),
                NumLaps: 29,
                GridPosition: (byte)(index + 1),
                Points: 0,
                NumPitStops: 1,
                ResultStatus: index == playerIndex ? (byte)3 : (byte)0,
                ResultReason: 0,
                BestLapTimeInMs: 90_000,
                TotalRaceTime: 5400d,
                PenaltiesTime: 0,
                NumPenalties: 0,
                NumTyreStints: 1,
                TyreStintsActual: new byte[8],
                TyreStintsVisual: new byte[8],
                TyreStintsEndLaps: new byte[8]);
        }

        return new FinalClassificationPacket((byte)cars.Length, cars);
    }

    private static SessionPacket CreateSessionPacket()
    {
        return new SessionPacket(
            Weather: 2,
            TrackTemperature: 31,
            AirTemperature: 24,
            TotalLaps: 29,
            TrackLength: 5400,
            SessionType: 12,
            TrackId: 10,
            Formula: 0,
            SessionTimeLeft: 1800,
            SessionDuration: 3600,
            PitSpeedLimit: 80,
            GamePaused: false,
            IsSpectating: false,
            SpectatorCarIndex: 0,
            SliProNativeSupport: false,
            NumMarshalZones: 0,
            MarshalZones: Array.Empty<MarshalZoneData>(),
            SafetyCarStatus: 0,
            NetworkGame: false,
            NumWeatherForecastSamples: 0,
            WeatherForecastSamples: Array.Empty<WeatherForecastSampleData>(),
            ForecastAccuracy: 0,
            AiDifficulty: 80,
            SeasonLinkIdentifier: 1,
            WeekendLinkIdentifier: 1,
            SessionLinkIdentifier: 1,
            PitStopWindowIdealLap: 0,
            PitStopWindowLatestLap: 0,
            PitStopRejoinPosition: 0,
            SteeringAssist: false,
            BrakingAssist: 0,
            GearboxAssist: 0,
            PitAssist: false,
            PitReleaseAssist: false,
            ErsAssist: false,
            DrsAssist: false,
            DynamicRacingLine: 0,
            DynamicRacingLineType: 0,
            GameMode: 0,
            RuleSet: 0,
            TimeOfDay: 0,
            SessionLength: 0,
            SpeedUnitsLeadPlayer: 0,
            TemperatureUnitsLeadPlayer: 0,
            SpeedUnitsSecondaryPlayer: 0,
            TemperatureUnitsSecondaryPlayer: 0,
            NumSafetyCarPeriods: 0,
            NumVirtualSafetyCarPeriods: 0,
            NumRedFlagPeriods: 0,
            EqualCarPerformance: false,
            RecoveryMode: 0,
            FlashbackLimit: 0,
            SurfaceType: 0,
            LowFuelMode: false,
            RaceStarts: false,
            TyreTemperature: false,
            PitLaneTyreSim: false,
            CarDamage: 0,
            CarDamageRate: 0,
            Collisions: 0,
            CollisionsOffForFirstLapOnly: false,
            MpUnsafePitRelease: false,
            MpOffForGriefing: false,
            CornerCuttingStringency: 0,
            ParcFermeRules: false,
            PitStopExperience: 0,
            SafetyCar: 0,
            SafetyCarExperience: 0,
            FormationLap: false,
            FormationLapExperience: false,
            RedFlags: 0,
            AffectsLicenceLevelSolo: false,
            AffectsLicenceLevelMp: false,
            NumSessionsInWeekend: 0,
            WeekendStructure: Array.Empty<byte>(),
            Sector2LapDistanceStart: 0f,
            Sector3LapDistanceStart: 0f);
    }

    private static ParticipantData[] BuildParticipants(int playerIndex, int restrictedOpponentIndex)
    {
        var participants = new ParticipantData[22];

        for (var index = 0; index < participants.Length; index++)
        {
            participants[index] = new ParticipantData(
                IsAiControlled: index != playerIndex,
                DriverId: (byte)index,
                NetworkId: (byte)(50 + index),
                TeamId: (byte)(20 + index),
                IsMyTeam: false,
                RaceNumber: (byte)(index + 1),
                Nationality: (byte)(30 + index),
                Name: $"Driver {index}",
                YourTelemetry: index != restrictedOpponentIndex,
                ShowOnlineNames: true,
                TechLevel: 0,
                Platform: 0,
                NumColours: 0,
                LiveryColours: Array.Empty<LiveryColourData>());
        }

        return participants;
    }

    private static LapDataEntry[] BuildLapDataCars()
    {
        var cars = new LapDataEntry[22];

        for (var index = 0; index < cars.Length; index++)
        {
            cars[index] = new LapDataEntry(
                LastLapTimeInMs: (uint)(90000 + index),
                CurrentLapTimeInMs: (uint)(45000 + index),
                Sector1TimeInMs: 15000,
                Sector1TimeMinutes: 0,
                Sector2TimeInMs: 15000,
                Sector2TimeMinutes: 0,
                DeltaToCarInFrontInMs: (ushort)(100 + index),
                DeltaToCarInFrontMinutes: 0,
                DeltaToRaceLeaderInMs: (ushort)(200 + index),
                DeltaToRaceLeaderMinutes: 0,
                LapDistance: 1200 + index,
                TotalDistance: 8000 + index,
                SafetyCarDelta: 0f,
                CarPosition: (byte)(index + 1),
                CurrentLapNumber: (byte)(10 + index),
                PitStatus: 0,
                NumPitStops: 0,
                Sector: 1,
                IsCurrentLapInvalid: false,
                Penalties: 0,
                TotalWarnings: 0,
                CornerCuttingWarnings: 0,
                NumUnservedDriveThroughPenalties: 0,
                NumUnservedStopGoPenalties: 0,
                GridPosition: (byte)(index + 1),
                DriverStatus: 0,
                ResultStatus: 0,
                IsPitLaneTimerActive: false,
                PitLaneTimeInLaneInMs: 0,
                PitStopTimerInMs: 0,
                ShouldServePitStopPenalty: false,
                SpeedTrapFastestSpeed: 0f,
                SpeedTrapFastestLap: 0);
        }

        return cars;
    }

    private static LapHistoryData[] BuildSessionHistory()
    {
        return
        [
            new LapHistoryData(90000, 0, 0, 0, 0, 0, 0, 1),
            new LapHistoryData(88000, 0, 0, 0, 0, 0, 0, 1),
            new LapHistoryData(91000, 0, 0, 0, 0, 0, 0, 1)
        ];
    }

    private static CarTelemetryData[] BuildTelemetryCars()
    {
        var cars = new CarTelemetryData[22];

        for (var index = 0; index < cars.Length; index++)
        {
            cars[index] = new CarTelemetryData(
                Speed: (ushort)(200 + index),
                Throttle: 0.5f,
                Steer: 0.1f,
                Brake: 0.2f,
                Clutch: 0,
                Gear: (sbyte)(3 + index),
                EngineRpm: (ushort)(11000 + index),
                Drs: index % 2 == 0,
                RevLightsPercent: 0,
                RevLightsBitValue: 0,
                BrakesTemperature: new WheelSet<ushort>(500, 500, 500, 500),
                TyresSurfaceTemperature: new WheelSet<byte>(90, 90, 90, 90),
                TyresInnerTemperature: new WheelSet<byte>(80, 80, 80, 80),
                EngineTemperature: 100,
                TyresPressure: new WheelSet<float>(22f, 22f, 22f, 22f),
                SurfaceType: new WheelSet<byte>(0, 0, 0, 0));
        }

        return cars;
    }

    private static CarStatusData[] BuildStatusCars()
    {
        var cars = new CarStatusData[22];

        for (var index = 0; index < cars.Length; index++)
        {
            cars[index] = new CarStatusData(
                TractionControl: 0,
                AntiLockBrakes: false,
                FuelMix: 0,
                FrontBrakeBias: 56,
                PitLimiterStatus: false,
                FuelInTank: 15.5f + index,
                FuelCapacity: 100f,
                FuelRemainingLaps: 20f,
                MaxRpm: 15000,
                IdleRpm: 4000,
                MaxGears: 8,
                DrsAllowed: true,
                DrsActivationDistance: 200,
                ActualTyreCompound: (byte)(16 + index),
                VisualTyreCompound: (byte)(6 + index),
                TyresAgeLaps: (byte)(3 + index),
                VehicleFiaFlags: 0,
                EnginePowerIce: 1f,
                EnginePowerMguk: 1f,
                ErsStoreEnergy: 1f,
                ErsDeployMode: 0,
                ErsHarvestedThisLapMguk: 0f,
                ErsHarvestedThisLapMguh: 0f,
                ErsDeployedThisLap: 0f,
                NetworkPaused: false);
        }

        return cars;
    }

    private static CarDamageData[] BuildDamageCars(int playerIndex, int visibleOpponentIndex)
    {
        var cars = new CarDamageData[22];

        for (var index = 0; index < cars.Length; index++)
        {
            cars[index] = CreateDamageData(
                tyreWear: index == playerIndex ? 30f : 8f,
                frontLeftWingDamage: 0,
                brakeDamage: 0,
                engineIceWear: 0,
                drsFault: false,
                ersFault: false);
        }

        cars[playerIndex] = CreateDamageData(
            tyreWear: 30f,
            frontLeftWingDamage: 30,
            brakeDamage: 12,
            engineIceWear: 82,
            drsFault: true,
            ersFault: true);
        cars[visibleOpponentIndex] = CreateDamageData(
            tyreWear: 40f,
            frontLeftWingDamage: 45,
            brakeDamage: 18,
            engineIceWear: 70,
            drsFault: true,
            ersFault: true);

        return cars;
    }

    private static CarDamageData CreateDamageData(
        float tyreWear,
        byte frontLeftWingDamage,
        byte brakeDamage,
        byte engineIceWear,
        bool drsFault,
        bool ersFault)
    {
        return new CarDamageData(
            TyreWear: new WheelSet<float>(tyreWear, tyreWear, tyreWear, tyreWear),
            TyreDamage: new WheelSet<byte>(0, 0, 0, 0),
            BrakesDamage: new WheelSet<byte>(brakeDamage, brakeDamage, brakeDamage, brakeDamage),
            TyreBlisters: new WheelSet<byte>(0, 0, 0, 0),
            FrontLeftWingDamage: frontLeftWingDamage,
            FrontRightWingDamage: 0,
            RearWingDamage: 0,
            FloorDamage: 0,
            DiffuserDamage: 0,
            SidepodDamage: 0,
            DrsFault: drsFault,
            ErsFault: ersFault,
            GearBoxDamage: 0,
            EngineDamage: 0,
            EngineMguhWear: 0,
            EngineEsWear: 0,
            EngineCeWear: 0,
            EngineIceWear: engineIceWear,
            EngineMgukWear: 0,
            EngineTcWear: 0,
            EngineBlown: false,
            EngineSeized: false);
    }
}
