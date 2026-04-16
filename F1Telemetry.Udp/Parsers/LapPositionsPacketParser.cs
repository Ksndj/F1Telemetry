using F1Telemetry.Udp.Packets;

namespace F1Telemetry.Udp.Parsers;

public sealed class LapPositionsPacketParser : FixedSizePacketParser<LapPositionsPacket>
{
    public LapPositionsPacketParser()
        : base(nameof(LapPositionsPacket), UdpPacketConstants.LapPositionsBodySize)
    {
    }

    protected override LapPositionsPacket Parse(ref PacketBufferReader reader)
    {
        var positions = new byte[UdpPacketConstants.MaxLapPositionsLaps][];
        var numLaps = reader.ReadByte();
        var lapStart = reader.ReadByte();

        for (var lapIndex = 0; lapIndex < positions.Length; lapIndex++)
        {
            positions[lapIndex] = reader.ReadBytes(UdpPacketConstants.MaxCarsInSession);
        }

        return new LapPositionsPacket(
            NumLaps: numLaps,
            LapStart: lapStart,
            PositionForVehicleIndexByLap: positions);
    }
}
