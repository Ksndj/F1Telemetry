using System.Text;
using F1Telemetry.Udp.Packets;
using F1Telemetry.Udp.Parsers;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// F1 26（packetFormat = 2026）版本化解析测试：
/// 覆盖各解析器在 2026 格式下的字段布局差异、24 辆数据数组长度与错误长度校验。
/// F1 25 用例保持原测试文件不变。
/// </summary>
public sealed class PacketParserFormat2026Tests
{
    private const ushort Format26 = ProtocolTestData.PacketFormat26;
    private const ushort Format25 = UdpPacketConstants.Format2025;

    [Fact]
    public void Motion_Format2026_Parses24CarsWithInt16GForces()
    {
        var payload = ProtocolTestData.BuildPacket(
            PacketId.Motion,
            UdpPacketConstants.MotionBodySizeByFormat[Format26],
            WriteMotion26Body,
            Format26);
        var parser = new MotionPacketParser();

        var parsed = parser.TryParse(payload.AsMemory(PacketHeader.Size), Format26, out var packet, out var error);

        Assert.True(parsed);
        Assert.Null(error);
        Assert.Equal(24, packet.Cars.Length);

        var first = packet.Cars[0];
        Assert.Equal(1.5f, first.WorldPosition.X, precision: 3);
        Assert.Equal(4.5f, first.WorldVelocity.X, precision: 3);
        Assert.Equal((short)100, first.WorldForwardDirectionX);
        Assert.Equal((short)400, first.WorldRightDirectionX);
        // F1 26：G 力为 int16（千分之一 g），12345 → 12.345f。
        Assert.Equal(12.345f, first.GForceLateral, precision: 3);
        Assert.Equal(-3.25f, first.GForceLongitudinal, precision: 3);
        Assert.Equal(0.5f, first.GForceVertical, precision: 3);
        Assert.Equal(7.5f, first.Yaw, precision: 3);
        Assert.Equal(8.5f, first.Pitch, precision: 3);
        Assert.Equal(9.5f, first.Roll, precision: 3);

        // 旧 3 参 TryParse 默认按 2025 解析，26 长度应被拒绝。
        Assert.False(parser.TryParse(payload.AsMemory(PacketHeader.Size), out _, out _));
    }

    [Fact]
    public void Motion_Format2026_WrongBodyLengthFails()
    {
        AssertWrongLengthFails(PacketId.Motion, new MotionPacketParser(), UdpPacketConstants.MotionBodySizeByFormat[Format26]);
    }

    [Fact]
    public void Session_Format2026_ParsesAeroDrsAndAssistTailFields()
    {
        var payload = ProtocolTestData.BuildPacket(
            PacketId.Session,
            UdpPacketConstants.SessionBodySizeByFormat[Format26],
            WriteSession26Body,
            Format26);
        var parser = new SessionPacketParser();

        var parsed = parser.TryParse(payload.AsMemory(PacketHeader.Size), Format26, out var packet, out var error);

        Assert.True(parsed);
        Assert.Null(error);

        // 25 基础字段在 26 布局中仍正确。
        Assert.Equal((byte)1, packet.Weather);
        Assert.Equal((ushort)5000, packet.TrackLength);

        // 26 尾部新增字段。
        Assert.Equal((byte)3, packet.ActiveAeroTrackStatus);
        Assert.Equal((byte)2, packet.NumActiveAeroZonesFull);
        Assert.Equal(8, packet.ActiveAeroZonesFull!.Length);
        Assert.Equal(100f, packet.ActiveAeroZonesFull[0].ZoneStart, precision: 3);
        Assert.Equal(150f, packet.ActiveAeroZonesFull[0].ZoneEnd, precision: 3);
        Assert.Equal(200f, packet.ActiveAeroZonesFull[1].ZoneStart, precision: 3);
        Assert.Equal(250f, packet.ActiveAeroZonesFull[1].ZoneEnd, precision: 3);
        Assert.Equal((byte)4, packet.NumActiveAeroZonesPartial);
        Assert.Equal(8, packet.ActiveAeroZonesPartial!.Length);
        Assert.Equal(300f, packet.ActiveAeroZonesPartial[0].ZoneStart, precision: 3);
        Assert.Equal(350f, packet.ActiveAeroZonesPartial[0].ZoneEnd, precision: 3);
        Assert.Equal((byte)2, packet.NumDrsZones);
        Assert.Equal(4, packet.DrsZones!.Length);
        Assert.Equal(400f, packet.DrsZones[0].ZoneStart, precision: 3);
        Assert.Equal(450f, packet.DrsZones[0].ZoneEnd, precision: 3);
        Assert.Equal(0.75f, packet.StartReactionTime, precision: 3);
        Assert.Equal((byte)1, packet.AntiLockBrakesAssist);
        Assert.Equal((byte)2, packet.TractionControlAssist);
        Assert.Equal((byte)3, packet.DynamicRacingLineHiVis);
        Assert.Equal((byte)4, packet.DynamicRacingLineColourBlind);
        Assert.Equal((byte)5, packet.RecurringRewindPrompt);
    }

    [Fact]
    public void Session_Format2025_NewFieldsStayAtDefaults()
    {
        var payload = ProtocolTestData.BuildPacket(
            PacketId.Session,
            UdpPacketConstants.SessionBodySizeByFormat[Format25],
            body =>
            {
                var offset = 0;
                ProtocolTestData.WriteByte(body, ref offset, 2); // weather
            },
            Format25);
        var parser = new SessionPacketParser();

        var parsed = parser.TryParse(payload.AsMemory(PacketHeader.Size), Format25, out var packet, out var error);

        Assert.True(parsed);
        Assert.Null(error);
        Assert.Equal((byte)2, packet.Weather);
        Assert.Equal((byte)0, packet.ActiveAeroTrackStatus);
        Assert.Equal((byte)0, packet.NumActiveAeroZonesFull);
        Assert.Empty(packet.ActiveAeroZonesFull!);
        Assert.Equal((byte)0, packet.NumActiveAeroZonesPartial);
        Assert.Empty(packet.ActiveAeroZonesPartial!);
        Assert.Equal((byte)0, packet.NumDrsZones);
        Assert.Empty(packet.DrsZones!);
        Assert.Equal(0f, packet.StartReactionTime);
        Assert.Equal((byte)0, packet.AntiLockBrakesAssist);
        Assert.Equal((byte)0, packet.TractionControlAssist);
        Assert.Equal((byte)0, packet.DynamicRacingLineHiVis);
        Assert.Equal((byte)0, packet.DynamicRacingLineColourBlind);
        Assert.Equal((byte)0, packet.RecurringRewindPrompt);
    }

    [Fact]
    public void Session_Format2026_WrongBodyLengthFails()
    {
        AssertWrongLengthFails(PacketId.Session, new SessionPacketParser(), UdpPacketConstants.SessionBodySizeByFormat[Format26]);
    }

    [Fact]
    public void CarTelemetry_Format2026_Parses24CarsWithByteEngineTemperature()
    {
        var payload = ProtocolTestData.BuildPacket(
            PacketId.CarTelemetry,
            UdpPacketConstants.CarTelemetryBodySizeByFormat[Format26],
            WriteCarTelemetry26Body,
            Format26);
        var parser = new CarTelemetryPacketParser();

        var parsed = parser.TryParse(payload.AsMemory(PacketHeader.Size), Format26, out var packet, out var error);

        Assert.True(parsed);
        Assert.Null(error);
        Assert.Equal(24, packet.Cars.Length);

        var first = packet.Cars[0];
        Assert.Equal((ushort)321, first.Speed);
        Assert.Equal((ushort)12345, first.EngineRpm);
        // F1 26：engineTemperature 读 1 字节（0x6E = 110）。
        Assert.Equal((ushort)110, first.EngineTemperature);
        Assert.Equal(21.1f, first.TyresPressure.RearLeft, precision: 3);
        Assert.Equal((byte)4, first.SurfaceType.FrontRight);
        Assert.Equal((byte)1, packet.MfdPanelIndex);
        Assert.Equal((sbyte)8, packet.SuggestedGear);
    }

    [Fact]
    public void CarTelemetry_Format2026_WrongBodyLengthFails()
    {
        AssertWrongLengthFails(PacketId.CarTelemetry, new CarTelemetryPacketParser(), UdpPacketConstants.CarTelemetryBodySizeByFormat[Format26]);
    }

    [Fact]
    public void CarStatus_Format2026_Parses24CarsWithErsHarvestedLimitPerLap()
    {
        var payload = ProtocolTestData.BuildPacket(
            PacketId.CarStatus,
            UdpPacketConstants.CarStatusBodySizeByFormat[Format26],
            WriteCarStatus26Body,
            Format26);
        var parser = new CarStatusPacketParser();

        var parsed = parser.TryParse(payload.AsMemory(PacketHeader.Size), Format26, out var packet, out var error);

        Assert.True(parsed);
        Assert.Null(error);
        Assert.Equal(24, packet.Cars.Length);

        var first = packet.Cars[0];
        Assert.Equal((byte)2, first.TractionControl);
        Assert.Equal(2000f, first.ErsHarvestedThisLapMguh, precision: 3);
        // F1 26：新增 ErsHarvestedLimitPerLap。
        Assert.Equal(2500f, first.ErsHarvestedLimitPerLap, precision: 3);
        Assert.Equal(1500f, first.ErsDeployedThisLap, precision: 3);
        Assert.False(first.NetworkPaused);
    }

    [Fact]
    public void CarStatus_Format2026_WrongBodyLengthFails()
    {
        AssertWrongLengthFails(PacketId.CarStatus, new CarStatusPacketParser(), UdpPacketConstants.CarStatusBodySizeByFormat[Format26]);
    }

    [Fact]
    public void Participants_Format2026_Parses24ParticipantsWithUInt16Ids()
    {
        var payload = ProtocolTestData.BuildPacket(
            PacketId.Participants,
            UdpPacketConstants.ParticipantsBodySizeByFormat[Format26],
            WriteParticipants26Body,
            Format26);
        var parser = new ParticipantsPacketParser();

        var parsed = parser.TryParse(payload.AsMemory(PacketHeader.Size), Format26, out var packet, out var error);

        Assert.True(parsed);
        Assert.Null(error);
        Assert.Equal((byte)24, packet.NumActiveCars);
        Assert.Equal(24, packet.Participants.Length);

        var first = packet.Participants[0];
        // F1 26：DriverId/NetworkId/TeamId 读 uint16（0x0123 = 291）。
        Assert.Equal((ushort)0x0123, first.DriverId);
        Assert.Equal((ushort)0x0456, first.NetworkId);
        Assert.Equal((ushort)0x0789, first.TeamId);
        Assert.Equal("Driver 0", first.Name);
        Assert.Equal((byte)44, first.RaceNumber);
        Assert.Equal((byte)3, first.LiveryColours[0].Red);
    }

    [Fact]
    public void Participants_Format2026_WrongBodyLengthFails()
    {
        AssertWrongLengthFails(PacketId.Participants, new ParticipantsPacketParser(), UdpPacketConstants.ParticipantsBodySizeByFormat[Format26]);
    }

    [Fact]
    public void LapPositions_Format2026_Parses24PositionsPerLap()
    {
        var payload = ProtocolTestData.BuildPacket(
            PacketId.LapPositions,
            UdpPacketConstants.LapPositionsBodySizeByFormat[Format26],
            body =>
            {
                body[0] = 1; // numLaps
                body[1] = 1; // lapStart
                body[2 + 0] = 3;  // lap 1, car 0
                body[2 + 23] = 9; // lap 1, car 23
            },
            Format26);
        var parser = new LapPositionsPacketParser();

        var parsed = parser.TryParse(payload.AsMemory(PacketHeader.Size), Format26, out var packet, out var error);

        Assert.True(parsed);
        Assert.Null(error);
        Assert.Equal((byte)1, packet.NumLaps);
        Assert.Equal((byte)1, packet.LapStart);
        Assert.Equal(24, packet.PositionForVehicleIndexByLap[0].Length);
        Assert.Equal((byte)3, packet.PositionForVehicleIndexByLap[0][0]);
        Assert.Equal((byte)9, packet.PositionForVehicleIndexByLap[0][23]);
    }

    [Fact]
    public void LapPositions_Format2026_WrongBodyLengthFails()
    {
        AssertWrongLengthFails(PacketId.LapPositions, new LapPositionsPacketParser(), UdpPacketConstants.LapPositionsBodySizeByFormat[Format26]);
    }

    [Fact]
    public void Event_Format2026_CollisionDetailIncludesSeverity()
    {
        var payload = ProtocolTestData.BuildPacket(
            PacketId.Event,
            UdpPacketConstants.EventBodySize,
            body =>
            {
                Encoding.ASCII.GetBytes("COLL").CopyTo(body);
                body[4] = 1; // vehicle1
                body[5] = 2; // vehicle2
                body[6] = 5; // severity（26 读 3 字节）
            },
            Format26);
        var parser = new EventPacketParser();

        var parsed = parser.TryParse(payload.AsMemory(PacketHeader.Size), Format26, out var packet, out var error);

        Assert.True(parsed);
        Assert.Null(error);
        Assert.Equal(EventCode.Collision, packet.Code);
        var detail = Assert.IsType<CollisionEventDetail>(packet.Detail);
        Assert.Equal((byte)1, detail.Vehicle1Index);
        Assert.Equal((byte)2, detail.Vehicle2Index);
        Assert.Equal((byte)5, detail.Severity);
    }

    [Fact]
    public void Event_Format2025_CollisionDetailSeverityDefaultsToZero()
    {
        var payload = ProtocolTestData.BuildPacket(
            PacketId.Event,
            UdpPacketConstants.EventBodySize,
            body =>
            {
                Encoding.ASCII.GetBytes("COLL").CopyTo(body);
                body[4] = 1; // vehicle1
                body[5] = 2; // vehicle2
                body[6] = 5; // 25 布局忽略该字节
            },
            Format25);
        var parser = new EventPacketParser();

        var parsed = parser.TryParse(payload.AsMemory(PacketHeader.Size), Format25, out var packet, out var error);

        Assert.True(parsed);
        Assert.Null(error);
        var detail = Assert.IsType<CollisionEventDetail>(packet.Detail);
        Assert.Equal((byte)1, detail.Vehicle1Index);
        Assert.Equal((byte)2, detail.Vehicle2Index);
        Assert.Equal((byte)0, detail.Severity);
    }

    [Fact]
    public void Event_Format2026_WrongBodyLengthFails()
    {
        AssertWrongLengthFails(PacketId.Event, new EventPacketParser(), UdpPacketConstants.EventBodySize);
    }

    [Fact]
    public void CarTelemetry2_Format2026_Parses24CarsWithAeroFields()
    {
        var payload = ProtocolTestData.BuildPacket(
            PacketId.CarTelemetry2,
            UdpPacketConstants.CarTelemetry2BodySizeByFormat[Format26],
            WriteCarTelemetry2Body,
            Format26);
        var parser = new CarTelemetry2PacketParser();

        var parsed = parser.TryParse(payload.AsMemory(PacketHeader.Size), Format26, out var packet, out var error);

        Assert.True(parsed);
        Assert.Null(error);
        Assert.Equal(24, packet.Cars.Length);

        var first = packet.Cars[0];
        Assert.Equal((byte)1, first.ActiveAeroMode);
        Assert.Equal((byte)1, first.ActiveAeroAvailable);
        Assert.Equal((ushort)1500, first.ActiveAeroActivationDistance);
        Assert.Equal((byte)1, first.OvertakeAvailable);
        Assert.Equal((byte)0, first.OvertakeActive);
        Assert.Equal((ushort)3200, first.OvertakeActivationDistance);
        Assert.Equal((byte)1, first.Regulations2026);
        Assert.Equal((byte)0, first.DrivingWrongWay);
    }

    [Fact]
    public void CarTelemetry2_Format2025_NotRegisteredFails()
    {
        var payload = ProtocolTestData.BuildPacket(
            PacketId.CarTelemetry2,
            UdpPacketConstants.CarTelemetry2BodySizeByFormat[Format26],
            _ => { },
            Format25);
        var parser = new CarTelemetry2PacketParser();

        var parsed = parser.TryParse(payload.AsMemory(PacketHeader.Size), Format25, out var packet, out var error);

        Assert.False(parsed);
        Assert.NotNull(error);
    }

    [Fact]
    public void CarTelemetry2_Format2026_WrongBodyLengthFails()
    {
        AssertWrongLengthFails(PacketId.CarTelemetry2, new CarTelemetry2PacketParser(), UdpPacketConstants.CarTelemetry2BodySizeByFormat[Format26]);
    }

    [Fact]
    public void LapData_Format2026_Parses24Cars()
    {
        var payload = ProtocolTestData.BuildPacket(
            PacketId.LapData,
            UdpPacketConstants.LapDataBodySizeByFormat[Format26],
            body =>
            {
                var offset = 0;
                ProtocolTestData.WriteUInt32(body, ref offset, 42); // car 0 lastLapTime
                offset = 57 * 24; // 24 辆 × 57 字节后为尾部字段
                ProtocolTestData.WriteByte(body, ref offset, 1); // timeTrialPersonalBestCarIndex
                ProtocolTestData.WriteByte(body, ref offset, 2); // timeTrialRivalCarIndex
            },
            Format26);
        var parser = new LapDataPacketParser();

        var parsed = parser.TryParse(payload.AsMemory(PacketHeader.Size), Format26, out var packet, out var error);

        Assert.True(parsed);
        Assert.Null(error);
        Assert.Equal(24, packet.Cars.Length);
        Assert.Equal(42u, packet.Cars[0].LastLapTimeInMs);
        Assert.Equal((byte)1, packet.TimeTrialPersonalBestCarIndex);
        Assert.Equal((byte)2, packet.TimeTrialRivalCarIndex);
    }

    [Fact]
    public void LapData_Format2026_WrongBodyLengthFails()
    {
        AssertWrongLengthFails(PacketId.LapData, new LapDataPacketParser(), UdpPacketConstants.LapDataBodySizeByFormat[Format26]);
    }

    [Fact]
    public void FinalClassification_Format2026_Parses24Cars()
    {
        var payload = ProtocolTestData.BuildPacket(
            PacketId.FinalClassification,
            UdpPacketConstants.FinalClassificationBodySizeByFormat[Format26],
            body =>
            {
                body[0] = 24; // numCars
                body[1] = 3;  // car 0 position
            },
            Format26);
        var parser = new FinalClassificationPacketParser();

        var parsed = parser.TryParse(payload.AsMemory(PacketHeader.Size), Format26, out var packet, out var error);

        Assert.True(parsed);
        Assert.Null(error);
        Assert.Equal((byte)24, packet.NumCars);
        Assert.Equal(24, packet.Cars.Length);
        Assert.Equal((byte)3, packet.Cars[0].Position);
        Assert.Equal(8, packet.Cars[0].TyreStintsActual.Length);
    }

    [Fact]
    public void FinalClassification_Format2026_WrongBodyLengthFails()
    {
        AssertWrongLengthFails(PacketId.FinalClassification, new FinalClassificationPacketParser(), UdpPacketConstants.FinalClassificationBodySizeByFormat[Format26]);
    }

    [Fact]
    public void CarDamage_Format2026_Parses24Cars()
    {
        var payload = ProtocolTestData.BuildPacket(
            PacketId.CarDamage,
            UdpPacketConstants.CarDamageBodySizeByFormat[Format26],
            body =>
            {
                var offset = 0;
                ProtocolTestData.WriteFloat(body, ref offset, 0.8f); // car 0 tyreWear.RearLeft
            },
            Format26);
        var parser = new CarDamagePacketParser();

        var parsed = parser.TryParse(payload.AsMemory(PacketHeader.Size), Format26, out var packet, out var error);

        Assert.True(parsed);
        Assert.Null(error);
        Assert.Equal(24, packet.Cars.Length);
        Assert.Equal(0.8f, packet.Cars[0].TyreWear.RearLeft, precision: 3);
    }

    [Fact]
    public void CarDamage_Format2026_WrongBodyLengthFails()
    {
        AssertWrongLengthFails(PacketId.CarDamage, new CarDamagePacketParser(), UdpPacketConstants.CarDamageBodySizeByFormat[Format26]);
    }

    /// <summary>校验 26 格式下包体长度错误时 TryParse 失败。</summary>
    private static void AssertWrongLengthFails<TPacket>(
        PacketId packetId,
        FixedSizePacketParser<TPacket> parser,
        int expectedBodySize)
    {
        var payload = ProtocolTestData.BuildPacket(packetId, expectedBodySize + 1, _ => { }, Format26);

        var parsed = parser.TryParse(payload.AsMemory(PacketHeader.Size), Format26, out _, out var error);

        Assert.False(parsed);
        Assert.NotNull(error);
    }

    private static void WriteMotion26Body(Span<byte> body)
    {
        var offset = 0;
        // 第一辆车写入可辨识值，其余 23 辆保持零。
        ProtocolTestData.WriteFloat(body, ref offset, 1.5f);  // worldPosition.X
        ProtocolTestData.WriteFloat(body, ref offset, 2.5f);  // worldPosition.Y
        ProtocolTestData.WriteFloat(body, ref offset, 3.5f);  // worldPosition.Z
        ProtocolTestData.WriteFloat(body, ref offset, 4.5f);  // worldVelocity.X
        ProtocolTestData.WriteFloat(body, ref offset, 5.5f);  // worldVelocity.Y
        ProtocolTestData.WriteFloat(body, ref offset, 6.5f);  // worldVelocity.Z
        ProtocolTestData.WriteInt16(body, ref offset, 100);   // forwardDirection.X
        ProtocolTestData.WriteInt16(body, ref offset, 200);   // forwardDirection.Y
        ProtocolTestData.WriteInt16(body, ref offset, 300);   // forwardDirection.Z
        ProtocolTestData.WriteInt16(body, ref offset, 400);   // rightDirection.X
        ProtocolTestData.WriteInt16(body, ref offset, 500);   // rightDirection.Y
        ProtocolTestData.WriteInt16(body, ref offset, 600);   // rightDirection.Z
        ProtocolTestData.WriteInt16(body, ref offset, 12345); // gForceLateral int16 → 12.345
        ProtocolTestData.WriteInt16(body, ref offset, -3250); // gForceLongitudinal int16 → -3.25
        ProtocolTestData.WriteInt16(body, ref offset, 500);   // gForceVertical int16 → 0.5
        ProtocolTestData.WriteFloat(body, ref offset, 7.5f);  // yaw
        ProtocolTestData.WriteFloat(body, ref offset, 8.5f);  // pitch
        ProtocolTestData.WriteFloat(body, ref offset, 9.5f);  // roll
    }

    private static void WriteSession26Body(Span<byte> body)
    {
        var offset = 0;
        ProtocolTestData.WriteByte(body, ref offset, 1);      // weather
        offset = 4;                                            // 跳过 trackTemperature/airTemperature/totalLaps
        ProtocolTestData.WriteUInt16(body, ref offset, 5000); // trackLength
        // 跳过 F1 25 包体剩余字段（全零），从 26 尾部新增字段开始。
        offset = UdpPacketConstants.SessionBodySizeByFormat[Format25];

        ProtocolTestData.WriteByte(body, ref offset, 3);        // activeAeroTrackStatus
        ProtocolTestData.WriteByte(body, ref offset, 2);        // numActiveAeroZonesFull
        ProtocolTestData.WriteFloat(body, ref offset, 100f);    // zone0.start
        ProtocolTestData.WriteFloat(body, ref offset, 150f);    // zone0.end
        ProtocolTestData.WriteFloat(body, ref offset, 200f);    // zone1.start
        ProtocolTestData.WriteFloat(body, ref offset, 250f);    // zone1.end
        offset += 6 * 8;                                        // zone2..7 全零
        ProtocolTestData.WriteByte(body, ref offset, 4);        // numActiveAeroZonesPartial
        ProtocolTestData.WriteFloat(body, ref offset, 300f);    // partial zone0.start
        ProtocolTestData.WriteFloat(body, ref offset, 350f);    // partial zone0.end
        offset += 7 * 8;                                        // partial zone1..7 全零
        ProtocolTestData.WriteByte(body, ref offset, 2);        // numDrsZones
        ProtocolTestData.WriteFloat(body, ref offset, 400f);    // drs zone0.start
        ProtocolTestData.WriteFloat(body, ref offset, 450f);    // drs zone0.end
        offset += 3 * 8;                                        // drs zone1..3 全零
        ProtocolTestData.WriteFloat(body, ref offset, 0.75f);   // startReactionTime
        ProtocolTestData.WriteByte(body, ref offset, 1);        // antiLockBrakesAssist
        ProtocolTestData.WriteByte(body, ref offset, 2);        // tractionControlAssist
        ProtocolTestData.WriteByte(body, ref offset, 3);        // dynamicRacingLineHiVis
        ProtocolTestData.WriteByte(body, ref offset, 4);        // dynamicRacingLineColourBlind
        ProtocolTestData.WriteByte(body, ref offset, 5);        // recurringRewindPrompt
    }

    private static void WriteCarTelemetry26Body(Span<byte> body)
    {
        var offset = 0;
        ProtocolTestData.WriteUInt16(body, ref offset, 321);
        ProtocolTestData.WriteFloat(body, ref offset, 0.75f);
        ProtocolTestData.WriteFloat(body, ref offset, -0.25f);
        ProtocolTestData.WriteFloat(body, ref offset, 0.1f);
        ProtocolTestData.WriteByte(body, ref offset, 50);
        ProtocolTestData.WriteSByte(body, ref offset, 7);
        ProtocolTestData.WriteUInt16(body, ref offset, 12345);
        ProtocolTestData.WriteByte(body, ref offset, 1);
        ProtocolTestData.WriteByte(body, ref offset, 88);
        ProtocolTestData.WriteUInt16(body, ref offset, 0x7FFF);
        ProtocolTestData.WriteUInt16(body, ref offset, 100);
        ProtocolTestData.WriteUInt16(body, ref offset, 101);
        ProtocolTestData.WriteUInt16(body, ref offset, 102);
        ProtocolTestData.WriteUInt16(body, ref offset, 103);
        ProtocolTestData.WriteByte(body, ref offset, 90);
        ProtocolTestData.WriteByte(body, ref offset, 91);
        ProtocolTestData.WriteByte(body, ref offset, 92);
        ProtocolTestData.WriteByte(body, ref offset, 93);
        ProtocolTestData.WriteByte(body, ref offset, 80);
        ProtocolTestData.WriteByte(body, ref offset, 81);
        ProtocolTestData.WriteByte(body, ref offset, 82);
        ProtocolTestData.WriteByte(body, ref offset, 83);
        // F1 26：engineTemperature 为 1 字节（0x6E = 110），F1 25 为 uint16。
        ProtocolTestData.WriteByte(body, ref offset, 0x6E);
        ProtocolTestData.WriteFloat(body, ref offset, 21.1f);
        ProtocolTestData.WriteFloat(body, ref offset, 21.2f);
        ProtocolTestData.WriteFloat(body, ref offset, 21.3f);
        ProtocolTestData.WriteFloat(body, ref offset, 21.4f);
        ProtocolTestData.WriteByte(body, ref offset, 1);
        ProtocolTestData.WriteByte(body, ref offset, 2);
        ProtocolTestData.WriteByte(body, ref offset, 3);
        ProtocolTestData.WriteByte(body, ref offset, 4);

        // 尾部字段（紧随 24 辆 × 59 字节之后）。
        offset = 59 * 24;
        ProtocolTestData.WriteByte(body, ref offset, 1);
        ProtocolTestData.WriteByte(body, ref offset, 0);
        ProtocolTestData.WriteSByte(body, ref offset, 8);
    }

    private static void WriteCarStatus26Body(Span<byte> body)
    {
        var offset = 0;
        ProtocolTestData.WriteByte(body, ref offset, 2);      // tractionControl
        ProtocolTestData.WriteByte(body, ref offset, 1);      // antiLockBrakes
        ProtocolTestData.WriteByte(body, ref offset, 3);      // fuelMix
        ProtocolTestData.WriteByte(body, ref offset, 58);     // frontBrakeBias
        ProtocolTestData.WriteByte(body, ref offset, 1);      // pitLimiterStatus
        ProtocolTestData.WriteFloat(body, ref offset, 5.5f);  // fuelInTank
        ProtocolTestData.WriteFloat(body, ref offset, 110f);  // fuelCapacity
        ProtocolTestData.WriteFloat(body, ref offset, 2.25f); // fuelRemainingLaps
        ProtocolTestData.WriteUInt16(body, ref offset, 15000);
        ProtocolTestData.WriteUInt16(body, ref offset, 4000);
        ProtocolTestData.WriteByte(body, ref offset, 8);
        ProtocolTestData.WriteByte(body, ref offset, 1);
        ProtocolTestData.WriteUInt16(body, ref offset, 150);
        ProtocolTestData.WriteByte(body, ref offset, 16);
        ProtocolTestData.WriteByte(body, ref offset, 17);
        ProtocolTestData.WriteByte(body, ref offset, 9);
        ProtocolTestData.WriteSByte(body, ref offset, -1);
        ProtocolTestData.WriteFloat(body, ref offset, 123.4f);
        ProtocolTestData.WriteFloat(body, ref offset, 56.7f);
        ProtocolTestData.WriteFloat(body, ref offset, 500000f);
        ProtocolTestData.WriteByte(body, ref offset, 3);
        ProtocolTestData.WriteFloat(body, ref offset, 1000f);
        ProtocolTestData.WriteFloat(body, ref offset, 2000f);
        // F1 26：新增 ErsHarvestedLimitPerLap。
        ProtocolTestData.WriteFloat(body, ref offset, 2500f);
        ProtocolTestData.WriteFloat(body, ref offset, 1500f);
        ProtocolTestData.WriteByte(body, ref offset, 0);
    }

    private static void WriteParticipants26Body(Span<byte> body)
    {
        var offset = 0;
        ProtocolTestData.WriteByte(body, ref offset, 24); // numActiveCars

        // 第一辆车写入可辨识值，其余 23 辆保持零。
        ProtocolTestData.WriteByte(body, ref offset, 0);           // isAiControlled
        ProtocolTestData.WriteUInt16(body, ref offset, 0x0123);    // driverId
        ProtocolTestData.WriteUInt16(body, ref offset, 0x0456);    // networkId
        ProtocolTestData.WriteUInt16(body, ref offset, 0x0789);    // teamId
        ProtocolTestData.WriteByte(body, ref offset, 0);           // isMyTeam
        ProtocolTestData.WriteByte(body, ref offset, 44);          // raceNumber
        ProtocolTestData.WriteByte(body, ref offset, 30);          // nationality
        var nameBytes = Encoding.ASCII.GetBytes("Driver 0");
        nameBytes.CopyTo(body.Slice(offset, nameBytes.Length));
        offset += 32; // name 固定 32 字节（含尾部零填充）
        ProtocolTestData.WriteByte(body, ref offset, 0);           // yourTelemetry
        ProtocolTestData.WriteByte(body, ref offset, 1);           // showOnlineNames
        ProtocolTestData.WriteUInt16(body, ref offset, 0);         // techLevel
        ProtocolTestData.WriteByte(body, ref offset, 0);           // platform
        ProtocolTestData.WriteByte(body, ref offset, 1);           // numColours
        ProtocolTestData.WriteByte(body, ref offset, 3);           // liveryColours[0].red
        ProtocolTestData.WriteByte(body, ref offset, 4);           // liveryColours[0].green
        ProtocolTestData.WriteByte(body, ref offset, 5);           // liveryColours[0].blue
    }

    private static void WriteCarTelemetry2Body(Span<byte> body)
    {
        var offset = 0;
        ProtocolTestData.WriteByte(body, ref offset, 1);       // activeAeroMode
        ProtocolTestData.WriteByte(body, ref offset, 1);       // activeAeroAvailable
        ProtocolTestData.WriteUInt16(body, ref offset, 1500);  // activeAeroActivationDistance
        ProtocolTestData.WriteByte(body, ref offset, 1);       // overtakeAvailable
        ProtocolTestData.WriteByte(body, ref offset, 0);       // overtakeActive
        ProtocolTestData.WriteUInt16(body, ref offset, 3200);  // overtakeActivationDistance
        ProtocolTestData.WriteByte(body, ref offset, 1);       // regulations2026
        ProtocolTestData.WriteByte(body, ref offset, 0);       // drivingWrongWay
    }
}
