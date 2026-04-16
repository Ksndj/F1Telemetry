using F1Telemetry.Udp.Packets;

namespace F1Telemetry.Udp.Parsers;

public sealed class MotionExPacketParser : FixedSizePacketParser<MotionExPacket>
{
    public MotionExPacketParser()
        : base(nameof(MotionExPacket), UdpPacketConstants.MotionExBodySize)
    {
    }

    protected override MotionExPacket Parse(ref PacketBufferReader reader)
    {
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
            ChassisPitch: reader.ReadSingle(),
            WheelCamber: PacketParserHelpers.ReadWheelSingles(ref reader),
            WheelCamberGain: PacketParserHelpers.ReadWheelSingles(ref reader));
    }
}
