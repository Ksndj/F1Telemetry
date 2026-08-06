using F1Telemetry.Udp.Packets;

namespace F1Telemetry.Udp.Parsers;

public sealed class CarDamagePacketParser : FixedSizePacketParser<CarDamagePacket>
{
    public CarDamagePacketParser()
        : base(nameof(CarDamagePacket), UdpPacketConstants.CarDamageBodySizeByFormat)
    {
    }

    protected override CarDamagePacket Parse(ref PacketBufferReader reader, ushort packetFormat)
    {
        var carCount = UdpPacketConstants.GetMaxCarsInSession(packetFormat);
        var cars = new CarDamageData[carCount];

        for (var index = 0; index < cars.Length; index++)
        {
            cars[index] = new CarDamageData(
                TyreWear: PacketParserHelpers.ReadWheelSingles(ref reader),
                TyreDamage: PacketParserHelpers.ReadWheelBytes(ref reader),
                BrakesDamage: PacketParserHelpers.ReadWheelBytes(ref reader),
                // F1 24 无 tyreBlisters[4]（brakesDamage 后直接 frontLeftWingDamage），模型保持默认值。
                // 注意：条件分支需内联在参数位置，保持字段按顺序消费读取器。
                TyreBlisters: packetFormat >= UdpPacketConstants.Format2025
                    ? PacketParserHelpers.ReadWheelBytes(ref reader)
                    : new WheelSet<byte>(0, 0, 0, 0),
                FrontLeftWingDamage: reader.ReadByte(),
                FrontRightWingDamage: reader.ReadByte(),
                RearWingDamage: reader.ReadByte(),
                FloorDamage: reader.ReadByte(),
                DiffuserDamage: reader.ReadByte(),
                SidepodDamage: reader.ReadByte(),
                DrsFault: reader.ReadBooleanByte(),
                ErsFault: reader.ReadBooleanByte(),
                GearBoxDamage: reader.ReadByte(),
                EngineDamage: reader.ReadByte(),
                EngineMguhWear: reader.ReadByte(),
                EngineEsWear: reader.ReadByte(),
                EngineCeWear: reader.ReadByte(),
                EngineIceWear: reader.ReadByte(),
                EngineMgukWear: reader.ReadByte(),
                EngineTcWear: reader.ReadByte(),
                EngineBlown: reader.ReadBooleanByte(),
                EngineSeized: reader.ReadBooleanByte());
        }

        return new CarDamagePacket(cars);
    }
}
