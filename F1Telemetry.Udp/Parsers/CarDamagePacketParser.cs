using F1Telemetry.Udp.Packets;

namespace F1Telemetry.Udp.Parsers;

public sealed class CarDamagePacketParser : FixedSizePacketParser<CarDamagePacket>
{
    public CarDamagePacketParser()
        : base(nameof(CarDamagePacket), UdpPacketConstants.CarDamageBodySize)
    {
    }

    protected override CarDamagePacket Parse(ref PacketBufferReader reader)
    {
        var cars = new CarDamageData[UdpPacketConstants.MaxCarsInSession];

        for (var index = 0; index < cars.Length; index++)
        {
            cars[index] = new CarDamageData(
                TyreWear: PacketParserHelpers.ReadWheelSingles(ref reader),
                TyreDamage: PacketParserHelpers.ReadWheelBytes(ref reader),
                BrakesDamage: PacketParserHelpers.ReadWheelBytes(ref reader),
                TyreBlisters: PacketParserHelpers.ReadWheelBytes(ref reader),
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
