using F1Telemetry.Core.Interfaces;

namespace F1Telemetry.Udp.Parsers;

public abstract class FixedSizePacketParser<TPacket> : IPacketParser<TPacket>
{
    private readonly int _expectedPayloadSize;
    private readonly string _packetName;

    protected FixedSizePacketParser(string packetName, int expectedPayloadSize)
    {
        _packetName = packetName ?? throw new ArgumentNullException(nameof(packetName));
        _expectedPayloadSize = expectedPayloadSize;
    }

    public bool TryParse(ReadOnlyMemory<byte> payload, out TPacket packet, out string? error)
    {
        if (payload.Length != _expectedPayloadSize)
        {
            packet = default!;
            error = $"{_packetName} payload length {payload.Length} does not match expected size {_expectedPayloadSize}.";
            return false;
        }

        try
        {
            var reader = new PacketBufferReader(payload);
            packet = Parse(ref reader);
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

    protected abstract TPacket Parse(ref PacketBufferReader reader);
}
