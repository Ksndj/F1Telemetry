using System.Numerics;
using F1Telemetry.Udp.Packets;

namespace F1Telemetry.Udp.Parsers;

public sealed class MotionPacketParser : FixedSizePacketParser<MotionPacket>
{
    public MotionPacketParser()
        : base(nameof(MotionPacket), UdpPacketConstants.MotionBodySize)
    {
    }

    protected override MotionPacket Parse(ref PacketBufferReader reader)
    {
        var cars = new CarMotionData[UdpPacketConstants.MaxCarsInSession];

        for (var index = 0; index < cars.Length; index++)
        {
            cars[index] = new CarMotionData(
                WorldPosition: new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                WorldVelocity: new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                WorldForwardDirectionX: reader.ReadInt16(),
                WorldForwardDirectionY: reader.ReadInt16(),
                WorldForwardDirectionZ: reader.ReadInt16(),
                WorldRightDirectionX: reader.ReadInt16(),
                WorldRightDirectionY: reader.ReadInt16(),
                WorldRightDirectionZ: reader.ReadInt16(),
                GForceLateral: reader.ReadSingle(),
                GForceLongitudinal: reader.ReadSingle(),
                GForceVertical: reader.ReadSingle(),
                Yaw: reader.ReadSingle(),
                Pitch: reader.ReadSingle(),
                Roll: reader.ReadSingle());
        }

        return new MotionPacket(cars);
    }
}
