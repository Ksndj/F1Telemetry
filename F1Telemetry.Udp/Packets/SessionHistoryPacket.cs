namespace F1Telemetry.Udp.Packets;

public sealed record SessionHistoryPacket(
    byte CarIndex,
    byte NumLaps,
    byte NumTyreStints,
    byte BestLapTimeLapNumber,
    byte BestSector1LapNumber,
    byte BestSector2LapNumber,
    byte BestSector3LapNumber,
    LapHistoryData[] LapHistory,
    TyreStintHistoryData[] TyreStints) : IUdpPacket;

public sealed record LapHistoryData(
    uint LapTimeInMs,
    ushort Sector1TimeMsPart,
    byte Sector1TimeMinutesPart,
    ushort Sector2TimeMsPart,
    byte Sector2TimeMinutesPart,
    ushort Sector3TimeMsPart,
    byte Sector3TimeMinutesPart,
    byte LapValidBitFlags)
{
    public bool IsLapValid => (LapValidBitFlags & 0x01) != 0;

    public bool IsSector1Valid => (LapValidBitFlags & 0x02) != 0;

    public bool IsSector2Valid => (LapValidBitFlags & 0x04) != 0;

    public bool IsSector3Valid => (LapValidBitFlags & 0x08) != 0;
}

public sealed record TyreStintHistoryData(
    byte EndLap,
    byte ActualTyreCompound,
    byte VisualTyreCompound);
