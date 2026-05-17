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
                        new LapHistoryData(91_500, 31_000, 0, 30_500, 0, 0, 0, 0x07)
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
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, allLaps.Select(lap => lap.LapNumber));
        var summary = Assert.Single(allLaps, lap => lap.LapNumber == 5);

        Assert.Equal(5, summary.LapNumber);
        Assert.Equal((uint)91_500, summary.LapTimeInMs);
        Assert.Equal((uint)31_000, summary.Sector1TimeInMs);
        Assert.Equal((uint)30_500, summary.Sector2TimeInMs);
        Assert.Equal((uint)30_000, summary.Sector3TimeInMs);
        Assert.NotNull(summary.AverageSpeedKph);
        Assert.InRange(summary.AverageSpeedKph!.Value, 199.5d, 200.5d);
        Assert.NotNull(summary.FuelUsedLitres);
        Assert.InRange(summary.FuelUsedLitres!.Value, 0.89f, 0.91f);
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
        Assert.Equal(1, analyzer.CaptureBestLap()?.LapNumber);
    }

    /// <summary>
    /// Verifies player session history packets can backfill completed qualifying laps without live lap summaries.
    /// </summary>
    [Fact]
    public void Observe_PlayerSessionHistoryWithoutRealtimeSummary_CreatesOfficialCompletedLaps()
    {
        var analyzer = new LapAnalyzer();

        analyzer.Observe(
            CreateParsedPacket(
                new SessionHistoryPacket(
                    CarIndex: 3,
                    NumLaps: 4,
                    NumTyreStints: 0,
                    BestLapTimeLapNumber: 1,
                    BestSector1LapNumber: 1,
                    BestSector2LapNumber: 2,
                    BestSector3LapNumber: 4,
                    LapHistory:
                    [
                        new LapHistoryData(90_000, 30_000, 0, 30_500, 0, 29_500, 0, 0x01),
                        new LapHistoryData(91_000, 30_200, 0, 30_700, 0, 30_100, 0, 0x00),
                        new LapHistoryData(0, 0, 0, 0, 0, 0, 0, 0x00),
                        new LapHistoryData(92_000, 30_400, 0, 30_800, 0, 0, 0, 0x01)
                    ],
                    TyreStints: Array.Empty<TyreStintHistoryData>()),
                playerCarIndex: 3,
                frameIdentifier: 110),
            CreateState(CreatePlayerCar(
                lapNumber: 5,
                lapDistance: 120f,
                currentLapTimeInMs: 10_000,
                lastLapTimeInMs: 92_000,
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

        var allLaps = analyzer.CaptureAllLaps();

        Assert.Equal(new[] { 1, 2, 4 }, allLaps.Select(summary => summary.LapNumber));
        Assert.Equal((uint)90_000, allLaps[0].LapTimeInMs);
        Assert.True(allLaps[0].IsValid);
        Assert.False(allLaps[1].IsValid);
        Assert.Equal((uint)30_800, allLaps[2].Sector2TimeInMs);
        Assert.Equal((uint)30_800, allLaps[2].Sector3TimeInMs);
        Assert.Equal(4, analyzer.CaptureLastLap()?.LapNumber);
        Assert.Equal(1, analyzer.CaptureBestLap()?.LapNumber);
    }

    /// <summary>
    /// Verifies official history updates an existing live lap summary without adding a duplicate row.
    /// </summary>
    [Fact]
    public void Observe_PlayerSessionHistoryWithExistingLap_UpdatesWithoutDuplicate()
    {
        var analyzer = new LapAnalyzer();

        analyzer.Observe(
            CreateParsedPacket(
                new LapDataPacket(Array.Empty<LapDataEntry>(), 255, 255),
                playerCarIndex: 3,
                frameIdentifier: 120),
            CreateState(CreatePlayerCar(
                lapNumber: 2,
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
                new LapDataPacket(Array.Empty<LapDataEntry>(), 255, 255),
                playerCarIndex: 3,
                frameIdentifier: 121),
            CreateState(CreatePlayerCar(
                lapNumber: 3,
                lapDistance: 35f,
                currentLapTimeInMs: 250,
                lastLapTimeInMs: 90_000,
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
                    NumLaps: 2,
                    NumTyreStints: 0,
                    BestLapTimeLapNumber: 2,
                    BestSector1LapNumber: 2,
                    BestSector2LapNumber: 2,
                    BestSector3LapNumber: 2,
                    LapHistory:
                    [
                        new LapHistoryData(0, 0, 0, 0, 0, 0, 0, 0),
                        new LapHistoryData(89_500, 30_000, 0, 30_100, 0, 29_400, 0, 0x01)
                    ],
                    TyreStints: Array.Empty<TyreStintHistoryData>()),
                playerCarIndex: 3,
                frameIdentifier: 122),
            CreateState(CreatePlayerCar(
                lapNumber: 3,
                lapDistance: 100f,
                currentLapTimeInMs: 1_000,
                lastLapTimeInMs: 90_000,
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

        var summary = Assert.Single(analyzer.CaptureAllLaps());

        Assert.Equal(2, summary.LapNumber);
        Assert.Equal((uint)89_500, summary.LapTimeInMs);
        Assert.Equal((uint)29_400, summary.Sector3TimeInMs);
        Assert.NotNull(summary.AverageSpeedKph);
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

    /// <summary>
    /// Verifies that in-flight lap samples can be read as an immutable snapshot.
    /// </summary>
    [Fact]
    public void CaptureCurrentLapSamples_ReturnsIndependentSnapshot()
    {
        var analyzer = new LapAnalyzer();

        analyzer.Observe(
            CreateParsedPacket(
                new LapDataPacket(Array.Empty<LapDataEntry>(), 255, 255),
                playerCarIndex: 3,
                frameIdentifier: 400),
            CreateState(CreatePlayerCar(
                lapNumber: 11,
                lapDistance: 120f,
                currentLapTimeInMs: 10_000,
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

        var firstSnapshot = analyzer.CaptureCurrentLapSamples();

        analyzer.Observe(
            CreateParsedPacket(
                new LapDataPacket(Array.Empty<LapDataEntry>(), 255, 255),
                playerCarIndex: 3,
                frameIdentifier: 401),
            CreateState(CreatePlayerCar(
                lapNumber: 11,
                lapDistance: 240f,
                currentLapTimeInMs: 12_000,
                lastLapTimeInMs: 90_000,
                speedKph: 188,
                throttle: 0.84,
                brake: 0.00,
                steering: 0.02f,
                gear: 6,
                fuelRemaining: 24.6f,
                fuelLapsRemaining: 10.8f,
                ersStoreEnergy: 2.5f,
                tyreWear: 8.2f,
                position: 6,
                deltaFront: 680,
                deltaLeader: 8_900,
                pitStatus: 0,
                isCurrentLapValid: true,
                visualTyreCompound: 18,
                actualTyreCompound: 21)));

        var secondSnapshot = analyzer.CaptureCurrentLapSamples();

        Assert.Single(firstSnapshot);
        Assert.Equal(120f, firstSnapshot[0].LapDistance);
        Assert.Equal(2, secondSnapshot.Count);
        Assert.Equal(240f, secondSnapshot[^1].LapDistance);
    }

    /// <summary>
    /// Verifies session reset clears in-memory lap history and current lap state.
    /// </summary>
    [Fact]
    public void ResetForSession_ChangesHistoryAndState()
    {
        var analyzer = new LapAnalyzer();

        analyzer.Observe(
            CreateParsedPacket(
                new LapDataPacket(Array.Empty<LapDataEntry>(), 255, 255),
                playerCarIndex: 3,
                frameIdentifier: 600),
            CreateState(CreatePlayerCar(
                lapNumber: 1,
                lapDistance: 100f,
                currentLapTimeInMs: 1_500,
                lastLapTimeInMs: 95_000,
                speedKph: 168,
                throttle: 0.70,
                brake: 0.01,
                steering: 0.01f,
                gear: 5,
                fuelRemaining: 20f,
                fuelLapsRemaining: 8.5f,
                ersStoreEnergy: 2.8f,
                tyreWear: 6.6f,
                position: 2,
                deltaFront: 800,
                deltaLeader: 5_500,
                pitStatus: 0,
                isCurrentLapValid: true,
                visualTyreCompound: 16,
                actualTyreCompound: 19)));

        analyzer.Observe(
            CreateParsedPacket(
                new LapDataPacket(Array.Empty<LapDataEntry>(), 255, 255),
                playerCarIndex: 3,
                frameIdentifier: 601),
            CreateState(CreatePlayerCar(
                lapNumber: 2,
                lapDistance: 10f,
                currentLapTimeInMs: 180,
                lastLapTimeInMs: 93_000,
                speedKph: 150,
                throttle: 0.66,
                brake: 0.00,
                steering: 0.03f,
                gear: 4,
                fuelRemaining: 19.8f,
                fuelLapsRemaining: 8.4f,
                ersStoreEnergy: 2.6f,
                tyreWear: 6.7f,
                position: 2,
                deltaFront: 760,
                deltaLeader: 5_200,
                pitStatus: 0,
                isCurrentLapValid: true,
                visualTyreCompound: 16,
                actualTyreCompound: 19)));

        Assert.Single(analyzer.CaptureAllLaps());
        Assert.NotNull(analyzer.CaptureLastLap());
        Assert.NotNull(analyzer.CaptureBestLap());

        analyzer.ResetForSession(99);

        Assert.Empty(analyzer.CaptureAllLaps());
        Assert.Null(analyzer.CaptureLastLap());
        Assert.Null(analyzer.CaptureBestLap());
        Assert.Empty(analyzer.CaptureCurrentLapSamples());
    }

    /// <summary>
    /// Verifies that player car damage packets enrich current lap samples with four-wheel wear.
    /// </summary>
    [Fact]
    public void Observe_PlayerCarDamagePacket_CapturesTyreWearPerWheel()
    {
        var analyzer = new LapAnalyzer();

        analyzer.Observe(
            CreateParsedPacket(
                new LapDataPacket(Array.Empty<LapDataEntry>(), 255, 255),
                playerCarIndex: 3,
                frameIdentifier: 500),
            CreateState(CreatePlayerCar(
                lapNumber: 12,
                lapDistance: 200f,
                currentLapTimeInMs: 14_000,
                lastLapTimeInMs: 90_000,
                speedKph: 192,
                throttle: 0.86,
                brake: 0.00,
                steering: 0.01f,
                gear: 7,
                fuelRemaining: 24.4f,
                fuelLapsRemaining: 10.7f,
                ersStoreEnergy: 2.4f,
                tyreWear: 8.3f,
                position: 6,
                deltaFront: 650,
                deltaLeader: 8_700,
                pitStatus: 0,
                isCurrentLapValid: true,
                visualTyreCompound: 18,
                actualTyreCompound: 21)));

        analyzer.Observe(
            CreateParsedPacket(
                new CarDamagePacket(CreateDamageCars(new WheelSet<float>(11f, 12f, 13f, 14f))),
                playerCarIndex: 3,
                frameIdentifier: 501),
            CreateState(CreatePlayerCar(
                lapNumber: 12,
                lapDistance: 260f,
                currentLapTimeInMs: 15_000,
                lastLapTimeInMs: 90_000,
                speedKph: 195,
                throttle: 0.88,
                brake: 0.00,
                steering: -0.01f,
                gear: 7,
                fuelRemaining: 24.3f,
                fuelLapsRemaining: 10.7f,
                ersStoreEnergy: 2.3f,
                tyreWear: 8.4f,
                position: 6,
                deltaFront: 640,
                deltaLeader: 8_650,
                pitStatus: 0,
                isCurrentLapValid: true,
                visualTyreCompound: 18,
                actualTyreCompound: 21)));

        var samples = analyzer.CaptureCurrentLapSamples();
        var latest = Assert.Single(samples.Skip(1));

        Assert.NotNull(latest.TyreWearPerWheel);
        Assert.Equal(11f, latest.TyreWearPerWheel!.RearLeft);
        Assert.Equal(14f, latest.TyreWearPerWheel.FrontRight);
    }

    /// <summary>
    /// Verifies that completed laps expose four-wheel wear deltas.
    /// </summary>
    [Fact]
    public void Observe_LapCloses_ComputesTyreWearDeltaPerWheel()
    {
        var analyzer = new LapAnalyzer();

        analyzer.Observe(
            CreateParsedPacket(
                new CarDamagePacket(CreateDamageCars(new WheelSet<float>(10f, 10f, 8f, 8f))),
                playerCarIndex: 3,
                frameIdentifier: 600),
            CreateState(CreatePlayerCar(
                lapNumber: 13,
                lapDistance: 100f,
                currentLapTimeInMs: 2_000,
                lastLapTimeInMs: 89_000,
                speedKph: 160,
                throttle: 0.72,
                brake: 0.00,
                steering: 0.02f,
                gear: 5,
                fuelRemaining: 24.0f,
                fuelLapsRemaining: 10.4f,
                ersStoreEnergy: 2.2f,
                tyreWear: 9.0f,
                position: 6,
                deltaFront: 620,
                deltaLeader: 8_400,
                pitStatus: 0,
                isCurrentLapValid: true,
                visualTyreCompound: 18,
                actualTyreCompound: 21)));

        analyzer.Observe(
            CreateParsedPacket(
                new CarDamagePacket(CreateDamageCars(new WheelSet<float>(12f, 13f, 10f, 11f))),
                playerCarIndex: 3,
                frameIdentifier: 601),
            CreateState(CreatePlayerCar(
                lapNumber: 13,
                lapDistance: 4_600f,
                currentLapTimeInMs: 89_000,
                lastLapTimeInMs: 89_000,
                speedKph: 220,
                throttle: 0.98,
                brake: 0.00,
                steering: -0.02f,
                gear: 8,
                fuelRemaining: 23.1f,
                fuelLapsRemaining: 10.0f,
                ersStoreEnergy: 1.8f,
                tyreWear: 10.2f,
                position: 5,
                deltaFront: 280,
                deltaLeader: 6_700,
                pitStatus: 0,
                isCurrentLapValid: true,
                visualTyreCompound: 18,
                actualTyreCompound: 21)));

        analyzer.Observe(
            CreateParsedPacket(
                new LapDataPacket(Array.Empty<LapDataEntry>(), 255, 255),
                playerCarIndex: 3,
                frameIdentifier: 602),
            CreateState(CreatePlayerCar(
                lapNumber: 14,
                lapDistance: 25f,
                currentLapTimeInMs: 150,
                lastLapTimeInMs: 89_000,
                speedKph: 150,
                throttle: 0.64,
                brake: 0.00,
                steering: 0.01f,
                gear: 4,
                fuelRemaining: 23.0f,
                fuelLapsRemaining: 9.9f,
                ersStoreEnergy: 1.7f,
                tyreWear: 10.3f,
                position: 5,
                deltaFront: 260,
                deltaLeader: 6_500,
                pitStatus: 0,
                isCurrentLapValid: true,
                visualTyreCompound: 18,
                actualTyreCompound: 21)));

        var summary = Assert.Single(analyzer.CaptureAllLaps());

        Assert.NotNull(summary.TyreWearDeltaPerWheel);
        Assert.Equal(2f, summary.TyreWearDeltaPerWheel!.RearLeft);
        Assert.Equal(3f, summary.TyreWearDeltaPerWheel.RearRight);
        Assert.Equal(2f, summary.TyreWearDeltaPerWheel.FrontLeft);
        Assert.Equal(3f, summary.TyreWearDeltaPerWheel.FrontRight);
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

    private static ParsedPacket CreateParsedPacket(
        IUdpPacket packet,
        byte playerCarIndex,
        uint frameIdentifier,
        ulong sessionUid = 1)
    {
        var header = new PacketHeader(
            PacketFormat: 2025,
            GameYear: 25,
            GameMajorVersion: 1,
            GameMinorVersion: 0,
            PacketVersion: 1,
            RawPacketId: GetPacketId(packet),
            SessionUid: sessionUid,
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
            CarDamagePacket => (byte)PacketId.CarDamage,
            SessionHistoryPacket => (byte)PacketId.SessionHistory,
            _ => throw new ArgumentOutOfRangeException(nameof(packet))
        };
    }

    private static CarDamageData[] CreateDamageCars(WheelSet<float> playerTyreWear)
    {
        var defaultDamage = new CarDamageData(
            TyreWear: new WheelSet<float>(0f, 0f, 0f, 0f),
            TyreDamage: new WheelSet<byte>(0, 0, 0, 0),
            BrakesDamage: new WheelSet<byte>(0, 0, 0, 0),
            TyreBlisters: new WheelSet<byte>(0, 0, 0, 0),
            FrontLeftWingDamage: 0,
            FrontRightWingDamage: 0,
            RearWingDamage: 0,
            FloorDamage: 0,
            DiffuserDamage: 0,
            SidepodDamage: 0,
            DrsFault: false,
            ErsFault: false,
            GearBoxDamage: 0,
            EngineDamage: 0,
            EngineMguhWear: 0,
            EngineEsWear: 0,
            EngineCeWear: 0,
            EngineIceWear: 0,
            EngineMgukWear: 0,
            EngineTcWear: 0,
            EngineBlown: false,
            EngineSeized: false);

        var cars = Enumerable.Repeat(defaultDamage, 22).ToArray();
        cars[3] = defaultDamage with { TyreWear = playerTyreWear };
        return cars;
    }
}
