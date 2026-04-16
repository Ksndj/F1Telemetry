using System.Net;
using F1Telemetry.Analytics.Laps;
using F1Telemetry.Core.Models;
using F1Telemetry.Udp.Packets;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies that player-lap sampling is closed into summaries with basic fault tolerance.
/// </summary>
public sealed class LapAnalyzerTests
{
    /// <summary>
    /// Verifies that a completed player lap is summarized when the lap number advances.
    /// </summary>
    [Fact]
    public void Observe_LapNumberAdvances_CreatesSummaryForClosedLap()
    {
        var analyzer = new LapAnalyzer();

        analyzer.Observe(
            CreateParsedPacket(
                new LapDataPacket(Array.Empty<LapDataEntry>(), 255, 255),
                playerCarIndex: 3,
                frameIdentifier: 100),
            CreateState(CreatePlayerCar(
                lapNumber: 5,
                lapDistance: 120f,
                currentLapTimeInMs: 10_000,
                lastLapTimeInMs: 94_000,
                speedKph: 180,
                throttle: 0.82,
                brake: 0.05,
                steering: 0.10f,
                gear: 7,
                fuelRemaining: 29.4f,
                fuelLapsRemaining: 13.2f,
                ersStoreEnergy: 3.7f,
                tyreWear: 4.5f,
                position: 4,
                deltaFront: 900,
                deltaLeader: 6_400,
                pitStatus: 0,
                isCurrentLapValid: true,
                visualTyreCompound: 16,
                actualTyreCompound: 19)));

        analyzer.Observe(
            CreateParsedPacket(
                new CarTelemetryPacket(Array.Empty<CarTelemetryData>(), 255, 255, 0),
                playerCarIndex: 3,
                frameIdentifier: 101),
            CreateState(CreatePlayerCar(
                lapNumber: 5,
                lapDistance: 4_800f,
                currentLapTimeInMs: 91_500,
                lastLapTimeInMs: 94_000,
                speedKph: 220,
                throttle: 0.96,
                brake: 0.00,
                steering: -0.03f,
                gear: 8,
                fuelRemaining: 28.5f,
                fuelLapsRemaining: 12.8f,
                ersStoreEnergy: 3.1f,
                tyreWear: 5.6f,
                position: 3,
                deltaFront: 300,
                deltaLeader: 4_900,
                pitStatus: 2,
                isCurrentLapValid: true,
                visualTyreCompound: 16,
                actualTyreCompound: 19)));

        analyzer.Observe(
            CreateParsedPacket(
                new LapDataPacket(Array.Empty<LapDataEntry>(), 255, 255),
                playerCarIndex: 3,
                frameIdentifier: 102),
            CreateState(CreatePlayerCar(
                lapNumber: 6,
                lapDistance: 35f,
                currentLapTimeInMs: 250,
                lastLapTimeInMs: 91_500,
                speedKph: 155,
                throttle: 0.70,
                brake: 0.00,
                steering: 0.02f,
                gear: 5,
                fuelRemaining: 28.4f,
                fuelLapsRemaining: 12.7f,
                ersStoreEnergy: 3.0f,
                tyreWear: 5.7f,
                position: 3,
                deltaFront: 280,
                deltaLeader: 4_850,
                pitStatus: 0,
                isCurrentLapValid: true,
                visualTyreCompound: 16,
                actualTyreCompound: 19)));

        analyzer.Observe(
            CreateParsedPacket(
                new SessionHistoryPacket(
                    CarIndex: 3,
                    NumLaps: 5,
                    NumTyreStints: 0,
                    BestLapTimeLapNumber: 5,
                    BestSector1LapNumber: 5,
                    BestSector2LapNumber: 5,
                    BestSector3LapNumber: 5,
                    LapHistory:
                    [
                        new LapHistoryData(90_000, 0, 0, 0, 0, 0, 0, 1),
                        new LapHistoryData(90_500, 0, 0, 0, 0, 0, 0, 1),
                        new LapHistoryData(91_000, 0, 0, 0, 0, 0, 0, 1),
                        new LapHistoryData(91_200, 0, 0, 0, 0, 0, 0, 1),
                        new LapHistoryData(91_500, 31_000, 0, 30_500, 0, 30_000, 0, 0x0F)
                    ],
                    TyreStints: Array.Empty<TyreStintHistoryData>()),
                playerCarIndex: 3,
                frameIdentifier: 103),
            CreateState(CreatePlayerCar(
                lapNumber: 6,
                lapDistance: 100f,
                currentLapTimeInMs: 1_000,
                lastLapTimeInMs: 91_500,
                speedKph: 180,
                throttle: 0.80,
                brake: 0.00,
                steering: 0.01f,
                gear: 6,
                fuelRemaining: 28.2f,
                fuelLapsRemaining: 12.6f,
                ersStoreEnergy: 2.9f,
                tyreWear: 5.8f,
                position: 3,
                deltaFront: 250,
                deltaLeader: 4_700,
                pitStatus: 0,
                isCurrentLapValid: true,
                visualTyreCompound: 16,
                actualTyreCompound: 19)));

        var allLaps = analyzer.CaptureAllLaps();
        var summary = Assert.Single(allLaps);

        Assert.Equal(5, summary.LapNumber);
        Assert.Equal((uint)91_500, summary.LapTimeInMs);
        Assert.Equal((uint)31_000, summary.Sector1TimeInMs);
        Assert.Equal((uint)30_500, summary.Sector2TimeInMs);
        Assert.Equal((uint)30_000, summary.Sector3TimeInMs);
        Assert.NotNull(summary.AverageSpeedKph);
        Assert.InRange(summary.AverageSpeedKph!.Value, 199.5d, 200.5d);
        Assert.NotNull(summary.FuelUsed);
        Assert.InRange(summary.FuelUsed!.Value, 0.89f, 0.91f);
        Assert.NotNull(summary.ErsUsed);
        Assert.InRange(summary.ErsUsed!.Value, 0.59f, 0.61f);
        Assert.NotNull(summary.TyreWearDelta);
        Assert.InRange(summary.TyreWearDelta!.Value, 1.09f, 1.11f);
        Assert.True(summary.IsValid);
        Assert.Equal("V16 / A19", summary.StartTyre);
        Assert.Equal("V16 / A19", summary.EndTyre);
        Assert.False(summary.StartedInPit);
        Assert.True(summary.EndedInPit);
        Assert.Same(summary, analyzer.CaptureLastLap());
        Assert.Same(summary, analyzer.CaptureBestLap());
    }

    /// <summary>
    /// Verifies that the first observed lap is treated as a seed when sampling starts mid-lap.
    /// </summary>
    [Fact]
    public void Observe_FirstSeenMidLap_DoesNotEmitPartialOpeningLap()
    {
        var analyzer = new LapAnalyzer();

        analyzer.Observe(
            CreateParsedPacket(
                new LapDataPacket(Array.Empty<LapDataEntry>(), 255, 255),
                playerCarIndex: 3,
                frameIdentifier: 200),
            CreateState(CreatePlayerCar(
                lapNumber: 8,
                lapDistance: 2_700f,
                currentLapTimeInMs: 48_000,
                lastLapTimeInMs: 93_000,
                speedKph: 205,
                throttle: 0.88,
                brake: 0.02,
                steering: 0.00f,
                gear: 7,
                fuelRemaining: 26.0f,
                fuelLapsRemaining: 11.5f,
                ersStoreEnergy: 3.0f,
                tyreWear: 7.0f,
                position: 5,
                deltaFront: 1_200,
                deltaLeader: 7_900,
                pitStatus: 0,
                isCurrentLapValid: true,
                visualTyreCompound: 17,
                actualTyreCompound: 20)));

        analyzer.Observe(
            CreateParsedPacket(
                new LapDataPacket(Array.Empty<LapDataEntry>(), 255, 255),
                playerCarIndex: 3,
                frameIdentifier: 201),
            CreateState(CreatePlayerCar(
                lapNumber: 9,
                lapDistance: 20f,
                currentLapTimeInMs: 150,
                lastLapTimeInMs: 92_800,
                speedKph: 150,
                throttle: 0.72,
                brake: 0.00,
                steering: 0.02f,
                gear: 5,
                fuelRemaining: 25.7f,
                fuelLapsRemaining: 11.3f,
                ersStoreEnergy: 2.8f,
                tyreWear: 7.2f,
                position: 5,
                deltaFront: 1_100,
                deltaLeader: 7_600,
                pitStatus: 0,
                isCurrentLapValid: true,
                visualTyreCompound: 17,
                actualTyreCompound: 20)));

        Assert.Empty(analyzer.CaptureAllLaps());
        Assert.Null(analyzer.CaptureLastLap());
        Assert.Null(analyzer.CaptureBestLap());
    }

    /// <summary>
    /// Verifies that frame regression clears the in-flight lap instead of emitting a bogus summary.
    /// </summary>
    [Fact]
    public void Observe_FrameRegresses_DropsCurrentLapWithoutClosingIt()
    {
        var analyzer = new LapAnalyzer();

        analyzer.Observe(
            CreateParsedPacket(
                new LapDataPacket(Array.Empty<LapDataEntry>(), 255, 255),
                playerCarIndex: 3,
                frameIdentifier: 300),
            CreateState(CreatePlayerCar(
                lapNumber: 10,
                lapDistance: 50f,
                currentLapTimeInMs: 5_000,
                lastLapTimeInMs: 90_000,
                speedKph: 180,
                throttle: 0.80,
                brake: 0.01,
                steering: 0.00f,
                gear: 6,
                fuelRemaining: 24.8f,
                fuelLapsRemaining: 10.9f,
                ersStoreEnergy: 2.6f,
                tyreWear: 8.0f,
                position: 6,
                deltaFront: 700,
                deltaLeader: 9_100,
                pitStatus: 0,
                isCurrentLapValid: true,
                visualTyreCompound: 18,
                actualTyreCompound: 21)));

        analyzer.Observe(
            CreateParsedPacket(
                new LapDataPacket(Array.Empty<LapDataEntry>(), 255, 255),
                playerCarIndex: 3,
                frameIdentifier: 301),
            CreateState(CreatePlayerCar(
                lapNumber: 10,
                lapDistance: 3_100f,
                currentLapTimeInMs: 56_000,
                lastLapTimeInMs: 90_000,
                speedKph: 215,
                throttle: 0.95,
                brake: 0.00,
                steering: -0.04f,
                gear: 8,
                fuelRemaining: 24.0f,
                fuelLapsRemaining: 10.5f,
                ersStoreEnergy: 2.1f,
                tyreWear: 8.8f,
                position: 6,
                deltaFront: 650,
                deltaLeader: 8_600,
                pitStatus: 0,
                isCurrentLapValid: true,
                visualTyreCompound: 18,
                actualTyreCompound: 21)));

        analyzer.Observe(
            CreateParsedPacket(
                new LapDataPacket(Array.Empty<LapDataEntry>(), 255, 255),
                playerCarIndex: 3,
                frameIdentifier: 250),
            CreateState(CreatePlayerCar(
                lapNumber: 10,
                lapDistance: 400f,
                currentLapTimeInMs: 7_000,
                lastLapTimeInMs: 90_000,
                speedKph: 175,
                throttle: 0.78,
                brake: 0.04,
                steering: 0.06f,
                gear: 6,
                fuelRemaining: 24.7f,
                fuelLapsRemaining: 10.8f,
                ersStoreEnergy: 2.5f,
                tyreWear: 8.1f,
                position: 6,
                deltaFront: 690,
                deltaLeader: 9_000,
                pitStatus: 0,
                isCurrentLapValid: true,
                visualTyreCompound: 18,
                actualTyreCompound: 21)));

        analyzer.Observe(
            CreateParsedPacket(
                new LapDataPacket(Array.Empty<LapDataEntry>(), 255, 255),
                playerCarIndex: 3,
                frameIdentifier: 251),
            CreateState(CreatePlayerCar(
                lapNumber: 11,
                lapDistance: 15f,
                currentLapTimeInMs: 200,
                lastLapTimeInMs: 89_800,
                speedKph: 140,
                throttle: 0.65,
                brake: 0.00,
                steering: 0.01f,
                gear: 4,
                fuelRemaining: 24.5f,
                fuelLapsRemaining: 10.6f,
                ersStoreEnergy: 2.4f,
                tyreWear: 8.3f,
                position: 6,
                deltaFront: 600,
                deltaLeader: 8_400,
                pitStatus: 0,
                isCurrentLapValid: true,
                visualTyreCompound: 18,
                actualTyreCompound: 21)));

        Assert.Empty(analyzer.CaptureAllLaps());
    }

    private static SessionState CreateState(CarSnapshot playerCar)
    {
        return new SessionState
        {
            PlayerCarIndex = (byte)playerCar.CarIndex,
            PlayerCar = playerCar,
            Cars = new[] { playerCar },
            Opponents = Array.Empty<CarSnapshot>(),
            UpdatedAt = playerCar.UpdatedAt
        };
    }

    private static CarSnapshot CreatePlayerCar(
        byte lapNumber,
        float lapDistance,
        uint currentLapTimeInMs,
        uint lastLapTimeInMs,
        double speedKph,
        double throttle,
        double brake,
        float steering,
        sbyte gear,
        float fuelRemaining,
        float fuelLapsRemaining,
        float ersStoreEnergy,
        float tyreWear,
        byte position,
        ushort deltaFront,
        ushort deltaLeader,
        byte pitStatus,
        bool isCurrentLapValid,
        byte visualTyreCompound,
        byte actualTyreCompound)
    {
        return new CarSnapshot
        {
            CarIndex = 3,
            IsPlayer = true,
            Position = position,
            CurrentLapNumber = lapNumber,
            LastLapTimeInMs = lastLapTimeInMs,
            CurrentLapTimeInMs = currentLapTimeInMs,
            LapDistance = lapDistance,
            DeltaToCarInFrontInMs = deltaFront,
            DeltaToRaceLeaderInMs = deltaLeader,
            Telemetry = new TelemetrySnapshot(
                Timestamp: DateTimeOffset.UtcNow,
                LapNumber: lapNumber,
                SpeedKph: speedKph,
                Throttle: throttle,
                Brake: brake,
                TrackName: null),
            SteeringInput = steering,
            Gear = gear,
            FuelInTank = fuelRemaining,
            FuelRemainingLaps = fuelLapsRemaining,
            ErsStoreEnergy = ersStoreEnergy,
            TyreWear = tyreWear,
            PitStatus = pitStatus,
            IsCurrentLapValid = isCurrentLapValid,
            VisualTyreCompound = visualTyreCompound,
            ActualTyreCompound = actualTyreCompound,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private static ParsedPacket CreateParsedPacket(IUdpPacket packet, byte playerCarIndex, uint frameIdentifier)
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
            FrameIdentifier: frameIdentifier,
            OverallFrameIdentifier: frameIdentifier,
            PlayerCarIndex: playerCarIndex,
            SecondaryPlayerCarIndex: 255);

        var datagram = new UdpDatagram(Array.Empty<byte>(), new IPEndPoint(IPAddress.Loopback, 20777), DateTimeOffset.UtcNow);
        return new ParsedPacket((PacketId)header.RawPacketId, header, packet, datagram);
    }

    private static byte GetPacketId(IUdpPacket packet)
    {
        return packet switch
        {
            LapDataPacket => (byte)PacketId.LapData,
            CarTelemetryPacket => (byte)PacketId.CarTelemetry,
            SessionHistoryPacket => (byte)PacketId.SessionHistory,
            _ => throw new ArgumentOutOfRangeException(nameof(packet))
        };
    }
}
