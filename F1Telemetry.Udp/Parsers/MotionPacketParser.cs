using System.Numerics;
using F1Telemetry.Udp.Packets;

namespace F1Telemetry.Udp.Parsers;

/// <summary>运动学包（Motion）解析器：车辆数组长度按协议格式（25→22 / 26→24）。</summary>
public sealed class MotionPacketParser : FixedSizePacketParser<MotionPacket>
{
    public MotionPacketParser()
        : base(nameof(MotionPacket), UdpPacketConstants.MotionBodySizeByFormat)
    {
    }

    protected override MotionPacket Parse(ref PacketBufferReader reader, ushort packetFormat)
    {
        var carCount = UdpPacketConstants.GetMaxCarsInSession(packetFormat);
        var cars = new CarMotionData[carCount];

        for (var index = 0; index < cars.Length; index++)
        {
            var worldPosition = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            var worldVelocity = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            var forwardDirectionX = reader.ReadInt16();
            var forwardDirectionY = reader.ReadInt16();
            var forwardDirectionZ = reader.ReadInt16();
            var rightDirectionX = reader.ReadInt16();
            var rightDirectionY = reader.ReadInt16();
            var rightDirectionZ = reader.ReadInt16();

            // F1 26 将 G 力三字段改为 int16（千分之一 g），F1 25 仍为 float。
            var gForceLateral = ReadGForce(ref reader, packetFormat);
            var gForceLongitudinal = ReadGForce(ref reader, packetFormat);
            var gForceVertical = ReadGForce(ref reader, packetFormat);

            cars[index] = new CarMotionData(
                WorldPosition: worldPosition,
                WorldVelocity: worldVelocity,
                WorldForwardDirectionX: forwardDirectionX,
                WorldForwardDirectionY: forwardDirectionY,
                WorldForwardDirectionZ: forwardDirectionZ,
                WorldRightDirectionX: rightDirectionX,
                WorldRightDirectionY: rightDirectionY,
                WorldRightDirectionZ: rightDirectionZ,
                GForceLateral: gForceLateral,
                GForceLongitudinal: gForceLongitudinal,
                GForceVertical: gForceVertical,
                Yaw: reader.ReadSingle(),
                Pitch: reader.ReadSingle(),
                Roll: reader.ReadSingle());
        }

        return new MotionPacket(cars);
    }

    /// <summary>F1 26 读 int16（千分之一 g）并转为 float，F1 25 直接读 float。</summary>
    private static float ReadGForce(ref PacketBufferReader reader, ushort packetFormat)
    {
        return packetFormat == UdpPacketConstants.Format2026 ? reader.ReadInt16() / 1000f : reader.ReadSingle();
    }
}
