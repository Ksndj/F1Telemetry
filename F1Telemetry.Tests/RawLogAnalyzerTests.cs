using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Text.Json;
using F1Telemetry.RawLogAnalyzer;
using F1Telemetry.Udp.Packets;
using Xunit;

namespace F1Telemetry.Tests;

public sealed class RawLogAnalyzerTests
{
    private const int LapDataEntryTestSize = 57;

    [Fact]
    public async Task AnalyzeAsync_WritesRaceAnalysisReportSkeletonForSelectedSession()
    {
        var inputPath = CreateTempJsonlPath();
        var outputPath = Path.ChangeExtension(inputPath, ".md");
        var statusPayload = Convert.ToBase64String(BuildCarStatusPacket(1001UL, fuelInTank: 5.5f, actualCompound: 16, visualCompound: 17));
        var lines = new[]
        {
            BuildRecord(BuildSessionPacket(0UL, trackId: 1, sessionType: 15, totalLaps: 1)),
            BuildRecord(BuildSessionPacket(1001UL, trackId: 9, sessionType: 15, totalLaps: 70)),
            BuildRecord(BuildLapDataPacket(1001UL, currentLap: 1, totalDistance: 1234.5f, position: 5, resultStatus: 2)),
            BuildRecord(BuildSessionHistoryPacket(1001UL, numLaps: 1, firstLapTimeInMs: 84289)),
            BuildRecord(BuildFinalClassificationPacket(1001UL, position: 4, numLaps: 3, gridPosition: 8, points: 12, bestLapTimeInMs: 84289, penaltiesTime: 5, numPenalties: 1)),
            BuildRecord(BuildCarTelemetryPacket(1001UL, speed: 321, throttle: 0.75f)),
            BuildRecord(BuildCarStatusPacket(1001UL, fuelInTank: 5.5f, actualCompound: 16, visualCompound: 17)),
            BuildRecord(BuildCarDamagePacket(1001UL, tyreWear: 12.5f)),
            BuildRecord(BuildTyreSetsPacket(1001UL, actualCompound: 16, visualCompound: 17, wear: 4)),
            BuildRecord(BuildEventPacket(1001UL, "SSTA")),
            BuildRecord(BuildSessionPacket(2002UL, trackId: 13, sessionType: 12, totalLaps: 10))
        };
        await File.WriteAllLinesAsync(inputPath, lines);
        var analyzer = new RawLogAnalyzerService();

        var result = await analyzer.AnalyzeAsync(new RawLogAnalyzerOptions(inputPath, outputPath, 1001UL));

        Assert.Equal(11, result.TotalLines);
        Assert.NotNull(result.RaceReport);
        Assert.Equal(1001UL, result.RaceReport.SessionUid);
        Assert.Equal("synthetic.jsonl", result.RaceReport.InputFile);
        Assert.Equal(9, result.RaceReport.SessionSummary.TrackId);
        Assert.Equal(15, result.RaceReport.SessionSummary.SessionType);
        Assert.Equal(70, result.RaceReport.SessionSummary.TotalLaps);
        Assert.Equal(4, result.RaceReport.PlayerRaceSummary.FinalPosition);
        Assert.Equal(8, result.RaceReport.PlayerRaceSummary.GridPosition);
        Assert.Equal(3, result.RaceReport.PlayerRaceSummary.CompletedLaps);
        Assert.Equal(12, result.RaceReport.PlayerRaceSummary.Points);
        Assert.Equal(84289U, result.RaceReport.PlayerRaceSummary.BestLapTimeInMs);
        Assert.Equal(5, result.RaceReport.PlayerRaceSummary.PenaltiesTimeSeconds);
        Assert.Single(result.RaceReport.LapSummaries);
        Assert.Equal(1, result.RaceReport.LapSummaries[0].LapNumber);
        Assert.Equal(84289U, result.RaceReport.LapSummaries[0].LapTimeInMs);
        Assert.Equal(1, result.RaceReport.LapSummaries[0].SampleCount);
        Assert.True(result.Sessions.TryGetValue(1001UL, out var firstSession));
        Assert.Equal(9, firstSession.TrackId);
        Assert.Equal(15, firstSession.SessionType);
        Assert.Equal(70, firstSession.TotalLaps);
        Assert.Equal(1, firstSession.MaxPlayerLapNumber);
        Assert.Equal(1234.5f, firstSession.MaxPlayerTotalDistance, precision: 2);
        Assert.Equal(321, firstSession.MaxPlayerSpeed);
        Assert.Equal(0.75f, firstSession.MaxPlayerThrottle, precision: 3);
        Assert.Equal(5.5f, firstSession.MinPlayerFuelInTank, precision: 3);
        Assert.Equal(5.5f, firstSession.MaxPlayerFuelInTank, precision: 3);
        Assert.Equal(12.5f, firstSession.MaxPlayerTyreWear, precision: 3);
        Assert.Contains("visual 17 / actual 16", firstSession.TyreCompoundPairs);
        Assert.Equal(1, firstSession.EventCodeCounts["SSTA"]);
        Assert.Equal(1, result.PacketIdCounts[PacketId.Session]);
        Assert.Equal(outputPath, result.ReportPath);
        Assert.True(File.Exists(outputPath));

        var markdown = await File.ReadAllTextAsync(outputPath);
        Assert.Contains("# Race Analysis Report", markdown);
        Assert.Contains("## Session Summary", markdown);
        Assert.Contains("## Player Summary", markdown);
        Assert.Contains("## Lap Summaries", markdown);
        Assert.Contains("## Data Quality Warnings", markdown);
        Assert.Contains("SessionUid: 1001", markdown);
        Assert.Contains("TrackId: 9", markdown);
        Assert.Contains("Final position: 4", markdown);
        Assert.DoesNotContain("payloadBase64", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(statusPayload, markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("CarTelemetry[]", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Motion[]", markdown, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnalyzeAsync_CountsBadLinesAndContinues()
    {
        var inputPath = CreateTempJsonlPath();
        var outputPath = Path.ChangeExtension(inputPath, ".md");
        var lines = new[]
        {
            "{ this is not json",
            "{\"payloadBase64\":\"%%%\"}",
            BuildRecord(BuildUnknownPacket(3003UL, packetId: 99)),
            BuildRecord(BuildSessionPacket(3003UL, trackId: 1, sessionType: 15, totalLaps: 3), declaredLength: 9999),
            BuildRecord(BuildHeaderOnlyKnownPacket(3003UL, PacketId.Session))
        };
        await File.WriteAllLinesAsync(inputPath, lines);
        var analyzer = new RawLogAnalyzerService();

        var result = await analyzer.AnalyzeAsync(new RawLogAnalyzerOptions(inputPath, outputPath, 3003UL));

        Assert.Equal(5, result.TotalLines);
        Assert.Equal(1, result.InvalidJsonLineCount);
        Assert.Equal(1, result.InvalidBase64LineCount);
        Assert.Equal(1, result.UnknownPacketIdCount);
        Assert.Equal(1, result.PayloadLengthMismatchCount);
        Assert.Equal(1, result.PacketParseFailureCount);
        Assert.Single(result.Sessions);
        Assert.True(result.Sessions.ContainsKey(3003UL));

        var markdown = await File.ReadAllTextAsync(outputPath);
        Assert.Contains("Invalid JSON lines: 1", markdown);
        Assert.Contains("Invalid base64 lines: 1", markdown);
        Assert.Contains("Unknown packet ids: 1", markdown);
        Assert.Contains("Payload length mismatches: 1", markdown);
        Assert.Contains("Packet parse failures: 1", markdown);
    }

    [Fact]
    public async Task AnalyzeAsync_WritesStintAndPitSummariesWithFixedEnums()
    {
        var inputPath = CreateTempJsonlPath();
        var outputPath = Path.ChangeExtension(inputPath, ".md");
        var lines = new[]
        {
            BuildRecord(BuildSessionPacket(1001UL, trackId: 9, sessionType: 15, totalLaps: 3)),
            BuildRecord(BuildLapDataPacket(1001UL, currentLap: 1, totalDistance: 100f, position: 5, resultStatus: 2, numPitStops: 0, lastLapTimeInMs: 90000)),
            BuildRecord(BuildCarStatusPacket(1001UL, fuelInTank: 20f, actualCompound: 16, visualCompound: 17, tyreAgeLaps: 1)),
            BuildRecord(BuildLapDataPacket(1001UL, currentLap: 2, totalDistance: 200f, position: 7, resultStatus: 2, pitStatus: 1, numPitStops: 1, isPitLaneTimerActive: true, pitLaneTimeInLaneInMs: 20000, pitStopTimerInMs: 2500, lastLapTimeInMs: 120000)),
            BuildRecord(BuildCarStatusPacket(1001UL, fuelInTank: 18f, actualCompound: 18, visualCompound: 18, tyreAgeLaps: 0)),
            BuildRecord(BuildLapDataPacket(1001UL, currentLap: 3, totalDistance: 300f, position: 7, resultStatus: 2, numPitStops: 1, lastLapTimeInMs: 91000)),
            BuildRecord(BuildCarStatusPacket(1001UL, fuelInTank: 16f, actualCompound: 18, visualCompound: 18, tyreAgeLaps: 1)),
            BuildRecord(BuildLapDataPacket(1001UL, currentLap: 4, totalDistance: 400f, position: 7, resultStatus: 3, numPitStops: 1, lastLapTimeInMs: 0)),
            BuildRecord(BuildSessionHistoryPacket(
                1001UL,
                numLaps: 3,
                firstLapTimeInMs: 90000,
                lapTimesInMs: [90000U, 120000U, 91000U],
                tyreStints:
                [
                    new TyreStintTestData(EndLap: 1, Actual: 16, Visual: 17),
                    new TyreStintTestData(EndLap: 255, Actual: 18, Visual: 18)
                ])),
            BuildRecord(BuildFinalClassificationPacket(
                1001UL,
                position: 7,
                numLaps: 3,
                gridPosition: 5,
                points: 0,
                bestLapTimeInMs: 90000,
                penaltiesTime: 0,
                numPenalties: 0,
                numPitStops: 1,
                tyreStintsActual: [16, 18],
                tyreStintsVisual: [17, 18],
                tyreStintsEndLaps: [1, 255]))
        };
        await File.WriteAllLinesAsync(inputPath, lines);
        var analyzer = new RawLogAnalyzerService();

        var result = await analyzer.AnalyzeAsync(new RawLogAnalyzerOptions(inputPath, outputPath, 1001UL));

        Assert.NotNull(result.RaceReport);
        Assert.Equal(2, result.RaceReport.StintSummaries.Count);
        Assert.Equal(1, result.RaceReport.StintSummaries[0].StartLap);
        Assert.Equal(1, result.RaceReport.StintSummaries[0].EndLap);
        Assert.Equal(StintSummarySource.SessionHistory, result.RaceReport.StintSummaries[0].Source);
        Assert.Equal(RaceAnalysisConfidence.High, result.RaceReport.StintSummaries[0].Confidence);
        Assert.Equal(2, result.RaceReport.StintSummaries[1].StartLap);
        Assert.Equal(3, result.RaceReport.StintSummaries[1].EndLap);
        Assert.Contains("raw end lap 255", result.RaceReport.StintSummaries[1].Notes, StringComparison.OrdinalIgnoreCase);

        var pitStop = Assert.Single(result.RaceReport.PitStopSummaries);
        Assert.Equal(2, pitStop.PitLap);
        Assert.Equal("visual 17 / actual 16", pitStop.CompoundBefore);
        Assert.Equal("visual 18 / actual 18", pitStop.CompoundAfter);
        Assert.Equal(1, pitStop.TyreAgeBefore);
        Assert.Equal(0, pitStop.TyreAgeAfter);
        Assert.Equal(5, pitStop.PositionBefore);
        Assert.Equal(7, pitStop.PositionAfter);
        Assert.Equal(2, pitStop.PositionLost);
        Assert.Null(pitStop.EstimatedPitLossInMs);
        Assert.Equal(RaceAnalysisConfidence.High, pitStop.Confidence);

        var markdown = await File.ReadAllTextAsync(outputPath);
        Assert.Contains("## Stint Summaries", markdown);
        Assert.Contains("## Pit Stop Summary", markdown);
        Assert.Contains("SessionHistory", markdown);
        Assert.Contains("High", markdown);
        Assert.Contains("unavailable", markdown);
        Assert.DoesNotContain("payloadBase64", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CarTelemetry[]", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Motion[]", markdown, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnalyzeAsync_WritesRaceTrendSummariesWithKgFuelAndErsLapCounts()
    {
        var inputPath = CreateTempJsonlPath();
        var outputPath = Path.ChangeExtension(inputPath, ".md");
        var lines = new[]
        {
            BuildRecord(BuildSessionPacket(1001UL, trackId: 9, sessionType: 15, totalLaps: 3)),
            BuildRecord(BuildLapDataPacket(1001UL, currentLap: 1, totalDistance: 100f, position: 5, resultStatus: 2, numPitStops: 0, lastLapTimeInMs: 90000)),
            BuildRecord(BuildCarStatusPacket(1001UL, fuelInTank: 30f, actualCompound: 16, visualCompound: 17, tyreAgeLaps: 1, fuelRemainingLaps: 3.5f, ersStoreEnergy: 2_500_000f, ersDeployMode: 1, ersHarvestedThisLapMguk: 100_000f, ersHarvestedThisLapMguh: 50_000f, ersDeployedThisLap: 700_000f)),
            BuildRecord(BuildCarDamagePacket(1001UL, tyreWear: 10f)),
            BuildRecord(BuildLapDataPacket(1001UL, currentLap: 2, totalDistance: 200f, position: 5, resultStatus: 2, numPitStops: 0, lastLapTimeInMs: 91000)),
            BuildRecord(BuildCarStatusPacket(1001UL, fuelInTank: 27f, actualCompound: 16, visualCompound: 17, tyreAgeLaps: 2, fuelRemainingLaps: 2.2f, ersStoreEnergy: 400_000f, ersDeployMode: 3, ersHarvestedThisLapMguk: 80_000f, ersHarvestedThisLapMguh: 40_000f, ersDeployedThisLap: 900_000f)),
            BuildRecord(BuildCarDamagePacket(1001UL, tyreWear: 55f)),
            BuildRecord(BuildLapDataPacket(1001UL, currentLap: 3, totalDistance: 300f, position: 5, resultStatus: 3, numPitStops: 0, lastLapTimeInMs: 92000)),
            BuildRecord(BuildCarStatusPacket(1001UL, fuelInTank: 24f, actualCompound: 16, visualCompound: 17, tyreAgeLaps: 3, fuelRemainingLaps: 1.2f, ersStoreEnergy: 1_200_000f, ersDeployMode: 2, ersHarvestedThisLapMguk: 400_000f, ersHarvestedThisLapMguh: 200_000f, ersDeployedThisLap: 200_000f)),
            BuildRecord(BuildCarDamagePacket(1001UL, tyreWear: 70f)),
            BuildRecord(BuildSessionHistoryPacket(
                1001UL,
                numLaps: 3,
                firstLapTimeInMs: 90000,
                lapTimesInMs: [90000U, 91000U, 92000U],
                tyreStints:
                [
                    new TyreStintTestData(EndLap: 3, Actual: 16, Visual: 17)
                ])),
            BuildRecord(BuildFinalClassificationPacket(1001UL, position: 5, numLaps: 3, gridPosition: 5, points: 10, bestLapTimeInMs: 90000, penaltiesTime: 0, numPenalties: 0))
        };
        await File.WriteAllLinesAsync(inputPath, lines);
        var analyzer = new RawLogAnalyzerService();

        var result = await analyzer.AnalyzeAsync(new RawLogAnalyzerOptions(inputPath, outputPath, 1001UL));

        Assert.NotNull(result.RaceReport);
        var tyreUsage = Assert.Single(result.RaceReport.TyreUsageSummaries);
        Assert.Equal(1, tyreUsage.StintIndex);
        Assert.Equal(10f, tyreUsage.StartWearPercent);
        Assert.Equal(70f, tyreUsage.EndWearPercent);
        Assert.Equal(70f, tyreUsage.MaxWearPercent);
        Assert.Equal(60f, tyreUsage.WearDeltaPercent);
        Assert.Equal(20f, tyreUsage.AverageWearPerLapPercent);
        Assert.Equal(3, tyreUsage.ObservedLapCount);
        Assert.Equal(RaceTrendRisk.High, tyreUsage.Risk);
        Assert.Equal(RaceAnalysisConfidence.High, tyreUsage.Confidence);

        var fuel = result.RaceReport.FuelTrendSummary;
        Assert.Equal(30f, fuel.StartFuelKg);
        Assert.Equal(24f, fuel.EndFuelKg);
        Assert.Equal(24f, fuel.MinFuelKg);
        Assert.Equal(30f, fuel.MaxFuelKg);
        Assert.Equal(6f, fuel.FuelUsedKg);
        Assert.Equal(2f, fuel.AverageFuelPerLapKg);
        Assert.Equal(1.2f, fuel.MinFuelRemainingLaps);
        Assert.Equal(RaceTrendRisk.Medium, fuel.Risk);
        Assert.Equal(RaceAnalysisConfidence.High, fuel.Confidence);

        var ers = result.RaceReport.ErsTrendSummary;
        Assert.Equal(2.5f, ers.StartStoreEnergyMJ);
        Assert.Equal(1.2f, ers.EndStoreEnergyMJ);
        Assert.Equal(0.4f, ers.MinStoreEnergyMJ);
        Assert.Equal(2.5f, ers.MaxStoreEnergyMJ);
        Assert.Equal(-1.3f, ers.NetStoreEnergyDeltaMJ);
        Assert.Equal(1, ers.LowErsLapCount);
        Assert.Equal(2, ers.HighUsageLaps);
        Assert.Equal(1, ers.RecoveryLaps);
        Assert.Equal(RaceTrendRisk.High, ers.Risk);

        var markdown = await File.ReadAllTextAsync(outputPath);
        Assert.Contains("## Tyre Usage Summary", markdown);
        Assert.Contains("## Fuel Trend Summary", markdown);
        Assert.Contains("Start fuel kg", markdown);
        Assert.DoesNotContain("liters", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("litres", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("## ERS Trend Summary", markdown);
        Assert.Contains("Low ERS lap count: 1", markdown);
        Assert.Contains("High usage laps: 2", markdown);
        Assert.Contains("Recovery laps: 1", markdown);
        Assert.DoesNotContain("payloadBase64", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CarTelemetry[]", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Motion[]", markdown, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnalyzeAsync_UsesCarDamageTyreWearBeforeTyreSetsWhenSourcesDisagree()
    {
        var inputPath = CreateTempJsonlPath();
        var outputPath = Path.ChangeExtension(inputPath, ".md");
        var lines = new[]
        {
            BuildRecord(BuildSessionPacket(1001UL, trackId: 9, sessionType: 15, totalLaps: 2)),
            BuildRecord(BuildLapDataPacket(1001UL, currentLap: 1, totalDistance: 100f, position: 5, resultStatus: 2, lastLapTimeInMs: 90000)),
            BuildRecord(BuildCarStatusPacket(1001UL, fuelInTank: 20f, actualCompound: 16, visualCompound: 17, tyreAgeLaps: 1)),
            BuildRecord(BuildCarDamagePacket(1001UL, tyreWear: 20f)),
            BuildRecord(BuildTyreSetsPacket(1001UL, actualCompound: 16, visualCompound: 17, wear: 44)),
            BuildRecord(BuildLapDataPacket(1001UL, currentLap: 2, totalDistance: 200f, position: 5, resultStatus: 3, lastLapTimeInMs: 91000)),
            BuildRecord(BuildCarStatusPacket(1001UL, fuelInTank: 18f, actualCompound: 16, visualCompound: 17, tyreAgeLaps: 2)),
            BuildRecord(BuildCarDamagePacket(1001UL, tyreWear: 30f)),
            BuildRecord(BuildTyreSetsPacket(1001UL, actualCompound: 16, visualCompound: 17, wear: 60)),
            BuildRecord(BuildSessionHistoryPacket(
                1001UL,
                numLaps: 2,
                firstLapTimeInMs: 90000,
                lapTimesInMs: [90000U, 91000U],
                tyreStints:
                [
                    new TyreStintTestData(EndLap: 2, Actual: 16, Visual: 17)
                ])),
            BuildRecord(BuildFinalClassificationPacket(1001UL, position: 5, numLaps: 2, gridPosition: 5, points: 10, bestLapTimeInMs: 90000, penaltiesTime: 0, numPenalties: 0))
        };
        await File.WriteAllLinesAsync(inputPath, lines);
        var analyzer = new RawLogAnalyzerService();

        var result = await analyzer.AnalyzeAsync(new RawLogAnalyzerOptions(inputPath, outputPath, 1001UL));

        Assert.NotNull(result.RaceReport);
        var tyreUsage = Assert.Single(result.RaceReport.TyreUsageSummaries);
        Assert.Equal(20f, tyreUsage.StartWearPercent);
        Assert.Equal(30f, tyreUsage.EndWearPercent);
        Assert.Equal(30f, tyreUsage.MaxWearPercent);
        Assert.Contains("TyreSets", tyreUsage.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CarDamage", tyreUsage.Notes, StringComparison.OrdinalIgnoreCase);

        var markdown = await File.ReadAllTextAsync(outputPath);
        Assert.Contains("TyreSets", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CarDamage", markdown, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnalyzeAsync_WritesUnavailableTrendSummariesWhenTrendPacketsAreMissing()
    {
        var inputPath = CreateTempJsonlPath();
        var outputPath = Path.ChangeExtension(inputPath, ".md");
        var lines = new[]
        {
            BuildRecord(BuildSessionPacket(1001UL, trackId: 9, sessionType: 15, totalLaps: 1)),
            BuildRecord(BuildLapDataPacket(1001UL, currentLap: 1, totalDistance: 100f, position: 5, resultStatus: 3, lastLapTimeInMs: 90000)),
            BuildRecord(BuildSessionHistoryPacket(
                1001UL,
                numLaps: 1,
                firstLapTimeInMs: 90000,
                lapTimesInMs: [90000U],
                tyreStints:
                [
                    new TyreStintTestData(EndLap: 1, Actual: 16, Visual: 17)
                ])),
            BuildRecord(BuildFinalClassificationPacket(1001UL, position: 5, numLaps: 1, gridPosition: 5, points: 10, bestLapTimeInMs: 90000, penaltiesTime: 0, numPenalties: 0))
        };
        await File.WriteAllLinesAsync(inputPath, lines);
        var analyzer = new RawLogAnalyzerService();

        var result = await analyzer.AnalyzeAsync(new RawLogAnalyzerOptions(inputPath, outputPath, 1001UL));

        Assert.NotNull(result.RaceReport);
        Assert.Equal(RaceTrendRisk.Unknown, result.RaceReport.FuelTrendSummary.Risk);
        Assert.Equal(RaceTrendRisk.Unknown, result.RaceReport.ErsTrendSummary.Risk);
        Assert.Equal(RaceTrendRisk.Unknown, Assert.Single(result.RaceReport.TyreUsageSummaries).Risk);

        var markdown = await File.ReadAllTextAsync(outputPath);
        Assert.Contains("unavailable", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("payloadBase64", markdown, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnalyzeAsync_WritesGapTrendSummaryWithMsStorageAndSecondMarkdownDisplay()
    {
        var inputPath = CreateTempJsonlPath();
        var outputPath = Path.ChangeExtension(inputPath, ".md");
        var lines = new[]
        {
            BuildRecord(BuildSessionPacket(1001UL, trackId: 9, sessionType: 15, totalLaps: 3)),
            BuildRecord(BuildLapDataPacketWithCars(
                1001UL,
                new LapDataCarTestData(CarIndex: 0, CurrentLap: 1, Position: 3, DeltaToCarInFrontInMs: 900, TotalDistance: 100f),
                new LapDataCarTestData(CarIndex: 1, CurrentLap: 1, Position: 4, DeltaToCarInFrontInMs: 700, TotalDistance: 98f))),
            BuildRecord(BuildLapDataPacketWithCars(
                1001UL,
                new LapDataCarTestData(CarIndex: 0, CurrentLap: 2, Position: 4, DeltaToCarInFrontInMs: 1400, TotalDistance: 200f),
                new LapDataCarTestData(CarIndex: 1, CurrentLap: 2, Position: 5, DeltaToCarInFrontInMs: 1800, TotalDistance: 198f))),
            BuildRecord(BuildLapDataPacketWithCars(
                1001UL,
                new LapDataCarTestData(CarIndex: 0, CurrentLap: 3, Position: 2, DeltaToCarInFrontInMs: 2500, TotalDistance: 300f, ResultStatus: 3),
                new LapDataCarTestData(CarIndex: 1, CurrentLap: 3, Position: 3, DeltaToCarInFrontInMs: 800, TotalDistance: 298f, ResultStatus: 3))),
            BuildRecord(BuildFinalClassificationPacket(1001UL, position: 2, numLaps: 3, gridPosition: 4, points: 18, bestLapTimeInMs: 90000, penaltiesTime: 0, numPenalties: 0))
        };
        await File.WriteAllLinesAsync(inputPath, lines);
        var analyzer = new RawLogAnalyzerService();

        var result = await analyzer.AnalyzeAsync(new RawLogAnalyzerOptions(inputPath, outputPath, 1001UL));

        Assert.NotNull(result.RaceReport);
        var summary = result.RaceReport.GapTrendSummary;
        Assert.Equal(3, summary.ObservedLapCount);
        Assert.Equal(1, summary.AttackWindowLapCount);
        Assert.Equal(2, summary.DefenseWindowLapCount);
        Assert.Equal(3, summary.TrafficImpactLapCount);
        Assert.Equal(900U, summary.MinGapFrontMs);
        Assert.Equal(700U, summary.MinGapBehindMs);
        Assert.Equal(GapAnalysisConfidence.High, summary.Confidence);
        var attack = Assert.Single(summary.AttackWindows);
        Assert.Equal(1, attack.StartLap);
        Assert.Equal(1, attack.EndLap);
        Assert.Equal(900U, attack.MinGapFrontMs);
        Assert.Contains(summary.DefenseWindows, window => window.StartLap == 1 && window.MinGapBehindMs == 700U);
        Assert.Contains(summary.DefenseWindows, window => window.StartLap == 3 && window.MinGapBehindMs == 800U);
        Assert.Contains(summary.TrafficImpactLaps, lap => lap.LapNumber == 2 && lap.GapFrontMs == 1400U && lap.ImpactType == TrafficImpactType.FrontTraffic);
        Assert.NotNull(typeof(TrafficImpactLapSummary).GetProperty(nameof(TrafficImpactLapSummary.GapFrontMs)));
        Assert.NotNull(typeof(TrafficImpactLapSummary).GetProperty(nameof(TrafficImpactLapSummary.GapBehindMs)));
        Assert.Null(typeof(TrafficImpactLapSummary).GetProperty("GapFrontSeconds"));
        Assert.Null(typeof(TrafficImpactLapSummary).GetProperty("GapBehindSeconds"));

        var markdown = await File.ReadAllTextAsync(outputPath);
        Assert.Contains("## Gap Trend Summary", markdown);
        Assert.Contains("0.9 s", markdown);
        Assert.Contains("1.4 s", markdown);
        Assert.DoesNotContain("payloadBase64", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CarTelemetry[]", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Motion[]", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("LapPositions[]", markdown, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnalyzeAsync_LeavesBehindGapUnavailableWhenAdjacentRearCarIsOffLap()
    {
        var inputPath = CreateTempJsonlPath();
        var outputPath = Path.ChangeExtension(inputPath, ".md");
        var lines = new[]
        {
            BuildRecord(BuildSessionPacket(1001UL, trackId: 9, sessionType: 15, totalLaps: 1)),
            BuildRecord(BuildLapDataPacketWithCars(
                1001UL,
                new LapDataCarTestData(CarIndex: 0, CurrentLap: 2, Position: 5, DeltaToCarInFrontInMs: 800, TotalDistance: 200f, ResultStatus: 3),
                new LapDataCarTestData(CarIndex: 1, CurrentLap: 1, Position: 6, DeltaToCarInFrontInMs: 500, TotalDistance: 150f, ResultStatus: 2))),
            BuildRecord(BuildFinalClassificationPacket(1001UL, position: 5, numLaps: 2, gridPosition: 5, points: 10, bestLapTimeInMs: 90000, penaltiesTime: 0, numPenalties: 0))
        };
        await File.WriteAllLinesAsync(inputPath, lines);
        var analyzer = new RawLogAnalyzerService();

        var result = await analyzer.AnalyzeAsync(new RawLogAnalyzerOptions(inputPath, outputPath, 1001UL));

        Assert.NotNull(result.RaceReport);
        var summary = result.RaceReport.GapTrendSummary;
        Assert.Empty(summary.DefenseWindows);
        Assert.Equal(0, summary.DefenseWindowLapCount);
        Assert.Equal(GapAnalysisConfidence.Medium, summary.Confidence);
        var trafficLap = Assert.Single(summary.TrafficImpactLaps);
        Assert.Equal(800U, trafficLap.GapFrontMs);
        Assert.Null(trafficLap.GapBehindMs);
        Assert.Contains("same lap", trafficLap.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("same lap", summary.Notes, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnalyzeAsync_TreatsLapPositionsAsOrderingOnlyWithoutEstimatingGapTime()
    {
        var inputPath = CreateTempJsonlPath();
        var outputPath = Path.ChangeExtension(inputPath, ".md");
        var lines = new[]
        {
            BuildRecord(BuildSessionPacket(1001UL, trackId: 9, sessionType: 15, totalLaps: 1)),
            BuildRecord(BuildLapPositionsPacket(
                1001UL,
                numLaps: 1,
                lapStart: 1,
                new LapPositionTestData(LapNumber: 1, CarIndex: 0, Position: 3),
                new LapPositionTestData(LapNumber: 1, CarIndex: 1, Position: 4))),
            BuildRecord(BuildFinalClassificationPacket(1001UL, position: 3, numLaps: 1, gridPosition: 3, points: 15, bestLapTimeInMs: 90000, penaltiesTime: 0, numPenalties: 0))
        };
        await File.WriteAllLinesAsync(inputPath, lines);
        var analyzer = new RawLogAnalyzerService();

        var result = await analyzer.AnalyzeAsync(new RawLogAnalyzerOptions(inputPath, outputPath, 1001UL));

        Assert.NotNull(result.RaceReport);
        var summary = result.RaceReport.GapTrendSummary;
        Assert.Equal(1, summary.ObservedLapCount);
        Assert.Null(summary.MinGapFrontMs);
        Assert.Null(summary.MinGapBehindMs);
        Assert.Empty(summary.AttackWindows);
        Assert.Empty(summary.DefenseWindows);
        Assert.Empty(summary.TrafficImpactLaps);
        Assert.Equal(GapAnalysisConfidence.Low, summary.Confidence);
        Assert.Contains("position", summary.Notes, StringComparison.OrdinalIgnoreCase);

        var markdown = await File.ReadAllTextAsync(outputPath);
        Assert.Contains("unavailable", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PositionForVehicleIndexByLap", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("LapPositions[]", markdown, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnalyzeAsync_DoesNotEstimateGapTimingFromLapDistanceOrTotalDistance()
    {
        var inputPath = CreateTempJsonlPath();
        var outputPath = Path.ChangeExtension(inputPath, ".md");
        var lines = new[]
        {
            BuildRecord(BuildSessionPacket(1001UL, trackId: 9, sessionType: 15, totalLaps: 1)),
            BuildRecord(BuildLapDataPacketWithCars(
                1001UL,
                new LapDataCarTestData(CarIndex: 0, CurrentLap: 1, Position: 2, DeltaToCarInFrontInMs: 0, LapDistance: 4900f, TotalDistance: 10000f, ResultStatus: 3),
                new LapDataCarTestData(CarIndex: 1, CurrentLap: 1, Position: 3, DeltaToCarInFrontInMs: 0, LapDistance: 4895f, TotalDistance: 9995f, ResultStatus: 3))),
            BuildRecord(BuildFinalClassificationPacket(1001UL, position: 2, numLaps: 1, gridPosition: 2, points: 18, bestLapTimeInMs: 90000, penaltiesTime: 0, numPenalties: 0))
        };
        await File.WriteAllLinesAsync(inputPath, lines);
        var analyzer = new RawLogAnalyzerService();

        var result = await analyzer.AnalyzeAsync(new RawLogAnalyzerOptions(inputPath, outputPath, 1001UL));

        Assert.NotNull(result.RaceReport);
        var summary = result.RaceReport.GapTrendSummary;
        Assert.Equal(1, summary.ObservedLapCount);
        Assert.Null(summary.MinGapFrontMs);
        Assert.Null(summary.MinGapBehindMs);
        Assert.Empty(summary.AttackWindows);
        Assert.Empty(summary.DefenseWindows);
        Assert.Empty(summary.TrafficImpactLaps);
        Assert.Equal(GapAnalysisConfidence.Low, summary.Confidence);
        Assert.Contains("no time gap", summary.Notes, StringComparison.OrdinalIgnoreCase);

        var markdown = await File.ReadAllTextAsync(outputPath);
        Assert.Contains("unavailable", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("0.005 s", markdown, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnalyzeAsync_WritesUnknownGapSummaryWhenNoGapEvidenceExists()
    {
        var inputPath = CreateTempJsonlPath();
        var outputPath = Path.ChangeExtension(inputPath, ".md");
        var lines = new[]
        {
            BuildRecord(BuildSessionPacket(1001UL, trackId: 9, sessionType: 15, totalLaps: 1)),
            BuildRecord(BuildFinalClassificationPacket(1001UL, position: 1, numLaps: 1, gridPosition: 1, points: 25, bestLapTimeInMs: 90000, penaltiesTime: 0, numPenalties: 0))
        };
        await File.WriteAllLinesAsync(inputPath, lines);
        var analyzer = new RawLogAnalyzerService();

        var result = await analyzer.AnalyzeAsync(new RawLogAnalyzerOptions(inputPath, outputPath, 1001UL));

        Assert.NotNull(result.RaceReport);
        var summary = result.RaceReport.GapTrendSummary;
        Assert.Equal(0, summary.ObservedLapCount);
        Assert.Equal(GapAnalysisConfidence.Unknown, summary.Confidence);
        Assert.Null(summary.MinGapFrontMs);
        Assert.Null(summary.MinGapBehindMs);
        Assert.Empty(summary.AttackWindows);
        Assert.Empty(summary.DefenseWindows);
        Assert.Empty(summary.TrafficImpactLaps);
    }

    [Fact]
    public async Task AnalyzeAsync_WritesRaceEventTimelineFromKeyUdpAndDerivedEvents()
    {
        var inputPath = CreateTempJsonlPath();
        var outputPath = Path.ChangeExtension(inputPath, ".md");
        var lines = new[]
        {
            BuildRecord(BuildSessionPacket(1001UL, trackId: 9, sessionType: 15, totalLaps: 2), timestampUtc: "2026-04-28T10:00:00.0000000Z"),
            BuildRecord(BuildEventPacket(1001UL, "SSTA"), timestampUtc: "2026-04-28T10:00:01.0000000Z"),
            BuildRecord(BuildLapDataPacketWithCars(
                1001UL,
                new LapDataCarTestData(CarIndex: 0, CurrentLap: 1, Position: 5, DeltaToCarInFrontInMs: 900, TotalDistance: 100f),
                new LapDataCarTestData(CarIndex: 1, CurrentLap: 1, Position: 6, DeltaToCarInFrontInMs: 700, TotalDistance: 98f)),
                timestampUtc: "2026-04-28T10:00:02.0000000Z"),
            BuildRecord(BuildCarStatusPacket(1001UL, fuelInTank: 20f, actualCompound: 16, visualCompound: 17, tyreAgeLaps: 1, fuelRemainingLaps: 2.5f, ersStoreEnergy: 2_000_000f)),
            BuildRecord(BuildEventPacket(1001UL, "OVTK", [0, 2]), timestampUtc: "2026-04-28T10:00:04.0000000Z"),
            BuildRecord(BuildEventPacket(1001UL, "OVTK", [3, 0]), timestampUtc: "2026-04-28T10:00:05.0000000Z"),
            BuildRecord(BuildEventPacket(1001UL, "PENA", [1, 2, 0, 4, 5, 2, 0]), timestampUtc: "2026-04-28T10:00:06.0000000Z"),
            BuildRecord(BuildEventPacket(1001UL, "SCAR", [1, 0]), timestampUtc: "2026-04-28T10:00:07.0000000Z"),
            BuildRecord(BuildEventPacket(1001UL, "RDFL"), timestampUtc: "2026-04-28T10:00:08.0000000Z"),
            BuildRecord(BuildLapDataPacketWithCars(
                1001UL,
                new LapDataCarTestData(CarIndex: 0, CurrentLap: 2, Position: 8, DeltaToCarInFrontInMs: 800, TotalDistance: 200f, ResultStatus: 3, PitStatus: 1, NumPitStops: 1, IsCurrentLapInvalid: true),
                new LapDataCarTestData(CarIndex: 1, CurrentLap: 2, Position: 9, DeltaToCarInFrontInMs: 700, TotalDistance: 198f, ResultStatus: 3)),
                timestampUtc: "2026-04-28T10:00:09.0000000Z"),
            BuildRecord(BuildCarStatusPacket(1001UL, fuelInTank: 18f, actualCompound: 18, visualCompound: 18, tyreAgeLaps: 0, fuelRemainingLaps: 0.4f, ersStoreEnergy: 400_000f, ersDeployedThisLap: 900_000f)),
            BuildRecord(BuildCarDamagePacket(1001UL, tyreWear: 75f)),
            BuildRecord(BuildEventPacket(1001UL, "BUTN", [1, 0, 0, 0])),
            BuildRecord(BuildEventPacket(1001UL, "SPTP")),
            BuildRecord(BuildEventPacket(1001UL, "SEND")),
            BuildRecord(BuildSessionHistoryPacket(
                1001UL,
                numLaps: 2,
                firstLapTimeInMs: 90000,
                lapTimesInMs: [90000U, 91000U],
                tyreStints:
                [
                    new TyreStintTestData(EndLap: 1, Actual: 16, Visual: 17),
                    new TyreStintTestData(EndLap: 2, Actual: 18, Visual: 18)
                ])),
            BuildRecord(BuildFinalClassificationPacket(1001UL, position: 8, numLaps: 2, gridPosition: 5, points: 4, bestLapTimeInMs: 90000, penaltiesTime: 5, numPenalties: 1, numPitStops: 1))
        };
        await File.WriteAllLinesAsync(inputPath, lines);
        var analyzer = new RawLogAnalyzerService();

        var result = await analyzer.AnalyzeAsync(new RawLogAnalyzerOptions(inputPath, outputPath, 1001UL));

        Assert.NotNull(result.RaceReport);
        var timeline = result.RaceReport.RaceEventTimeline;
        Assert.Contains(timeline, entry => entry.EventType == RaceEventTimelineType.Start && entry.Lap == 0);
        Assert.Contains(timeline, entry => entry.EventType == RaceEventTimelineType.Overtake && entry.RelatedVehicleIndex == 2);
        Assert.Contains(timeline, entry => entry.EventType == RaceEventTimelineType.PositionLost && entry.RelatedVehicleIndex == 3);
        Assert.Contains(timeline, entry => entry.EventType == RaceEventTimelineType.Penalty && entry.Lap == 2);
        Assert.Contains(timeline, entry => entry.EventType == RaceEventTimelineType.SafetyCar && entry.Source == RaceEventTimelineSource.UdpEvent);
        Assert.Contains(timeline, entry => entry.EventType == RaceEventTimelineType.RedFlag && entry.Source == RaceEventTimelineSource.UdpEvent);
        Assert.Contains(timeline, entry => entry.EventType == RaceEventTimelineType.PitStop && entry.Lap == 2);
        Assert.Contains(timeline, entry => entry.EventType == RaceEventTimelineType.TyreChange && entry.Lap == 2);
        Assert.Contains(timeline, entry => entry.EventType == RaceEventTimelineType.LowFuel && entry.Lap == 2);
        Assert.Contains(timeline, entry => entry.EventType == RaceEventTimelineType.HighTyreWear && entry.Lap == 2);
        Assert.Contains(timeline, entry => entry.EventType == RaceEventTimelineType.LowErs && entry.Lap == 2);
        Assert.Contains(timeline, entry => entry.EventType == RaceEventTimelineType.InvalidLap && entry.Lap == 2);
        Assert.Contains(timeline, entry => entry.EventType == RaceEventTimelineType.FinalClassification && entry.Lap == 3);
        Assert.DoesNotContain(timeline, entry => entry.Message.Contains("BUTN", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(timeline, entry => entry.Message.Contains("SPTP", StringComparison.OrdinalIgnoreCase));
        Assert.True(timeline.SequenceEqual(timeline.OrderBy(entry => entry.Lap).ThenBy(entry => entry.TimestampUtc)));

        var markdown = await File.ReadAllTextAsync(outputPath);
        Assert.Contains("## Race Event Timeline", markdown);
        Assert.DoesNotContain("payloadBase64", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CarTelemetry[]", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Motion[]", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("LapPositions[]", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MarshalZones[]", markdown, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnalyzeAsync_FiltersUnrelatedOvertakesFromRaceEventTimeline()
    {
        var inputPath = CreateTempJsonlPath();
        var outputPath = Path.ChangeExtension(inputPath, ".md");
        var lines = new[]
        {
            BuildRecord(BuildSessionPacket(1001UL, trackId: 9, sessionType: 15, totalLaps: 1)),
            BuildRecord(BuildLapDataPacket(1001UL, currentLap: 1, totalDistance: 100f, position: 5, resultStatus: 3)),
            BuildRecord(BuildEventPacket(1001UL, "OVTK", [2, 3])),
            BuildRecord(BuildFinalClassificationPacket(1001UL, position: 5, numLaps: 1, gridPosition: 5, points: 10, bestLapTimeInMs: 90000, penaltiesTime: 0, numPenalties: 0))
        };
        await File.WriteAllLinesAsync(inputPath, lines);
        var analyzer = new RawLogAnalyzerService();

        var result = await analyzer.AnalyzeAsync(new RawLogAnalyzerOptions(inputPath, outputPath, 1001UL));

        Assert.NotNull(result.RaceReport);
        Assert.Equal(1, result.Sessions[1001UL].EventCodeCounts["OVTK"]);
        Assert.DoesNotContain(result.RaceReport.RaceEventTimeline, entry => entry.EventType is RaceEventTimelineType.Overtake or RaceEventTimelineType.PositionLost);
    }

    [Fact]
    public async Task AnalyzeAsync_AllowsExplicitNonRaceSessionWithDebugTimelineWarning()
    {
        var inputPath = CreateTempJsonlPath();
        var outputPath = Path.ChangeExtension(inputPath, ".md");
        var lines = new[]
        {
            BuildRecord(BuildSessionPacket(1001UL, trackId: 9, sessionType: 12, totalLaps: 1)),
            BuildRecord(BuildEventPacket(1001UL, "SSTA")),
            BuildRecord(BuildLapDataPacket(1001UL, currentLap: 1, totalDistance: 100f, position: 5, resultStatus: 3))
        };
        await File.WriteAllLinesAsync(inputPath, lines);
        var analyzer = new RawLogAnalyzerService();

        var result = await analyzer.AnalyzeAsync(new RawLogAnalyzerOptions(inputPath, outputPath, 1001UL));

        Assert.NotNull(result.RaceReport);
        Assert.Equal(12, result.RaceReport.SessionSummary.SessionType);
        Assert.Contains("非正赛样本，事件线仅供调试", result.RaceReport.DataQualityWarnings);
        Assert.Contains(result.RaceReport.RaceEventTimeline, entry => entry.EventType == RaceEventTimelineType.Start);

        var markdown = await File.ReadAllTextAsync(outputPath);
        Assert.Contains("非正赛样本，事件线仅供调试", markdown);
    }

    [Fact]
    public async Task AnalyzeAsync_UsesSessionStatusChangesForSafetyEventsAndWarnsUnknownMarshalFlags()
    {
        var inputPath = CreateTempJsonlPath();
        var outputPath = Path.ChangeExtension(inputPath, ".md");
        var lines = new[]
        {
            BuildRecord(BuildSessionPacket(1001UL, trackId: 9, sessionType: 15, totalLaps: 1, safetyCarStatus: 0, marshalZoneFlags: [0])),
            BuildRecord(BuildLapDataPacket(1001UL, currentLap: 1, totalDistance: 100f, position: 5, resultStatus: 3)),
            BuildRecord(BuildSessionPacket(1001UL, trackId: 9, sessionType: 15, totalLaps: 1, safetyCarStatus: 1, marshalZoneFlags: [3])),
            BuildRecord(BuildSessionPacket(1001UL, trackId: 9, sessionType: 15, totalLaps: 1, safetyCarStatus: 1, marshalZoneFlags: [3])),
            BuildRecord(BuildSessionPacket(1001UL, trackId: 9, sessionType: 15, totalLaps: 1, safetyCarStatus: 2, marshalZoneFlags: [4])),
            BuildRecord(BuildSessionPacket(1001UL, trackId: 9, sessionType: 15, totalLaps: 1, safetyCarStatus: 9, marshalZoneFlags: [9])),
            BuildRecord(BuildFinalClassificationPacket(1001UL, position: 5, numLaps: 1, gridPosition: 5, points: 10, bestLapTimeInMs: 90000, penaltiesTime: 0, numPenalties: 0))
        };
        await File.WriteAllLinesAsync(inputPath, lines);
        var analyzer = new RawLogAnalyzerService();

        var result = await analyzer.AnalyzeAsync(new RawLogAnalyzerOptions(inputPath, outputPath, 1001UL));

        Assert.NotNull(result.RaceReport);
        Assert.Single(result.RaceReport.RaceEventTimeline, entry => entry.EventType == RaceEventTimelineType.SafetyCar && entry.Source == RaceEventTimelineSource.SessionStatus);
        Assert.Single(result.RaceReport.RaceEventTimeline, entry => entry.EventType == RaceEventTimelineType.VirtualSafetyCar);
        Assert.Single(result.RaceReport.RaceEventTimeline, entry => entry.EventType == RaceEventTimelineType.YellowFlag);
        Assert.Single(result.RaceReport.RaceEventTimeline, entry => entry.EventType == RaceEventTimelineType.RedFlag && entry.Source == RaceEventTimelineSource.SessionStatus);
        Assert.Contains(result.RaceReport.DataQualityWarnings, warning => warning.Contains("Unknown marshal zone flag", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.RaceReport.DataQualityWarnings, warning => warning.Contains("Unknown safety car status", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AnalyzeAsync_LabelsSlowLapOnlyPitInferenceAsPossibleLowConfidence()
    {
        var inputPath = CreateTempJsonlPath();
        var outputPath = Path.ChangeExtension(inputPath, ".md");
        var lines = new[]
        {
            BuildRecord(BuildSessionPacket(1001UL, trackId: 9, sessionType: 15, totalLaps: 3)),
            BuildRecord(BuildLapDataPacket(1001UL, currentLap: 1, totalDistance: 100f, position: 5, resultStatus: 2, numPitStops: 0, lastLapTimeInMs: 90000)),
            BuildRecord(BuildLapDataPacket(1001UL, currentLap: 2, totalDistance: 200f, position: 8, resultStatus: 2, numPitStops: 0, lastLapTimeInMs: 160000)),
            BuildRecord(BuildLapDataPacket(1001UL, currentLap: 3, totalDistance: 300f, position: 8, resultStatus: 2, numPitStops: 0, lastLapTimeInMs: 91000)),
            BuildRecord(BuildSessionHistoryPacket(1001UL, numLaps: 3, firstLapTimeInMs: 90000, lapTimesInMs: [90000U, 160000U, 91000U])),
            BuildRecord(BuildFinalClassificationPacket(1001UL, position: 8, numLaps: 3, gridPosition: 5, points: 0, bestLapTimeInMs: 90000, penaltiesTime: 0, numPenalties: 0))
        };
        await File.WriteAllLinesAsync(inputPath, lines);
        var analyzer = new RawLogAnalyzerService();

        var result = await analyzer.AnalyzeAsync(new RawLogAnalyzerOptions(inputPath, outputPath, 1001UL));

        Assert.NotNull(result.RaceReport);
        var pitStop = Assert.Single(result.RaceReport.PitStopSummaries);
        Assert.Equal(2, pitStop.PitLap);
        Assert.Equal(RaceAnalysisConfidence.Low, pitStop.Confidence);
        Assert.Null(pitStop.EstimatedPitLossInMs);
        Assert.Contains("possible", pitStop.Notes, StringComparison.OrdinalIgnoreCase);

        var markdown = await File.ReadAllTextAsync(outputPath);
        Assert.Contains("possible", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("confirmed from slow lap", markdown, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnalyzeAsync_AutoSelectsLargestRaceSessionAndIgnoresSessionZero()
    {
        var inputPath = CreateTempJsonlPath();
        var outputPath = Path.ChangeExtension(inputPath, ".md");
        var lines = new[]
        {
            BuildRecord(BuildSessionPacket(0UL, trackId: 1, sessionType: 15, totalLaps: 1)),
            BuildRecord(BuildSessionPacket(1001UL, trackId: 9, sessionType: 15, totalLaps: 70)),
            BuildRecord(BuildSessionPacket(2002UL, trackId: 10, sessionType: 15, totalLaps: 50)),
            BuildRecord(BuildLapDataPacket(2002UL, currentLap: 1, totalDistance: 300f, position: 12, resultStatus: 2)),
            BuildRecord(BuildLapDataPacket(2002UL, currentLap: 2, totalDistance: 600f, position: 11, resultStatus: 2)),
            BuildRecord(BuildFinalClassificationPacket(2002UL, position: 11, numLaps: 2, gridPosition: 12, points: 0, bestLapTimeInMs: 90000, penaltiesTime: 0, numPenalties: 0))
        };
        await File.WriteAllLinesAsync(inputPath, lines);
        var analyzer = new RawLogAnalyzerService();

        var result = await analyzer.AnalyzeAsync(new RawLogAnalyzerOptions(inputPath, outputPath));

        Assert.NotNull(result.RaceReport);
        Assert.Equal(2002UL, result.RaceReport.SessionUid);
        Assert.Equal(10, result.RaceReport.SessionSummary.TrackId);
        Assert.Equal(2, result.RaceReport.LapSummaries.Count);
        Assert.NotEqual(0UL, result.RaceReport.SessionSummary.SessionUid);
    }

    [Fact]
    public async Task AnalyzeAsync_ThrowsClearErrorWhenNoRaceSessionExists()
    {
        var inputPath = CreateTempJsonlPath();
        var outputPath = Path.ChangeExtension(inputPath, ".md");
        await File.WriteAllLinesAsync(
            inputPath,
            new[]
            {
                BuildRecord(BuildSessionPacket(1001UL, trackId: 12, sessionType: 10, totalLaps: 5)),
                BuildRecord(BuildLapDataPacket(1001UL, currentLap: 1, totalDistance: 100f))
            });
        var analyzer = new RawLogAnalyzerService();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => analyzer.AnalyzeAsync(new RawLogAnalyzerOptions(inputPath, outputPath)));

        Assert.Contains("No valid Race session", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(outputPath));
    }

    [Fact]
    public async Task ProgramMain_AcceptsSessionUidArgumentAndWritesRaceReport()
    {
        var inputPath = CreateTempJsonlPath();
        var outputPath = Path.ChangeExtension(inputPath, ".md");
        await File.WriteAllLinesAsync(
            inputPath,
            new[]
            {
                BuildRecord(BuildSessionPacket(1001UL, trackId: 9, sessionType: 15, totalLaps: 70)),
                BuildRecord(BuildLapDataPacket(1001UL, currentLap: 1, totalDistance: 100f)),
                BuildRecord(BuildFinalClassificationPacket(1001UL, position: 21, numLaps: 67, gridPosition: 8, points: 0, bestLapTimeInMs: 84289, penaltiesTime: 40, numPenalties: 4))
            });

        var exitCode = await Program.Main(["--input", inputPath, "--output", outputPath, "--session-uid", "1001"]);

        Assert.Equal(0, exitCode);
        var markdown = await File.ReadAllTextAsync(outputPath);
        Assert.Contains("# Race Analysis Report", markdown);
        Assert.Contains("SessionUid: 1001", markdown);
    }

    private static string CreateTempJsonlPath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "F1Telemetry.RawLogAnalyzer.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "synthetic.jsonl");
    }

    private static string BuildRecord(
        byte[] payload,
        int? declaredLength = null,
        string timestampUtc = "2026-04-28T10:00:00.0000000Z")
    {
        var record = new
        {
            timestampUtc,
            source = "127.0.0.1:20777",
            length = declaredLength ?? payload.Length,
            packetId = payload.Length > 6 ? payload[6] : (byte?)null,
            sessionUid = payload.Length >= PacketHeader.Size ? BinaryPrimitives.ReadUInt64LittleEndian(payload.AsSpan(7, sizeof(ulong))) : (ulong?)null,
            frameIdentifier = payload.Length >= PacketHeader.Size ? BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(19, sizeof(uint))) : (uint?)null,
            playerCarIndex = payload.Length > 27 ? payload[27] : (byte?)null,
            packetFormat = payload.Length >= 2 ? BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(0, sizeof(ushort))) : (ushort?)null,
            gameYear = payload.Length > 2 ? payload[2] : (byte?)null,
            packetVersion = payload.Length > 5 ? payload[5] : (byte?)null,
            payloadBase64 = Convert.ToBase64String(payload)
        };

        return JsonSerializer.Serialize(record);
    }

    private static byte[] BuildSessionPacket(
        ulong sessionUid,
        sbyte trackId,
        byte sessionType,
        byte totalLaps,
        byte safetyCarStatus = 0,
        sbyte[]? marshalZoneFlags = null)
    {
        return BuildPacket(sessionUid, PacketId.Session, UdpPacketConstants.SessionBodySize, body =>
        {
            var offset = 0;
            ProtocolTestData.WriteByte(body, ref offset, 1);
            ProtocolTestData.WriteSByte(body, ref offset, 28);
            ProtocolTestData.WriteSByte(body, ref offset, 21);
            ProtocolTestData.WriteByte(body, ref offset, totalLaps);
            ProtocolTestData.WriteUInt16(body, ref offset, 5412);
            ProtocolTestData.WriteByte(body, ref offset, sessionType);
            ProtocolTestData.WriteSByte(body, ref offset, trackId);
            ProtocolTestData.WriteByte(body, ref offset, 0);
            ProtocolTestData.WriteUInt16(body, ref offset, 0);
            ProtocolTestData.WriteUInt16(body, ref offset, 0);
            ProtocolTestData.WriteByte(body, ref offset, 0);
            ProtocolTestData.WriteByte(body, ref offset, 0);
            ProtocolTestData.WriteByte(body, ref offset, 0);
            ProtocolTestData.WriteByte(body, ref offset, 0);
            ProtocolTestData.WriteByte(body, ref offset, 0);
            ProtocolTestData.WriteByte(body, ref offset, (byte)(marshalZoneFlags?.Length ?? 0));
            for (var index = 0; index < UdpPacketConstants.MaxMarshalZones; index++)
            {
                ProtocolTestData.WriteFloat(body, ref offset, index / 10f);
                ProtocolTestData.WriteSByte(
                    body,
                    ref offset,
                    marshalZoneFlags is not null && index < marshalZoneFlags.Length ? marshalZoneFlags[index] : (sbyte)0);
            }

            ProtocolTestData.WriteByte(body, ref offset, safetyCarStatus);
        });
    }

    private static byte[] BuildLapDataPacket(
        ulong sessionUid,
        byte currentLap,
        float totalDistance,
        byte position = 1,
        byte resultStatus = 2,
        byte pitStatus = 0,
        byte numPitStops = 0,
        bool isPitLaneTimerActive = false,
        ushort pitLaneTimeInLaneInMs = 0,
        ushort pitStopTimerInMs = 0,
        uint lastLapTimeInMs = 90000)
    {
        return BuildLapDataPacketWithCars(
            sessionUid,
            new LapDataCarTestData(
                CarIndex: 0,
                CurrentLap: currentLap,
                Position: position,
                DeltaToCarInFrontInMs: 250,
                TotalDistance: totalDistance,
                ResultStatus: resultStatus,
                PitStatus: pitStatus,
                NumPitStops: numPitStops,
                IsPitLaneTimerActive: isPitLaneTimerActive,
                PitLaneTimeInLaneInMs: pitLaneTimeInLaneInMs,
                PitStopTimerInMs: pitStopTimerInMs,
                IsCurrentLapInvalid: false,
                LastLapTimeInMs: lastLapTimeInMs));
    }

    private static byte[] BuildLapDataPacketWithCars(ulong sessionUid, params LapDataCarTestData[] cars)
    {
        return BuildPacket(sessionUid, PacketId.LapData, UdpPacketConstants.LapDataBodySize, body =>
        {
            foreach (var car in cars)
            {
                WriteLapDataEntry(body, car);
            }
        });
    }

    private static void WriteLapDataEntry(Span<byte> body, LapDataCarTestData car)
    {
        var offset = car.CarIndex * LapDataEntryTestSize;
        ProtocolTestData.WriteUInt32(body, ref offset, car.LastLapTimeInMs);
        ProtocolTestData.WriteUInt32(body, ref offset, 45000);
        ProtocolTestData.WriteUInt16(body, ref offset, 30000);
        ProtocolTestData.WriteByte(body, ref offset, 0);
        ProtocolTestData.WriteUInt16(body, ref offset, 31000);
        ProtocolTestData.WriteByte(body, ref offset, 0);
        ProtocolTestData.WriteUInt16(body, ref offset, car.DeltaToCarInFrontInMs);
        ProtocolTestData.WriteByte(body, ref offset, car.DeltaToCarInFrontMinutes);
        ProtocolTestData.WriteUInt16(body, ref offset, 1000);
        ProtocolTestData.WriteByte(body, ref offset, 0);
        ProtocolTestData.WriteFloat(body, ref offset, car.LapDistance);
        ProtocolTestData.WriteFloat(body, ref offset, car.TotalDistance);
        ProtocolTestData.WriteFloat(body, ref offset, 0);
        ProtocolTestData.WriteByte(body, ref offset, car.Position);
        ProtocolTestData.WriteByte(body, ref offset, car.CurrentLap);
        ProtocolTestData.WriteByte(body, ref offset, car.PitStatus);
        ProtocolTestData.WriteByte(body, ref offset, car.NumPitStops);
        ProtocolTestData.WriteByte(body, ref offset, 0);
        ProtocolTestData.WriteByte(body, ref offset, car.IsCurrentLapInvalid ? (byte)1 : (byte)0);
        ProtocolTestData.WriteByte(body, ref offset, 0);
        ProtocolTestData.WriteByte(body, ref offset, 0);
        ProtocolTestData.WriteByte(body, ref offset, 0);
        ProtocolTestData.WriteByte(body, ref offset, 0);
        ProtocolTestData.WriteByte(body, ref offset, 0);
        ProtocolTestData.WriteByte(body, ref offset, car.Position);
        ProtocolTestData.WriteByte(body, ref offset, 1);
        ProtocolTestData.WriteByte(body, ref offset, car.ResultStatus);
        ProtocolTestData.WriteByte(body, ref offset, car.IsPitLaneTimerActive ? (byte)1 : (byte)0);
        ProtocolTestData.WriteUInt16(body, ref offset, car.PitLaneTimeInLaneInMs);
        ProtocolTestData.WriteUInt16(body, ref offset, car.PitStopTimerInMs);
    }

    private static byte[] BuildLapPositionsPacket(
        ulong sessionUid,
        byte numLaps,
        byte lapStart,
        params LapPositionTestData[] positions)
    {
        return BuildPacket(sessionUid, PacketId.LapPositions, UdpPacketConstants.LapPositionsBodySize, body =>
        {
            var offset = 0;
            ProtocolTestData.WriteByte(body, ref offset, numLaps);
            ProtocolTestData.WriteByte(body, ref offset, lapStart);
            foreach (var position in positions)
            {
                var lapIndex = position.LapNumber - lapStart;
                if (lapIndex < 0 || lapIndex >= UdpPacketConstants.MaxLapPositionsLaps)
                {
                    continue;
                }

                var targetOffset = offset + (lapIndex * UdpPacketConstants.MaxCarsInSession) + position.CarIndex;
                body[targetOffset] = position.Position;
            }
        });
    }

    private static byte[] BuildSessionHistoryPacket(
        ulong sessionUid,
        byte numLaps,
        uint firstLapTimeInMs,
        uint[]? lapTimesInMs = null,
        TyreStintTestData[]? tyreStints = null)
    {
        return BuildPacket(sessionUid, PacketId.SessionHistory, UdpPacketConstants.SessionHistoryBodySize, body =>
        {
            var offset = 0;
            ProtocolTestData.WriteByte(body, ref offset, 0);
            ProtocolTestData.WriteByte(body, ref offset, numLaps);
            ProtocolTestData.WriteByte(body, ref offset, (byte)(tyreStints?.Length ?? 0));
            ProtocolTestData.WriteByte(body, ref offset, 1);
            ProtocolTestData.WriteByte(body, ref offset, 1);
            ProtocolTestData.WriteByte(body, ref offset, 1);
            ProtocolTestData.WriteByte(body, ref offset, 1);
            for (var index = 0; index < UdpPacketConstants.MaxSessionHistoryLaps; index++)
            {
                var lapTime = lapTimesInMs is not null && index < lapTimesInMs.Length
                    ? lapTimesInMs[index]
                    : index == 0 ? firstLapTimeInMs : 0U;
                ProtocolTestData.WriteUInt32(body, ref offset, lapTime);
                ProtocolTestData.WriteUInt16(body, ref offset, 30000);
                ProtocolTestData.WriteByte(body, ref offset, 0);
                ProtocolTestData.WriteUInt16(body, ref offset, 31000);
                ProtocolTestData.WriteByte(body, ref offset, 0);
                ProtocolTestData.WriteUInt16(body, ref offset, 23289);
                ProtocolTestData.WriteByte(body, ref offset, 0);
                ProtocolTestData.WriteByte(body, ref offset, lapTime > 0 ? (byte)0x0F : (byte)0);
            }

            for (var index = 0; index < UdpPacketConstants.MaxSessionHistoryTyreStints; index++)
            {
                if (tyreStints is not null && index < tyreStints.Length)
                {
                    ProtocolTestData.WriteByte(body, ref offset, tyreStints[index].EndLap);
                    ProtocolTestData.WriteByte(body, ref offset, tyreStints[index].Actual);
                    ProtocolTestData.WriteByte(body, ref offset, tyreStints[index].Visual);
                    continue;
                }

                ProtocolTestData.WriteByte(body, ref offset, 0);
                ProtocolTestData.WriteByte(body, ref offset, 0);
                ProtocolTestData.WriteByte(body, ref offset, 0);
            }
        });
    }

    private static byte[] BuildFinalClassificationPacket(
        ulong sessionUid,
        byte position,
        byte numLaps,
        byte gridPosition,
        byte points,
        uint bestLapTimeInMs,
        byte penaltiesTime,
        byte numPenalties,
        byte numPitStops = 0,
        byte[]? tyreStintsActual = null,
        byte[]? tyreStintsVisual = null,
        byte[]? tyreStintsEndLaps = null)
    {
        return BuildPacket(sessionUid, PacketId.FinalClassification, UdpPacketConstants.FinalClassificationBodySize, body =>
        {
            var offset = 0;
            ProtocolTestData.WriteByte(body, ref offset, 1);
            ProtocolTestData.WriteByte(body, ref offset, position);
            ProtocolTestData.WriteByte(body, ref offset, numLaps);
            ProtocolTestData.WriteByte(body, ref offset, gridPosition);
            ProtocolTestData.WriteByte(body, ref offset, points);
            ProtocolTestData.WriteByte(body, ref offset, numPitStops);
            ProtocolTestData.WriteByte(body, ref offset, 3);
            ProtocolTestData.WriteByte(body, ref offset, 0);
            ProtocolTestData.WriteUInt32(body, ref offset, bestLapTimeInMs);
            WriteDouble(body, ref offset, 5678.9d);
            ProtocolTestData.WriteByte(body, ref offset, penaltiesTime);
            ProtocolTestData.WriteByte(body, ref offset, numPenalties);
            ProtocolTestData.WriteByte(body, ref offset, (byte)(tyreStintsEndLaps?.Length ?? 0));
            WriteFixedBytes(body, ref offset, tyreStintsActual, UdpPacketConstants.MaxFinalClassificationTyreStints);
            WriteFixedBytes(body, ref offset, tyreStintsVisual, UdpPacketConstants.MaxFinalClassificationTyreStints);
            WriteFixedBytes(body, ref offset, tyreStintsEndLaps, UdpPacketConstants.MaxFinalClassificationTyreStints);
        });
    }

    private static byte[] BuildCarTelemetryPacket(ulong sessionUid, ushort speed, float throttle)
    {
        return BuildPacket(sessionUid, PacketId.CarTelemetry, UdpPacketConstants.CarTelemetryBodySize, body =>
        {
            var offset = 0;
            ProtocolTestData.WriteUInt16(body, ref offset, speed);
            ProtocolTestData.WriteFloat(body, ref offset, throttle);
        });
    }

    private static byte[] BuildCarStatusPacket(
        ulong sessionUid,
        float fuelInTank,
        byte actualCompound,
        byte visualCompound,
        byte tyreAgeLaps = 9,
        float fuelRemainingLaps = 0,
        float ersStoreEnergy = 0,
        byte ersDeployMode = 0,
        float ersHarvestedThisLapMguk = 0,
        float ersHarvestedThisLapMguh = 0,
        float ersDeployedThisLap = 0)
    {
        return BuildPacket(sessionUid, PacketId.CarStatus, UdpPacketConstants.CarStatusBodySize, body =>
        {
            var offset = 5;
            ProtocolTestData.WriteFloat(body, ref offset, fuelInTank);
            ProtocolTestData.WriteFloat(body, ref offset, 110f);
            ProtocolTestData.WriteFloat(body, ref offset, fuelRemainingLaps);
            offset = 25;
            ProtocolTestData.WriteByte(body, ref offset, actualCompound);
            ProtocolTestData.WriteByte(body, ref offset, visualCompound);
            ProtocolTestData.WriteByte(body, ref offset, tyreAgeLaps);
            offset = 37;
            ProtocolTestData.WriteFloat(body, ref offset, ersStoreEnergy);
            ProtocolTestData.WriteByte(body, ref offset, ersDeployMode);
            ProtocolTestData.WriteFloat(body, ref offset, ersHarvestedThisLapMguk);
            ProtocolTestData.WriteFloat(body, ref offset, ersHarvestedThisLapMguh);
            ProtocolTestData.WriteFloat(body, ref offset, ersDeployedThisLap);
        });
    }

    private static byte[] BuildCarDamagePacket(ulong sessionUid, float tyreWear)
    {
        return BuildPacket(sessionUid, PacketId.CarDamage, UdpPacketConstants.CarDamageBodySize, body =>
        {
            var offset = 0;
            ProtocolTestData.WriteFloat(body, ref offset, tyreWear);
        });
    }

    private static byte[] BuildTyreSetsPacket(ulong sessionUid, byte actualCompound, byte visualCompound, byte wear)
    {
        return BuildPacket(sessionUid, PacketId.TyreSets, UdpPacketConstants.TyreSetsBodySize, body =>
        {
            var offset = 0;
            ProtocolTestData.WriteByte(body, ref offset, 0);
            ProtocolTestData.WriteByte(body, ref offset, actualCompound);
            ProtocolTestData.WriteByte(body, ref offset, visualCompound);
            ProtocolTestData.WriteByte(body, ref offset, wear);
            ProtocolTestData.WriteByte(body, ref offset, 1);
            ProtocolTestData.WriteByte(body, ref offset, 0);
            ProtocolTestData.WriteByte(body, ref offset, 20);
            ProtocolTestData.WriteByte(body, ref offset, 18);
            ProtocolTestData.WriteInt16(body, ref offset, 0);
            ProtocolTestData.WriteByte(body, ref offset, 1);
            offset = 1 + (UdpPacketConstants.MaxTyreSets * 10);
            ProtocolTestData.WriteByte(body, ref offset, 0);
        });
    }

    private static byte[] BuildEventPacket(ulong sessionUid, string eventCode, byte[]? detail = null)
    {
        return BuildPacket(sessionUid, PacketId.Event, UdpPacketConstants.EventBodySize, body =>
        {
            Encoding.ASCII.GetBytes(eventCode, body[..4]);
            if (detail is not null)
            {
                detail.AsSpan(0, Math.Min(detail.Length, 12)).CopyTo(body[4..]);
            }
        });
    }

    private static byte[] BuildUnknownPacket(ulong sessionUid, byte packetId)
    {
        var payload = new byte[PacketHeader.Size];
        WriteHeader(payload, sessionUid, packetId);
        return payload;
    }

    private static byte[] BuildHeaderOnlyKnownPacket(ulong sessionUid, PacketId packetId)
    {
        var payload = new byte[PacketHeader.Size];
        WriteHeader(payload, sessionUid, (byte)packetId);
        return payload;
    }

    private static byte[] BuildPacket(ulong sessionUid, PacketId packetId, int bodySize, PacketBodyWriter writeBody)
    {
        var payload = new byte[PacketHeader.Size + bodySize];
        WriteHeader(payload.AsSpan(0, PacketHeader.Size), sessionUid, (byte)packetId);
        writeBody(payload.AsSpan(PacketHeader.Size, bodySize));
        return payload;
    }

    private static void WriteHeader(Span<byte> destination, ulong sessionUid, byte packetId)
    {
        ProtocolTestData.WriteHeader(destination, PacketId.Motion);
        destination[6] = packetId;
        BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(7, sizeof(ulong)), sessionUid);
        destination[27] = 0;
    }

    private static void WriteDouble(Span<byte> destination, ref int offset, double value)
    {
        BinaryPrimitives.WriteInt64LittleEndian(destination.Slice(offset, sizeof(double)), BitConverter.DoubleToInt64Bits(value));
        offset += sizeof(double);
    }

    private static void WriteFixedBytes(Span<byte> destination, ref int offset, byte[]? values, int count)
    {
        for (var index = 0; index < count; index++)
        {
            ProtocolTestData.WriteByte(
                destination,
                ref offset,
                values is not null && index < values.Length ? values[index] : (byte)0);
        }
    }

    private sealed record TyreStintTestData(byte EndLap, byte Actual, byte Visual);

    private sealed record LapDataCarTestData(
        byte CarIndex,
        byte CurrentLap,
        byte Position,
        ushort DeltaToCarInFrontInMs,
        byte DeltaToCarInFrontMinutes = 0,
        float LapDistance = 123.4f,
        float TotalDistance = 100f,
        byte ResultStatus = 2,
        byte PitStatus = 0,
        byte NumPitStops = 0,
        bool IsPitLaneTimerActive = false,
        ushort PitLaneTimeInLaneInMs = 0,
        ushort PitStopTimerInMs = 0,
        bool IsCurrentLapInvalid = false,
        uint LastLapTimeInMs = 90000);

    private sealed record LapPositionTestData(
        byte LapNumber,
        byte CarIndex,
        byte Position);
}
