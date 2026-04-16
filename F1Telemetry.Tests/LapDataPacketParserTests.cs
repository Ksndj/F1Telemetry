using F1Telemetry.Udp.Packets;
using F1Telemetry.Udp.Parsers;
using Xunit;

namespace F1Telemetry.Tests;

public sealed class LapDataPacketParserTests
{
    private const int BodySize = 1285 - PacketHeader.Size;

    [Fact]
    public void LapDataPacketParser_ParsesPacketAndKeepsProtocolFields()
    {
        var payload = ProtocolTestData.BuildPacket(PacketId.LapData, BodySize, WriteLapDataBody);
        var parser = new LapDataPacketParser();

        var parsed = parser.TryParse(payload.AsMemory(PacketHeader.Size), out var packet, out var error);

        Assert.True(parsed);
        Assert.Null(error);
        Assert.Equal(22, packet.Cars.Length);
        Assert.Equal((byte)1, packet.TimeTrialPersonalBestCarIndex);
        Assert.Equal((byte)2, packet.TimeTrialRivalCarIndex);

        var firstCar = packet.Cars[0];
        Assert.Equal(12u, firstCar.LastLapTimeInMs);
        Assert.Equal(34u, firstCar.CurrentLapTimeInMs);
        Assert.Equal((ushort)56, firstCar.Sector1TimeInMs);
        Assert.Equal((byte)7, firstCar.Sector1TimeMinutes);
        Assert.Equal((ushort)78, firstCar.Sector2TimeInMs);
        Assert.Equal((byte)8, firstCar.Sector2TimeMinutes);
        Assert.Equal((ushort)90, firstCar.DeltaToCarInFrontInMs);
        Assert.Equal((ushort)123, firstCar.DeltaToRaceLeaderInMs);
        Assert.Equal(11.5f, firstCar.LapDistance, precision: 3);
        Assert.Equal(22.5f, firstCar.TotalDistance, precision: 3);
        Assert.Equal(33.5f, firstCar.SafetyCarDelta, precision: 3);
        Assert.Equal((byte)5, firstCar.CarPosition);
        Assert.Equal((byte)6, firstCar.CurrentLapNumber);
        Assert.Equal((byte)10, firstCar.GridPosition);
        Assert.Equal((byte)11, firstCar.DriverStatus);
        Assert.Equal((byte)12, firstCar.ResultStatus);
        Assert.True(firstCar.IsPitLaneTimerActive);
        Assert.Equal((ushort)140, firstCar.PitLaneTimeInLaneInMs);
        Assert.Equal((ushort)150, firstCar.PitStopTimerInMs);
        Assert.True(firstCar.ShouldServePitStopPenalty);
        Assert.Equal(88.75f, firstCar.SpeedTrapFastestSpeed, precision: 3);
        Assert.Equal((byte)77, firstCar.SpeedTrapFastestLap);
    }

    private static void WriteLapDataBody(Span<byte> body)
    {
        var carOffset = 0;
        ProtocolTestData.WriteUInt32(body, ref carOffset, 12);
        ProtocolTestData.WriteUInt32(body, ref carOffset, 34);
        ProtocolTestData.WriteUInt16(body, ref carOffset, 56);
        ProtocolTestData.WriteByte(body, ref carOffset, 7);
        ProtocolTestData.WriteUInt16(body, ref carOffset, 78);
        ProtocolTestData.WriteByte(body, ref carOffset, 8);
        ProtocolTestData.WriteUInt16(body, ref carOffset, 90);
        ProtocolTestData.WriteByte(body, ref carOffset, 9);
        ProtocolTestData.WriteUInt16(body, ref carOffset, 123);
        ProtocolTestData.WriteByte(body, ref carOffset, 10);
        ProtocolTestData.WriteFloat(body, ref carOffset, 11.5f);
        ProtocolTestData.WriteFloat(body, ref carOffset, 22.5f);
        ProtocolTestData.WriteFloat(body, ref carOffset, 33.5f);
        ProtocolTestData.WriteByte(body, ref carOffset, 5);
        ProtocolTestData.WriteByte(body, ref carOffset, 6);
        ProtocolTestData.WriteByte(body, ref carOffset, 1);
        ProtocolTestData.WriteByte(body, ref carOffset, 2);
        ProtocolTestData.WriteByte(body, ref carOffset, 3);
        ProtocolTestData.WriteByte(body, ref carOffset, 4);
        ProtocolTestData.WriteByte(body, ref carOffset, 5);
        ProtocolTestData.WriteByte(body, ref carOffset, 6);
        ProtocolTestData.WriteByte(body, ref carOffset, 7);
        ProtocolTestData.WriteByte(body, ref carOffset, 8);
        ProtocolTestData.WriteByte(body, ref carOffset, 9);
        ProtocolTestData.WriteByte(body, ref carOffset, 10);
        ProtocolTestData.WriteByte(body, ref carOffset, 11);
        ProtocolTestData.WriteByte(body, ref carOffset, 12);
        ProtocolTestData.WriteByte(body, ref carOffset, 1);
        ProtocolTestData.WriteUInt16(body, ref carOffset, 140);
        ProtocolTestData.WriteUInt16(body, ref carOffset, 150);
        ProtocolTestData.WriteByte(body, ref carOffset, 1);
        ProtocolTestData.WriteFloat(body, ref carOffset, 88.75f);
        ProtocolTestData.WriteByte(body, ref carOffset, 77);

        carOffset = 57 * 22;

        ProtocolTestData.WriteByte(body, ref carOffset, 1);
        ProtocolTestData.WriteByte(body, ref carOffset, 2);
    }
}
