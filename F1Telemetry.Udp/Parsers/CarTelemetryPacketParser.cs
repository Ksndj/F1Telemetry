using F1Telemetry.Udp.Packets;

namespace F1Telemetry.Udp.Parsers;

public sealed class CarTelemetryPacketParser : FixedSizePacketParser<CarTelemetryPacket>
{
    public CarTelemetryPacketParser()
        : base(nameof(CarTelemetryPacket), UdpPacketConstants.CarTelemetryBodySize)
    {
    }

    protected override CarTelemetryPacket Parse(ref PacketBufferReader reader)
    {
        var cars = new CarTelemetryData[UdpPacketConstants.MaxCarsInSession];

        for (var index = 0; index < cars.Length; index++)
        {
            cars[index] = new CarTelemetryData(
                Speed: reader.ReadUInt16(),
                Throttle: reader.ReadSingle(),
                Steer: reader.ReadSingle(),
                Brake: reader.ReadSingle(),
                Clutch: reader.ReadByte(),
                Gear: reader.ReadSByte(),
                EngineRpm: reader.ReadUInt16(),
                Drs: reader.ReadBooleanByte(),
                RevLightsPercent: reader.ReadByte(),
                RevLightsBitValue: reader.ReadUInt16(),
                BrakesTemperature: PacketParserHelpers.ReadWheelUInt16(ref reader),
                TyresSurfaceTemperature: PacketParserHelpers.ReadWheelBytes(ref reader),
                TyresInnerTemperature: PacketParserHelpers.ReadWheelBytes(ref reader),
                EngineTemperature: reader.ReadUInt16(),
                TyresPressure: PacketParserHelpers.ReadWheelSingles(ref reader),
                SurfaceType: PacketParserHelpers.ReadWheelBytes(ref reader));
        }

        return new CarTelemetryPacket(
            Cars: cars,
            MfdPanelIndex: reader.ReadByte(),
            MfdPanelIndexSecondaryPlayer: reader.ReadByte(),
            SuggestedGear: reader.ReadSByte());
    }
}
