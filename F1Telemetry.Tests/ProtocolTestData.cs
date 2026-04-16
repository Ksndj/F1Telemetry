using System.Buffers.Binary;
using F1Telemetry.Udp.Packets;

namespace F1Telemetry.Tests;

internal delegate void PacketBodyWriter(Span<byte> body);

internal static class ProtocolTestData
{
    public const ushort PacketFormat = 2025;
    public const byte GameYear = 25;
    public const byte GameMajorVersion = 1;
    public const byte GameMinorVersion = 0;
    public const byte PacketVersion = 1;
    public const ulong SessionUid = 0x1122334455667788UL;
    public const float SessionTime = 123.5f;
    public const uint FrameIdentifier = 42;
    public const uint OverallFrameIdentifier = 84;
    public const byte PlayerCarIndex = 7;
    public const byte SecondaryPlayerCarIndex = 255;

    public static byte[] BuildPacket(PacketId packetId, int bodySize, PacketBodyWriter writeBody)
    {
        var payload = new byte[PacketHeader.Size + bodySize];
        WriteHeader(payload.AsSpan(0, PacketHeader.Size), packetId);
        writeBody(payload.AsSpan(PacketHeader.Size, bodySize));
        return payload;
    }

    public static byte[] BuildHeaderOnlyPacket(PacketId packetId)
    {
        var payload = new byte[PacketHeader.Size];
        WriteHeader(payload.AsSpan(), packetId);
        return payload;
    }

    public static void WriteHeader(Span<byte> destination, PacketId packetId)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(0, sizeof(ushort)), PacketFormat);
        destination[2] = GameYear;
        destination[3] = GameMajorVersion;
        destination[4] = GameMinorVersion;
        destination[5] = PacketVersion;
        destination[6] = (byte)packetId;
        BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(7, sizeof(ulong)), SessionUid);
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(15, sizeof(int)), BitConverter.SingleToInt32Bits(SessionTime));
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(19, sizeof(uint)), FrameIdentifier);
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(23, sizeof(uint)), OverallFrameIdentifier);
        destination[27] = PlayerCarIndex;
        destination[28] = SecondaryPlayerCarIndex;
    }

    public static void WriteUInt16(Span<byte> destination, ref int offset, ushort value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(offset, sizeof(ushort)), value);
        offset += sizeof(ushort);
    }

    public static void WriteInt16(Span<byte> destination, ref int offset, short value)
    {
        BinaryPrimitives.WriteInt16LittleEndian(destination.Slice(offset, sizeof(short)), value);
        offset += sizeof(short);
    }

    public static void WriteUInt32(Span<byte> destination, ref int offset, uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(offset, sizeof(uint)), value);
        offset += sizeof(uint);
    }

    public static void WriteInt32(Span<byte> destination, ref int offset, int value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(offset, sizeof(int)), value);
        offset += sizeof(int);
    }

    public static void WriteFloat(Span<byte> destination, ref int offset, float value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(offset, sizeof(int)), BitConverter.SingleToInt32Bits(value));
        offset += sizeof(float);
    }

    public static void WriteByte(Span<byte> destination, ref int offset, byte value)
    {
        destination[offset] = value;
        offset += sizeof(byte);
    }

    public static void WriteSByte(Span<byte> destination, ref int offset, sbyte value)
    {
        destination[offset] = unchecked((byte)value);
        offset += sizeof(sbyte);
    }
}
