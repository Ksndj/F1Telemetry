using System.Buffers.Binary;
using System.Text;
using F1Telemetry.Udp.Packets;
using F1Telemetry.Udp.Parsers;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// F1 24（packetFormat = 2024）版本化解析测试：
/// 覆盖 2024 布局与 2025 的差异（Participants name[48]/无 livery、FinalClassification 无 resultReason、
/// CarDamage 无 tyreBlisters、MotionEx 无末尾 9 个 float），22 辆数据数组长度与错误长度校验。
/// F1 25/26 用例保持各自测试文件不变。
/// </summary>
public sealed class PacketParserFormat2024Tests
{
    private const ushort Format24 = ProtocolTestData.PacketFormat24;

    [Fact]
    public void Participants_Format2024_Parses22ParticipantsWith48ByteNameAndNoLivery()
    {
        var payload = ProtocolTestData.BuildPacket(
            PacketId.Participants,
            UdpPacketConstants.ParticipantsBodySizeByFormat[Format24],
            WriteParticipants24Body,
            Format24);
        var parser = new ParticipantsPacketParser();

        var parsed = parser.TryParse(payload.AsMemory(PacketHeader.Size), Format24, out var packet, out var error);

        Assert.True(parsed);
        Assert.Null(error);
        Assert.Equal((byte)22, packet.NumActiveCars);
        Assert.Equal(22, packet.Participants.Length);

        var first = packet.Participants[0];
        // 2024：DriverId/NetworkId/TeamId 为 byte，name 为 48 字节（长名完整读入）。
        Assert.Equal((ushort)1, first.DriverId);
        Assert.Equal((ushort)2, first.NetworkId);
        Assert.Equal((ushort)3, first.TeamId);
        Assert.Equal(LongName, first.Name);
        Assert.Equal((byte)44, first.RaceNumber);
        // 2024：techLevel 为 uint16（0x0102 = 258）。
        Assert.Equal((ushort)258, first.TechLevel);
        Assert.Equal((byte)7, first.Platform);
        // 2024：无 livery 字段，模型保持默认。
        Assert.Equal((byte)0, first.NumColours);
        Assert.All(first.LiveryColours, colour => Assert.Equal(new LiveryColourData(0, 0, 0), colour));
    }

    [Fact]
    public void Participants_Format2024_WrongBodyLengthFails()
    {
        AssertWrongLengthFails(PacketId.Participants, new ParticipantsPacketParser(), UdpPacketConstants.ParticipantsBodySizeByFormat[Format24]);
    }

    [Fact]
    public void FinalClassification_Format2024_Parses22CarsWithoutResultReason()
    {
        var payload = ProtocolTestData.BuildPacket(
            PacketId.FinalClassification,
            UdpPacketConstants.FinalClassificationBodySizeByFormat[Format24],
            WriteFinalClassification24Body,
            Format24);
        var parser = new FinalClassificationPacketParser();

        var parsed = parser.TryParse(payload.AsMemory(PacketHeader.Size), Format24, out var packet, out var error);

        Assert.True(parsed);
        Assert.Null(error);
        Assert.Equal((byte)22, packet.NumCars);
        Assert.Equal(22, packet.Cars.Length);

        var first = packet.Cars[0];
        Assert.Equal((byte)3, first.Position);
        Assert.Equal((byte)29, first.NumLaps);
        Assert.Equal((byte)3, first.ResultStatus);
        // 2024：无 resultReason（resultStatus 后直接 bestLapTimeInMS），保持默认 0。
        Assert.Equal((byte)0, first.ResultReason);
        Assert.Equal(84289u, first.BestLapTimeInMs);
        Assert.Equal(4567.89, first.TotalRaceTime, precision: 5);
        Assert.Equal((byte)5, first.PenaltiesTime);
        Assert.Equal((byte)1, first.NumPenalties);
        Assert.Equal(8, first.TyreStintsActual.Length);
        Assert.Equal((byte)2, first.TyreStintsActual[0]);
        Assert.Equal((byte)3, first.TyreStintsVisual[0]);
        Assert.Equal((byte)10, first.TyreStintsEndLaps[0]);
    }

    [Fact]
    public void FinalClassification_Format2024_WrongBodyLengthFails()
    {
        AssertWrongLengthFails(PacketId.FinalClassification, new FinalClassificationPacketParser(), UdpPacketConstants.FinalClassificationBodySizeByFormat[Format24]);
    }

    [Fact]
    public void CarDamage_Format2024_Parses22CarsWithoutTyreBlisters()
    {
        var payload = ProtocolTestData.BuildPacket(
            PacketId.CarDamage,
            UdpPacketConstants.CarDamageBodySizeByFormat[Format24],
            WriteCarDamage24Body,
            Format24);
        var parser = new CarDamagePacketParser();

        var parsed = parser.TryParse(payload.AsMemory(PacketHeader.Size), Format24, out var packet, out var error);

        Assert.True(parsed);
        Assert.Null(error);
        Assert.Equal(22, packet.Cars.Length);

        var first = packet.Cars[0];
        Assert.Equal(0.8f, first.TyreWear.RearLeft, precision: 3);
        Assert.Equal((byte)4, first.TyreDamage.FrontRight);
        Assert.Equal((byte)8, first.BrakesDamage.FrontRight);
        // 2024：无 tyreBlisters（brakesDamage 后直接 frontLeftWingDamage），保持默认 0。
        Assert.Equal(new WheelSet<byte>(0, 0, 0, 0), first.TyreBlisters);
        Assert.Equal((byte)9, first.FrontLeftWingDamage);
        Assert.Equal((byte)10, first.FrontRightWingDamage);
        Assert.Equal((byte)11, first.RearWingDamage);
        Assert.Equal((byte)16, first.EngineDamage);
        Assert.True(first.EngineBlown);
        Assert.False(first.EngineSeized);
    }

    [Fact]
    public void CarDamage_Format2024_WrongBodyLengthFails()
    {
        AssertWrongLengthFails(PacketId.CarDamage, new CarDamagePacketParser(), UdpPacketConstants.CarDamageBodySizeByFormat[Format24]);
    }

    [Fact]
    public void MotionEx_Format2024_ParsesWithoutTrailingFields()
    {
        var payload = ProtocolTestData.BuildPacket(
            PacketId.MotionEx,
            UdpPacketConstants.MotionExBodySizeByFormat[Format24],
            WriteMotionEx24Body,
            Format24);
        var parser = new MotionExPacketParser();

        var parsed = parser.TryParse(payload.AsMemory(PacketHeader.Size), Format24, out var packet, out var error);

        Assert.True(parsed);
        Assert.Null(error);
        Assert.Equal(1.5f, packet.SuspensionPosition.RearLeft, precision: 3);
        Assert.Equal(6.5f, packet.WheelVerticalForce.RearLeft, precision: 3);
        Assert.Equal(7.5f, packet.FrontRollAngle, precision: 3);
        Assert.Equal(8.5f, packet.RearRollAngle, precision: 3);
        Assert.Equal(9.5f, packet.ChassisYaw, precision: 3);
        // 2024：包体到 chassisYaw 为止，追加字段保持默认。
        Assert.Equal(0f, packet.ChassisPitch);
        Assert.Equal(new WheelSet<float>(0f, 0f, 0f, 0f), packet.WheelCamber);
        Assert.Equal(new WheelSet<float>(0f, 0f, 0f, 0f), packet.WheelCamberGain);
    }

    [Fact]
    public void MotionEx_Format2024_WrongBodyLengthFails()
    {
        AssertWrongLengthFails(PacketId.MotionEx, new MotionExPacketParser(), UdpPacketConstants.MotionExBodySizeByFormat[Format24]);
    }

    [Fact]
    public void LapPositions_Format2024_NotRegisteredFails()
    {
        // LapPositions 为 2025 新增包，2024 尺寸表无条目，应走解析器拒绝路径。
        var payload = ProtocolTestData.BuildPacket(
            PacketId.LapPositions,
            UdpPacketConstants.LapPositionsBodySizeByFormat[UdpPacketConstants.Format2025],
            _ => { },
            Format24);
        var parser = new LapPositionsPacketParser();

        var parsed = parser.TryParse(payload.AsMemory(PacketHeader.Size), Format24, out var packet, out var error);

        Assert.False(parsed);
        Assert.Null(packet);
        Assert.NotNull(error);
    }

    [Fact]
    public void CarTelemetry2_Format2024_NotRegisteredFails()
    {
        // CarTelemetry2 为 2026 新增包，2024 尺寸表无条目，应走解析器拒绝路径。
        var payload = ProtocolTestData.BuildPacket(
            PacketId.CarTelemetry2,
            UdpPacketConstants.CarTelemetry2BodySizeByFormat[UdpPacketConstants.Format2026],
            _ => { },
            Format24);
        var parser = new CarTelemetry2PacketParser();

        var parsed = parser.TryParse(payload.AsMemory(PacketHeader.Size), Format24, out var packet, out var error);

        Assert.False(parsed);
        Assert.Null(packet);
        Assert.NotNull(error);
    }

    /// <summary>长于 32 字节的车手名，用于证明 2024 读 48 字节 name 字段。</summary>
    private const string LongName = "Driver 0 of F1 2024 test grid ____________";

    /// <summary>校验 24 格式下包体长度错误时 TryParse 失败。</summary>
    private static void AssertWrongLengthFails<TPacket>(
        PacketId packetId,
        FixedSizePacketParser<TPacket> parser,
        int expectedBodySize)
    {
        var payload = ProtocolTestData.BuildPacket(packetId, expectedBodySize + 1, _ => { }, Format24);

        var parsed = parser.TryParse(payload.AsMemory(PacketHeader.Size), Format24, out _, out var error);

        Assert.False(parsed);
        Assert.NotNull(error);
    }

    private static void WriteParticipants24Body(Span<byte> body)
    {
        var offset = 0;
        ProtocolTestData.WriteByte(body, ref offset, 22); // numActiveCars

        // 第一辆车写入可辨识值，其余 21 辆保持零。
        ProtocolTestData.WriteByte(body, ref offset, 0); // isAiControlled
        ProtocolTestData.WriteByte(body, ref offset, 1); // driverId
        ProtocolTestData.WriteByte(body, ref offset, 2); // networkId
        ProtocolTestData.WriteByte(body, ref offset, 3); // teamId
        ProtocolTestData.WriteByte(body, ref offset, 0); // isMyTeam
        ProtocolTestData.WriteByte(body, ref offset, 44); // raceNumber
        ProtocolTestData.WriteByte(body, ref offset, 30); // nationality
        var nameBytes = Encoding.ASCII.GetBytes(LongName);
        nameBytes.CopyTo(body.Slice(offset, nameBytes.Length));
        offset += 48; // name 固定 48 字节（含尾部零填充）
        ProtocolTestData.WriteByte(body, ref offset, 0); // yourTelemetry
        ProtocolTestData.WriteByte(body, ref offset, 1); // showOnlineNames
        ProtocolTestData.WriteUInt16(body, ref offset, 258); // techLevel（0x0102，uint16）
        ProtocolTestData.WriteByte(body, ref offset, 7); // platform
        // 无 numColours/liveryColours 字段。
    }

    private static void WriteFinalClassification24Body(Span<byte> body)
    {
        var offset = 0;
        ProtocolTestData.WriteByte(body, ref offset, 22); // numCars

        // 第一辆车写入可辨识值，其余 21 辆保持零。
        ProtocolTestData.WriteByte(body, ref offset, 3);  // position
        ProtocolTestData.WriteByte(body, ref offset, 29); // numLaps
        ProtocolTestData.WriteByte(body, ref offset, 5);  // gridPosition
        ProtocolTestData.WriteByte(body, ref offset, 12); // points
        ProtocolTestData.WriteByte(body, ref offset, 1);  // numPitStops
        ProtocolTestData.WriteByte(body, ref offset, 3);  // resultStatus
        // 2024 无 resultReason，直接 bestLapTimeInMS（84289 = 0x00014941）。
        ProtocolTestData.WriteUInt32(body, ref offset, 84289);
        WriteDouble(body, ref offset, 4567.89); // totalRaceTime
        ProtocolTestData.WriteByte(body, ref offset, 5);  // penaltiesTime
        ProtocolTestData.WriteByte(body, ref offset, 1);  // numPenalties
        ProtocolTestData.WriteByte(body, ref offset, 1);  // numTyreStints
        ProtocolTestData.WriteByte(body, ref offset, 2);  // tyreStintsActual[0]
        offset += UdpPacketConstants.MaxFinalClassificationTyreStints - 1; // 剩余 actual 槽位
        ProtocolTestData.WriteByte(body, ref offset, 3);  // tyreStintsVisual[0]
        offset += UdpPacketConstants.MaxFinalClassificationTyreStints - 1; // 剩余 visual 槽位
        ProtocolTestData.WriteByte(body, ref offset, 10); // tyreStintsEndLaps[0]
    }

    private static void WriteCarDamage24Body(Span<byte> body)
    {
        var offset = 0;
        ProtocolTestData.WriteFloat(body, ref offset, 0.8f); // car 0 tyreWear.RearLeft
        ProtocolTestData.WriteFloat(body, ref offset, 0.7f); // tyreWear.RearRight
        ProtocolTestData.WriteFloat(body, ref offset, 0.6f); // tyreWear.FrontLeft
        ProtocolTestData.WriteFloat(body, ref offset, 0.5f); // tyreWear.FrontRight
        ProtocolTestData.WriteByte(body, ref offset, 1); // tyreDamage.RearLeft
        ProtocolTestData.WriteByte(body, ref offset, 2); // tyreDamage.RearRight
        ProtocolTestData.WriteByte(body, ref offset, 3); // tyreDamage.FrontLeft
        ProtocolTestData.WriteByte(body, ref offset, 4); // tyreDamage.FrontRight
        ProtocolTestData.WriteByte(body, ref offset, 5); // brakesDamage.RearLeft
        ProtocolTestData.WriteByte(body, ref offset, 6); // brakesDamage.RearRight
        ProtocolTestData.WriteByte(body, ref offset, 7); // brakesDamage.FrontLeft
        ProtocolTestData.WriteByte(body, ref offset, 8); // brakesDamage.FrontRight
        // 2024 无 tyreBlisters：brakesDamage 后直接 wing/damage 字段。
        ProtocolTestData.WriteByte(body, ref offset, 9);  // frontLeftWingDamage
        ProtocolTestData.WriteByte(body, ref offset, 10); // frontRightWingDamage
        ProtocolTestData.WriteByte(body, ref offset, 11); // rearWingDamage
        ProtocolTestData.WriteByte(body, ref offset, 12); // floorDamage
        ProtocolTestData.WriteByte(body, ref offset, 13); // diffuserDamage
        ProtocolTestData.WriteByte(body, ref offset, 14); // sidepodDamage
        ProtocolTestData.WriteByte(body, ref offset, 1);  // drsFault
        ProtocolTestData.WriteByte(body, ref offset, 0);  // ersFault
        ProtocolTestData.WriteByte(body, ref offset, 15); // gearBoxDamage
        ProtocolTestData.WriteByte(body, ref offset, 16); // engineDamage
        ProtocolTestData.WriteByte(body, ref offset, 17); // engineMGUHWear
        ProtocolTestData.WriteByte(body, ref offset, 18); // engineESWear
        ProtocolTestData.WriteByte(body, ref offset, 19); // engineCEWear
        ProtocolTestData.WriteByte(body, ref offset, 20); // engineICEWear
        ProtocolTestData.WriteByte(body, ref offset, 21); // engineMGUKWear
        ProtocolTestData.WriteByte(body, ref offset, 22); // engineTCWear
        ProtocolTestData.WriteByte(body, ref offset, 1);  // engineBlown
        ProtocolTestData.WriteByte(body, ref offset, 0);  // engineSeized
    }

    private static void WriteMotionEx24Body(Span<byte> body)
    {
        var offset = 0;
        ProtocolTestData.WriteFloat(body, ref offset, 1.5f); // suspensionPosition.RearLeft
        // 跳过中间全零字段，写尾部可辨识值（包体 208 字节）。
        offset = 172;
        ProtocolTestData.WriteFloat(body, ref offset, 6.5f); // wheelVertForce.RearLeft
        offset = 196;
        ProtocolTestData.WriteFloat(body, ref offset, 7.5f); // frontRollAngle
        ProtocolTestData.WriteFloat(body, ref offset, 8.5f); // rearRollAngle
        ProtocolTestData.WriteFloat(body, ref offset, 9.5f); // chassisYaw（2024 末尾字段）
        // 无 chassisPitch/wheelCamber/wheelCamberGain。
    }

    private static void WriteDouble(Span<byte> destination, ref int offset, double value)
    {
        BinaryPrimitives.WriteInt64LittleEndian(
            destination.Slice(offset, sizeof(double)),
            BitConverter.DoubleToInt64Bits(value));
        offset += sizeof(double);
    }
}
