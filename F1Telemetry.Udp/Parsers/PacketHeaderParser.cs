using System.Buffers.Binary;
using F1Telemetry.Core.Interfaces;
using F1Telemetry.Udp.Packets;

namespace F1Telemetry.Udp.Parsers;

public sealed class PacketHeaderParser : IPacketParser<PacketHeader>
{
    public bool TryParse(ReadOnlyMemory<byte> payload, out PacketHeader packet, out string? error)
    {
        if (payload.Length < PacketHeader.Size)
        {
            packet = default!;
            error = $"UDP payload length {payload.Length} is smaller than header size {PacketHeader.Size}.";
            return false;
        }

        var span = payload.Span;
        var sessionTimeBits = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(15, sizeof(uint)));

        packet = new PacketHeader(
            PacketFormat: BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(0, sizeof(ushort))),
            GameYear: span[2],
            GameMajorVersion: span[3],
            GameMinorVersion: span[4],
            PacketVersion: span[5],
            RawPacketId: span[6],
            SessionUid: BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(7, sizeof(ulong))),
            SessionTime: BitConverter.Int32BitsToSingle((int)sessionTimeBits),
            FrameIdentifier: BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(19, sizeof(uint))),
            OverallFrameIdentifier: BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(23, sizeof(uint))),
            PlayerCarIndex: span[27],
            SecondaryPlayerCarIndex: span[28]);

        error = null;
        return true;
    }
}
