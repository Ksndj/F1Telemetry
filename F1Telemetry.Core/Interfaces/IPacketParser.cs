namespace F1Telemetry.Core.Interfaces;

public interface IPacketParser<TPacket>
{
    bool TryParse(ReadOnlyMemory<byte> payload, out TPacket packet, out string? error);
}
