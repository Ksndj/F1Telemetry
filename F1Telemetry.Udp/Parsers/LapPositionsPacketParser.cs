using F1Telemetry.Udp.Packets;

namespace F1Telemetry.Udp.Parsers;

public sealed class LapPositionsPacketParser : FixedSizePacketParser<LapPositionsPacket>
{
    public LapPositionsPacketParser()
        : base(nameof(LapPositionsPacket), UdpPacketConstants.LapPositionsBodySizeByFormat)
    {
    }

    protected override LapPositionsPacket Parse(ref PacketBufferReader reader, ushort packetFormat)
    {
        var positions = new byte[UdpPacketConstants.MaxLapPositionsLaps][];
        var numLaps = reader.ReadByte();
        var lapStart = reader.ReadByte();

        // 每行位置数量按协议格式决定（25→22 / 26→24）。
        var carsPerLap = UdpPacketConstants.GetMaxCarsInSession(packetFormat);

        for (var lapIndex = 0; lapIndex < positions.Length; lapIndex++)
        {
            positions[lapIndex] = reader.ReadBytes(carsPerLap);
        }

        return new LapPositionsPacket(
            NumLaps: numLaps,
            LapStart: lapStart,
            PositionForVehicleIndexByLap: positions);
    }
}
