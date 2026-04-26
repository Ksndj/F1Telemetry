using System.Buffers.Binary;
using System.IO;
using System.Net;
using System.Text.Json;
using F1Telemetry.Core.Models;
using F1Telemetry.Udp.Services;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies optional raw UDP JSONL recording without opening a real UDP port.
/// </summary>
public sealed class UdpRawLogWriterTests
{
    /// <summary>
    /// Verifies raw UDP logging is disabled by default.
    /// </summary>
    [Fact]
    public async Task DefaultOptions_DisableRawUdpLogging()
    {
        var directory = CreateLogDirectory();
        var writer = new UdpRawLogWriter();

        writer.TryEnqueue(CreateDatagram(CreatePacketPayload()));
        await writer.DisposeAsync();

        Assert.False(new UdpRawLogOptions().Enabled);
        Assert.False(Directory.Exists(directory));
        Assert.Equal(0, writer.Status.WrittenPacketCount);
    }

    /// <summary>
    /// Verifies an enabled writer stores one UDP packet as one JSONL line.
    /// </summary>
    [Fact]
    public async Task TryEnqueue_WhenEnabled_WritesJsonLineWithHeaderAndPayload()
    {
        var directory = CreateLogDirectory();
        var payload = CreatePacketPayload();
        var writer = new UdpRawLogWriter();
        writer.UpdateOptions(new UdpRawLogOptions { Enabled = true, DirectoryPath = directory });

        writer.TryEnqueue(CreateDatagram(payload));
        await writer.DisposeAsync();

        var logFile = Assert.Single(Directory.EnumerateFiles(directory, "*.jsonl"));
        var line = Assert.Single(await File.ReadAllLinesAsync(logFile));
        using var json = JsonDocument.Parse(line);
        var root = json.RootElement;

        Assert.True(root.TryGetProperty("timestampUtc", out _));
        Assert.Equal(payload.Length, root.GetProperty("length").GetInt32());
        Assert.Equal(6, root.GetProperty("packetId").GetInt32());
        Assert.Equal(123UL, root.GetProperty("sessionUid").GetUInt64());
        Assert.Equal(4567U, root.GetProperty("frameIdentifier").GetUInt32());
        Assert.Equal(0, root.GetProperty("playerCarIndex").GetInt32());
        Assert.Equal(2025, root.GetProperty("packetFormat").GetInt32());
        Assert.Equal(25, root.GetProperty("gameYear").GetInt32());
        Assert.Equal(1, root.GetProperty("packetVersion").GetInt32());
        Assert.Equal("127.0.0.1:20777", root.GetProperty("source").GetString());
        Assert.Equal(payload, Convert.FromBase64String(root.GetProperty("payloadBase64").GetString()!));
        Assert.Equal(logFile, writer.Status.CurrentFilePath);
        Assert.Equal(1, writer.Status.WrittenPacketCount);
    }

    /// <summary>
    /// Verifies a full queue drops raw log packets without throwing.
    /// </summary>
    [Fact]
    public async Task TryEnqueue_WhenQueueFull_DropsPacketWithoutThrowing()
    {
        var directory = CreateLogDirectory();
        var writer = new UdpRawLogWriter();
        writer.UpdateOptions(new UdpRawLogOptions
        {
            Enabled = true,
            DirectoryPath = directory,
            QueueCapacity = 0
        });

        Exception? exception = null;
        try
        {
            writer.TryEnqueue(CreateDatagram(CreatePacketPayload()));
        }
        catch (Exception ex)
        {
            exception = ex;
        }
        await writer.DisposeAsync();

        Assert.Null(exception);
        Assert.Equal(1, writer.Status.DroppedPacketCount);
        Assert.Equal(0, writer.Status.WrittenPacketCount);
    }

    /// <summary>
    /// Verifies dispose drains accepted packets before closing the writer.
    /// </summary>
    [Fact]
    public async Task DisposeAsync_FlushesAcceptedPackets()
    {
        var directory = CreateLogDirectory();
        var writer = new UdpRawLogWriter();
        writer.UpdateOptions(new UdpRawLogOptions { Enabled = true, DirectoryPath = directory });

        writer.TryEnqueue(CreateDatagram(CreatePacketPayload(frameIdentifier: 1)));
        writer.TryEnqueue(CreateDatagram(CreatePacketPayload(frameIdentifier: 2)));
        await writer.DisposeAsync();

        var logFile = Assert.Single(Directory.EnumerateFiles(directory, "*.jsonl"));
        var lines = await File.ReadAllLinesAsync(logFile);

        Assert.Equal(2, lines.Length);
        Assert.Equal(2, writer.Status.WrittenPacketCount);
    }

    private static UdpDatagram CreateDatagram(byte[] payload)
    {
        return new UdpDatagram(payload, new IPEndPoint(IPAddress.Loopback, 20777), DateTimeOffset.UtcNow);
    }

    private static byte[] CreatePacketPayload(uint frameIdentifier = 4567)
    {
        var payload = new byte[33];
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(0, sizeof(ushort)), 2025);
        payload[2] = 25;
        payload[3] = 1;
        payload[4] = 0;
        payload[5] = 1;
        payload[6] = 6;
        BinaryPrimitives.WriteUInt64LittleEndian(payload.AsSpan(7, sizeof(ulong)), 123UL);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(15, sizeof(uint)), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(19, sizeof(uint)), frameIdentifier);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(23, sizeof(uint)), frameIdentifier);
        payload[27] = 0;
        payload[28] = 255;
        payload[29] = 0xAA;
        payload[30] = 0xBB;
        payload[31] = 0xCC;
        payload[32] = 0xDD;
        return payload;
    }

    private static string CreateLogDirectory()
    {
        return Path.Combine(Path.GetTempPath(), "F1TelemetryTests", Guid.NewGuid().ToString("N"), ".logs", "udp");
    }
}
