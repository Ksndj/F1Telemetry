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
    public async Task AnalyzeAsync_SplitsSessionsAndAggregatesKeyPackets()
    {
        var inputPath = CreateTempJsonlPath();
        var outputPath = Path.ChangeExtension(inputPath, ".md");
        var statusPayload = Convert.ToBase64String(BuildCarStatusPacket(1001UL, fuelInTank: 5.5f, actualCompound: 16, visualCompound: 17));
        var lines = new[]
        {
            BuildRecord(BuildSessionPacket(1001UL, trackId: 12, sessionType: 10, totalLaps: 5)),
            BuildRecord(BuildLapDataPacket(1001UL, currentLap: 3, totalDistance: 1234.5f)),
            BuildRecord(BuildCarTelemetryPacket(1001UL, speed: 321, throttle: 0.75f)),
            BuildRecord(BuildCarStatusPacket(1001UL, fuelInTank: 5.5f, actualCompound: 16, visualCompound: 17)),
            BuildRecord(BuildCarDamagePacket(1001UL, tyreWear: 12.5f)),
            BuildRecord(BuildTyreSetsPacket(1001UL, actualCompound: 16, visualCompound: 17, wear: 4)),
            BuildRecord(BuildEventPacket(1001UL, "SSTA")),
            BuildRecord(BuildSessionPacket(2002UL, trackId: 13, sessionType: 12, totalLaps: 10))
        };
        await File.WriteAllLinesAsync(inputPath, lines);
        var analyzer = new RawLogAnalyzerService();

        var result = await analyzer.AnalyzeAsync(new RawLogAnalyzerOptions(inputPath, outputPath));

        Assert.Equal(8, result.TotalLines);
        Assert.Equal(8, result.ParsedPacketCount);
        Assert.Equal(2, result.Sessions.Count);
        Assert.True(result.Sessions.TryGetValue(1001UL, out var firstSession));
        Assert.Equal(12, firstSession.TrackId);
        Assert.Equal(10, firstSession.SessionType);
        Assert.Equal(5, firstSession.TotalLaps);
        Assert.Equal(3, firstSession.MaxPlayerLapNumber);
        Assert.Equal(1234.5f, firstSession.MaxPlayerTotalDistance, precision: 2);
        Assert.Equal(321, firstSession.MaxPlayerSpeed);
        Assert.Equal(0.75f, firstSession.MaxPlayerThrottle, precision: 3);
        Assert.Equal(5.5f, firstSession.MinPlayerFuelInTank, precision: 3);
        Assert.Equal(5.5f, firstSession.MaxPlayerFuelInTank, precision: 3);
        Assert.Equal(12.5f, firstSession.MaxPlayerTyreWear, precision: 3);
        Assert.Contains("visual 17 / actual 16", firstSession.TyreCompoundPairs);
        Assert.Equal(1, firstSession.EventCodeCounts["SSTA"]);
        Assert.Equal(2, result.PacketIdCounts[PacketId.Session]);
        Assert.Equal(outputPath, result.ReportPath);
        Assert.True(File.Exists(outputPath));

        var markdown = await File.ReadAllTextAsync(outputPath);
        Assert.Contains("Session 1001", markdown);
        Assert.Contains("TrackId: 12", markdown);
        Assert.Contains("Fuel in tank: 5.5 -> 5.5", markdown);
        Assert.DoesNotContain("payloadBase64", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(statusPayload, markdown, StringComparison.Ordinal);
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
            BuildRecord(BuildSessionPacket(3003UL, trackId: 1, sessionType: 1, totalLaps: 3), declaredLength: 9999),
            BuildRecord(BuildHeaderOnlyKnownPacket(3003UL, PacketId.Session))
        };
        await File.WriteAllLinesAsync(inputPath, lines);
        var analyzer = new RawLogAnalyzerService();

        var result = await analyzer.AnalyzeAsync(new RawLogAnalyzerOptions(inputPath, outputPath));

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

    private static byte[] BuildLapDataPacket(ulong sessionUid, byte currentLap, float totalDistance)
    {
        return BuildPacket(sessionUid, PacketId.LapData, UdpPacketConstants.LapDataBodySize, body =>
        {
            var offset = 0;
            ProtocolTestData.WriteUInt32(body, ref offset, 90000);
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
            ProtocolTestData.WriteByte(body, ref offset, 1);
            ProtocolTestData.WriteByte(body, ref offset, currentLap);
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

    private static byte[] BuildCarStatusPacket(ulong sessionUid, float fuelInTank, byte actualCompound, byte visualCompound)
    {
        return BuildPacket(sessionUid, PacketId.CarStatus, UdpPacketConstants.CarStatusBodySize, body =>
        {
            var offset = 5;
            ProtocolTestData.WriteFloat(body, ref offset, fuelInTank);
            offset = 25;
            ProtocolTestData.WriteByte(body, ref offset, actualCompound);
            ProtocolTestData.WriteByte(body, ref offset, visualCompound);
            ProtocolTestData.WriteByte(body, ref offset, 9);
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
}
