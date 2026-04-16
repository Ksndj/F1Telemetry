namespace F1Telemetry.Udp.Packets;

public sealed record FinalClassificationPacket(
    byte NumCars,
    FinalClassificationData[] Cars) : IUdpPacket;

public sealed record FinalClassificationData(
    byte Position,
    byte NumLaps,
    byte GridPosition,
    byte Points,
    byte NumPitStops,
    byte ResultStatus,
    byte ResultReason,
    uint BestLapTimeInMs,
    double TotalRaceTime,
    byte PenaltiesTime,
    byte NumPenalties,
    byte NumTyreStints,
    byte[] TyreStintsActual,
    byte[] TyreStintsVisual,
    byte[] TyreStintsEndLaps);
