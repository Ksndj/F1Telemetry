using System.Numerics;
using F1Telemetry.Udp.Packets;

namespace F1Telemetry.Udp.Parsers;

internal static class PacketParserHelpers
{
    public static WheelSet<byte> ReadWheelBytes(ref PacketBufferReader reader)
    {
        return new WheelSet<byte>(
            reader.ReadByte(),
            reader.ReadByte(),
            reader.ReadByte(),
            reader.ReadByte());
    }

    public static WheelSet<ushort> ReadWheelUInt16(ref PacketBufferReader reader)
    {
        return new WheelSet<ushort>(
            reader.ReadUInt16(),
            reader.ReadUInt16(),
            reader.ReadUInt16(),
            reader.ReadUInt16());
    }

    public static WheelSet<float> ReadWheelSingles(ref PacketBufferReader reader)
    {
        return new WheelSet<float>(
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadSingle());
    }

    public static Vector3 ReadVector3(ref PacketBufferReader reader)
    {
        return reader.ReadVector3();
    }

    public static byte[] ReadByteArray(ref PacketBufferReader reader, int count)
    {
        return reader.ReadBytes(count);
    }
}
