using F1Telemetry.Udp.Packets;

namespace F1Telemetry.Udp.Parsers;

public sealed class SessionHistoryPacketParser : FixedSizePacketParser<SessionHistoryPacket>
{
    public SessionHistoryPacketParser()
        : base(nameof(SessionHistoryPacket), UdpPacketConstants.SessionHistoryBodySize)
    {
    }

    protected override SessionHistoryPacket Parse(ref PacketBufferReader reader)
    {
        var lapHistory = new LapHistoryData[UdpPacketConstants.MaxSessionHistoryLaps];
        var tyreStints = new TyreStintHistoryData[UdpPacketConstants.MaxSessionHistoryTyreStints];

        var carIndex = reader.ReadByte();
        var numLaps = reader.ReadByte();
        var numTyreStints = reader.ReadByte();
        var bestLapTimeLapNumber = reader.ReadByte();
        var bestSector1LapNumber = reader.ReadByte();
        var bestSector2LapNumber = reader.ReadByte();
        var bestSector3LapNumber = reader.ReadByte();

        for (var index = 0; index < lapHistory.Length; index++)
        {
            lapHistory[index] = new LapHistoryData(
                LapTimeInMs: reader.ReadUInt32(),
                Sector1TimeMsPart: reader.ReadUInt16(),
                Sector1TimeMinutesPart: reader.ReadByte(),
                Sector2TimeMsPart: reader.ReadUInt16(),
                Sector2TimeMinutesPart: reader.ReadByte(),
                Sector3TimeMsPart: reader.ReadUInt16(),
                Sector3TimeMinutesPart: reader.ReadByte(),
                LapValidBitFlags: reader.ReadByte());
        }

        for (var index = 0; index < tyreStints.Length; index++)
        {
            tyreStints[index] = new TyreStintHistoryData(
                EndLap: reader.ReadByte(),
                ActualTyreCompound: reader.ReadByte(),
                VisualTyreCompound: reader.ReadByte());
        }

        return new SessionHistoryPacket(
            CarIndex: carIndex,
            NumLaps: numLaps,
            NumTyreStints: numTyreStints,
            BestLapTimeLapNumber: bestLapTimeLapNumber,
            BestSector1LapNumber: bestSector1LapNumber,
            BestSector2LapNumber: bestSector2LapNumber,
            BestSector3LapNumber: bestSector3LapNumber,
            LapHistory: lapHistory,
            TyreStints: tyreStints);
    }
}
