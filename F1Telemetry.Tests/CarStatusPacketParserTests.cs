using F1Telemetry.Udp.Packets;
using F1Telemetry.Udp.Parsers;
using Xunit;

namespace F1Telemetry.Tests;

public sealed class CarStatusPacketParserTests
{
    private const int BodySize = 1239 - PacketHeader.Size;

    [Fact]
    public void CarStatusPacketParser_ParsesPacketAndKeepsProtocolFields()
    {
        var payload = ProtocolTestData.BuildPacket(PacketId.CarStatus, BodySize, WriteCarStatusBody);
        var parser = new CarStatusPacketParser();

        var parsed = parser.TryParse(payload.AsMemory(PacketHeader.Size), out var packet, out var error);

        Assert.True(parsed);
        Assert.Null(error);
        Assert.Equal(22, packet.Cars.Length);

        var firstCar = packet.Cars[0];
        Assert.Equal((byte)2, firstCar.TractionControl);
        Assert.True(firstCar.AntiLockBrakes);
        Assert.Equal((byte)3, firstCar.FuelMix);
        Assert.Equal((byte)58, firstCar.FrontBrakeBias);
        Assert.True(firstCar.PitLimiterStatus);
        Assert.Equal(5.5f, firstCar.FuelInTank, precision: 3);
        Assert.Equal(110f, firstCar.FuelCapacity, precision: 3);
        Assert.Equal(2.25f, firstCar.FuelRemainingLaps, precision: 3);
        Assert.Equal((ushort)15000, firstCar.MaxRpm);
        Assert.Equal((ushort)4000, firstCar.IdleRpm);
        Assert.Equal((byte)8, firstCar.MaxGears);
        Assert.True(firstCar.DrsAllowed);
        Assert.Equal((ushort)150, firstCar.DrsActivationDistance);
        Assert.Equal((byte)16, firstCar.ActualTyreCompound);
        Assert.Equal((byte)17, firstCar.VisualTyreCompound);
        Assert.Equal((byte)9, firstCar.TyresAgeLaps);
        Assert.Equal((sbyte)-1, firstCar.VehicleFiaFlags);
        Assert.Equal(123.4f, firstCar.EnginePowerIce, precision: 3);
        Assert.Equal(56.7f, firstCar.EnginePowerMguk, precision: 3);
        Assert.Equal(500000f, firstCar.ErsStoreEnergy, precision: 3);
        Assert.Equal((byte)3, firstCar.ErsDeployMode);
        Assert.Equal(1000f, firstCar.ErsHarvestedThisLapMguk, precision: 3);
        Assert.Equal(2000f, firstCar.ErsHarvestedThisLapMguh, precision: 3);
        Assert.Equal(1500f, firstCar.ErsDeployedThisLap, precision: 3);
        Assert.False(firstCar.NetworkPaused);
    }

    private static void WriteCarStatusBody(Span<byte> body)
    {
        var offset = 0;
        ProtocolTestData.WriteByte(body, ref offset, 2);
        ProtocolTestData.WriteByte(body, ref offset, 1);
        ProtocolTestData.WriteByte(body, ref offset, 3);
        ProtocolTestData.WriteByte(body, ref offset, 58);
        ProtocolTestData.WriteByte(body, ref offset, 1);
        ProtocolTestData.WriteFloat(body, ref offset, 5.5f);
        ProtocolTestData.WriteFloat(body, ref offset, 110f);
        ProtocolTestData.WriteFloat(body, ref offset, 2.25f);
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
        ProtocolTestData.WriteFloat(body, ref offset, 1500f);
        ProtocolTestData.WriteByte(body, ref offset, 0);
    }
}
