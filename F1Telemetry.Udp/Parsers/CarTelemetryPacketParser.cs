using F1Telemetry.Udp.Packets;

namespace F1Telemetry.Udp.Parsers;

public sealed class CarTelemetryPacketParser : FixedSizePacketParser<CarTelemetryPacket>
{
    public CarTelemetryPacketParser()
        : base(nameof(CarTelemetryPacket), UdpPacketConstants.CarTelemetryBodySizeByFormat)
    {
    }

    protected override CarTelemetryPacket Parse(ref PacketBufferReader reader, ushort packetFormat)
    {
        var carCount = UdpPacketConstants.GetMaxCarsInSession(packetFormat);
        var cars = new CarTelemetryData[carCount];

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
                // F1 26 将 engineTemperature 缩窄为 byte，F1 25 仍为 uint16。
                EngineTemperature: packetFormat == UdpPacketConstants.Format2026 ? reader.ReadByte() : reader.ReadUInt16(),
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

/// <summary>F1 26 主动空气动力学遥测包（CarTelemetry2）解析器：24 辆 × 10 字节，无尾部字段。</summary>
public sealed class CarTelemetry2PacketParser : FixedSizePacketParser<CarTelemetry2Packet>
{
    public CarTelemetry2PacketParser()
        : base(nameof(CarTelemetry2Packet), UdpPacketConstants.CarTelemetry2BodySizeByFormat)
    {
    }

    protected override CarTelemetry2Packet Parse(ref PacketBufferReader reader, ushort packetFormat)
    {
        var carCount = UdpPacketConstants.GetMaxCarsInSession(packetFormat);
        var cars = new CarTelemetry2Data[carCount];

        for (var index = 0; index < cars.Length; index++)
        {
            cars[index] = new CarTelemetry2Data(
                ActiveAeroMode: reader.ReadByte(),
                ActiveAeroAvailable: reader.ReadByte(),
                ActiveAeroActivationDistance: reader.ReadUInt16(),
                OvertakeAvailable: reader.ReadByte(),
                OvertakeActive: reader.ReadByte(),
                OvertakeActivationDistance: reader.ReadUInt16(),
                Regulations2026: reader.ReadByte(),
                DrivingWrongWay: reader.ReadByte());
        }

        return new CarTelemetry2Packet(cars);
    }
}
