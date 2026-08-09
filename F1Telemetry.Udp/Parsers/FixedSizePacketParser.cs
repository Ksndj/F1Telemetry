using F1Telemetry.Core.Interfaces;
using F1Telemetry.Udp.Packets;

namespace F1Telemetry.Udp.Parsers;

/// <summary>
/// 固定尺寸包体解析器基类：按协议格式（packetFormat）查表校验包体长度，
/// 再以 <see cref="PacketBufferReader"/> 顺序读取字段并构建模型。
/// </summary>
public abstract class FixedSizePacketParser<TPacket> : IPacketParser<TPacket>
{
    private readonly IReadOnlyDictionary<ushort, int> _expectedPayloadSizesByFormat;
    private readonly string _packetName;

    /// <summary>
    /// 初始化解析器。
    /// </summary>
    /// <param name="packetName">包类型名，用于错误信息。</param>
    /// <param name="expectedPayloadSizesByFormat">按协议格式的包体（不含头）尺寸表。</param>
    protected FixedSizePacketParser(string packetName, IReadOnlyDictionary<ushort, int> expectedPayloadSizesByFormat)
    {
        _packetName = packetName ?? throw new ArgumentNullException(nameof(packetName));
        _expectedPayloadSizesByFormat = expectedPayloadSizesByFormat
            ?? throw new ArgumentNullException(nameof(expectedPayloadSizesByFormat));
    }

    /// <inheritdoc />
    /// <summary>按 F1 25（2025）格式解析，供单版本场景与测试使用。</summary>
    public bool TryParse(ReadOnlyMemory<byte> payload, out TPacket packet, out string? error)
    {
        return TryParse(payload, UdpPacketConstants.Format2025, out packet, out error);
    }

    /// <summary>
    /// 按指定协议格式解析：先按格式查表校验包体长度，再执行 <see cref="Parse"/>，
    /// 最后要求读取器恰好消费完整负载。
    /// </summary>
    public bool TryParse(ReadOnlyMemory<byte> payload, ushort packetFormat, out TPacket packet, out string? error)
    {
        if (!_expectedPayloadSizesByFormat.TryGetValue(packetFormat, out var expectedPayloadSize))
        {
            packet = default!;
            error = $"{_packetName} has no registered payload size for packet format {packetFormat}.";
            return false;
        }

        if (payload.Length != expectedPayloadSize)
        {
            packet = default!;
            error = $"{_packetName} payload length {payload.Length} does not match expected size {expectedPayloadSize} for packet format {packetFormat}.";
            return false;
        }

        try
        {
            var reader = new PacketBufferReader(payload);
            packet = Parse(ref reader, packetFormat);
            reader.EnsureConsumed(_packetName);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            packet = default!;
            error = $"{_packetName} parse failed: {ex.Message}";
            return false;
        }
    }

    /// <summary>子类按协议格式分支解析包体字段。</summary>
    protected abstract TPacket Parse(ref PacketBufferReader reader, ushort packetFormat);
}
