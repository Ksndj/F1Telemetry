using F1Telemetry.Udp.Packets;
using F1Telemetry.Udp.Parsers;
using Xunit;

namespace F1Telemetry.Tests;

public sealed class CarTelemetryPacketParserTests
{
    private const int BodySize = 1352 - PacketHeader.Size;

    [Fact]
    public void CarTelemetryPacketParser_ParsesPacketAndKeepsProtocolFields()
    {
        var payload = ProtocolTestData.BuildPacket(PacketId.CarTelemetry, BodySize, WriteCarTelemetryBody);
        var parser = new CarTelemetryPacketParser();

        var parsed = parser.TryParse(payload.AsMemory(PacketHeader.Size), out var packet, out var error);

        Assert.True(parsed);
        Assert.Null(error);
        Assert.Equal(22, packet.Cars.Length);
        Assert.Equal((byte)255, packet.MfdPanelIndex);
        Assert.Equal((byte)0, packet.MfdPanelIndexSecondaryPlayer);
        Assert.Equal((sbyte)8, packet.SuggestedGear);

        var firstCar = packet.Cars[0];
        Assert.Equal((ushort)321, firstCar.Speed);
        Assert.Equal(0.75f, firstCar.Throttle, precision: 3);
        Assert.Equal(-0.25f, firstCar.Steer, precision: 3);
        Assert.Equal(0.1f, firstCar.Brake, precision: 3);
        Assert.Equal((byte)50, firstCar.Clutch);
        Assert.Equal((sbyte)7, firstCar.Gear);
        Assert.Equal((ushort)12345, firstCar.EngineRpm);
        Assert.True(firstCar.Drs);
        Assert.Equal((byte)88, firstCar.RevLightsPercent);
        Assert.Equal((ushort)0x7FFF, firstCar.RevLightsBitValue);
        Assert.Equal((ushort)100, firstCar.BrakesTemperature.RearLeft);
        Assert.Equal((ushort)103, firstCar.BrakesTemperature.FrontRight);
        Assert.Equal((byte)90, firstCar.TyresSurfaceTemperature.RearLeft);
        Assert.Equal((byte)83, firstCar.TyresInnerTemperature.FrontRight);
        Assert.Equal((ushort)110, firstCar.EngineTemperature);
        Assert.Equal(21.1f, firstCar.TyresPressure.RearLeft, precision: 3);
        Assert.Equal(21.4f, firstCar.TyresPressure.FrontRight, precision: 3);
        Assert.Equal((byte)1, firstCar.SurfaceType.RearLeft);
        Assert.Equal((byte)4, firstCar.SurfaceType.FrontRight);
    }

    private static void WriteCarTelemetryBody(Span<byte> body)
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

        ProtocolTestData.WriteUInt16(body, ref offset, 110);

        ProtocolTestData.WriteFloat(body, ref offset, 21.1f);
        ProtocolTestData.WriteFloat(body, ref offset, 21.2f);
        ProtocolTestData.WriteFloat(body, ref offset, 21.3f);
        ProtocolTestData.WriteFloat(body, ref offset, 21.4f);

        ProtocolTestData.WriteByte(body, ref offset, 1);
        ProtocolTestData.WriteByte(body, ref offset, 2);
        ProtocolTestData.WriteByte(body, ref offset, 3);
        ProtocolTestData.WriteByte(body, ref offset, 4);

        offset = 60 * 22;
        ProtocolTestData.WriteByte(body, ref offset, 255);
        ProtocolTestData.WriteByte(body, ref offset, 0);
        ProtocolTestData.WriteSByte(body, ref offset, 8);
    }
}
