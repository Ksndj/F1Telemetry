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

    private static string BuildRecord(byte[] payload, int? declaredLength = null)
    {
        var record = new
        {
            timestampUtc = "2026-04-28T10:00:00.0000000Z",
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

    private static byte[] BuildSessionPacket(ulong sessionUid, sbyte trackId, byte sessionType, byte totalLaps)
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
        return BuildPacket(sessionUid, PacketId.LapData, UdpPacketConstants.LapDataBodySize, body =>
        {
            var offset = 0;
            ProtocolTestData.WriteUInt32(body, ref offset, lastLapTimeInMs);
            ProtocolTestData.WriteUInt32(body, ref offset, 45000);
            ProtocolTestData.WriteUInt16(body, ref offset, 30000);
            ProtocolTestData.WriteByte(body, ref offset, 0);
            ProtocolTestData.WriteUInt16(body, ref offset, 31000);
            ProtocolTestData.WriteByte(body, ref offset, 0);
            ProtocolTestData.WriteUInt16(body, ref offset, 250);
            ProtocolTestData.WriteByte(body, ref offset, 0);
            ProtocolTestData.WriteUInt16(body, ref offset, 1000);
            ProtocolTestData.WriteByte(body, ref offset, 0);
            ProtocolTestData.WriteFloat(body, ref offset, 123.4f);
            ProtocolTestData.WriteFloat(body, ref offset, totalDistance);
            ProtocolTestData.WriteFloat(body, ref offset, 0);
            ProtocolTestData.WriteByte(body, ref offset, position);
            ProtocolTestData.WriteByte(body, ref offset, currentLap);
            ProtocolTestData.WriteByte(body, ref offset, pitStatus);
            ProtocolTestData.WriteByte(body, ref offset, numPitStops);
            ProtocolTestData.WriteByte(body, ref offset, 0);
            ProtocolTestData.WriteByte(body, ref offset, 0);
            ProtocolTestData.WriteByte(body, ref offset, 0);
            ProtocolTestData.WriteByte(body, ref offset, 0);
            ProtocolTestData.WriteByte(body, ref offset, 0);
            ProtocolTestData.WriteByte(body, ref offset, 0);
            ProtocolTestData.WriteByte(body, ref offset, 0);
            ProtocolTestData.WriteByte(body, ref offset, position);
            ProtocolTestData.WriteByte(body, ref offset, 1);
            ProtocolTestData.WriteByte(body, ref offset, resultStatus);
            ProtocolTestData.WriteByte(body, ref offset, isPitLaneTimerActive ? (byte)1 : (byte)0);
            ProtocolTestData.WriteUInt16(body, ref offset, pitLaneTimeInLaneInMs);
            ProtocolTestData.WriteUInt16(body, ref offset, pitStopTimerInMs);
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
        byte tyreAgeLaps = 9)
    {
        return BuildPacket(sessionUid, PacketId.CarStatus, UdpPacketConstants.CarStatusBodySize, body =>
        {
            var offset = 5;
            ProtocolTestData.WriteFloat(body, ref offset, fuelInTank);
            offset = 25;
            ProtocolTestData.WriteByte(body, ref offset, actualCompound);
            ProtocolTestData.WriteByte(body, ref offset, visualCompound);
            ProtocolTestData.WriteByte(body, ref offset, tyreAgeLaps);
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

    private static byte[] BuildEventPacket(ulong sessionUid, string eventCode)
    {
        return BuildPacket(sessionUid, PacketId.Event, UdpPacketConstants.EventBodySize, body =>
        {
            Encoding.ASCII.GetBytes(eventCode, body[..4]);
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
}
