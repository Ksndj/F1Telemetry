using F1Telemetry.Udp.Packets;

namespace F1Telemetry.Udp.Parsers;

public sealed class MotionExPacketParser : FixedSizePacketParser<MotionExPacket>
{
    public MotionExPacketParser()
        : base(nameof(MotionExPacket), UdpPacketConstants.MotionExBodySizeByFormat)
    {
    }

    protected override MotionExPacket Parse(ref PacketBufferReader reader, ushort packetFormat)
    {
        // F1 24 包体到 chassisYaw 为止；F1 25 起在末尾追加 chassisPitch/wheelCamber/wheelCamberGain。
        // 2024 分支不读取追加字段，模型对应字段保持默认值。
        var isFormat2024 = packetFormat == UdpPacketConstants.Format2024;

        return new MotionExPacket(
            SuspensionPosition: PacketParserHelpers.ReadWheelSingles(ref reader),
            SuspensionVelocity: PacketParserHelpers.ReadWheelSingles(ref reader),
            SuspensionAcceleration: PacketParserHelpers.ReadWheelSingles(ref reader),
            WheelSpeed: PacketParserHelpers.ReadWheelSingles(ref reader),
            WheelSlipRatio: PacketParserHelpers.ReadWheelSingles(ref reader),
            WheelSlipAngle: PacketParserHelpers.ReadWheelSingles(ref reader),
            WheelLateralForce: PacketParserHelpers.ReadWheelSingles(ref reader),
            WheelLongitudinalForce: PacketParserHelpers.ReadWheelSingles(ref reader),
            HeightOfCogAboveGround: reader.ReadSingle(),
            LocalVelocityX: reader.ReadSingle(),
            LocalVelocityY: reader.ReadSingle(),
            LocalVelocityZ: reader.ReadSingle(),
            AngularVelocityX: reader.ReadSingle(),
            AngularVelocityY: reader.ReadSingle(),
            AngularVelocityZ: reader.ReadSingle(),
            AngularAccelerationX: reader.ReadSingle(),
            AngularAccelerationY: reader.ReadSingle(),
            AngularAccelerationZ: reader.ReadSingle(),
            FrontWheelsAngle: reader.ReadSingle(),
            WheelVerticalForce: PacketParserHelpers.ReadWheelSingles(ref reader),
            FrontAeroHeight: reader.ReadSingle(),
            RearAeroHeight: reader.ReadSingle(),
            FrontRollAngle: reader.ReadSingle(),
            RearRollAngle: reader.ReadSingle(),
            ChassisYaw: reader.ReadSingle(),
            ChassisPitch: isFormat2024 ? 0f : reader.ReadSingle(),
            WheelCamber: isFormat2024 ? new WheelSet<float>(0f, 0f, 0f, 0f) : PacketParserHelpers.ReadWheelSingles(ref reader),
            WheelCamberGain: isFormat2024 ? new WheelSet<float>(0f, 0f, 0f, 0f) : PacketParserHelpers.ReadWheelSingles(ref reader));
    }
}
