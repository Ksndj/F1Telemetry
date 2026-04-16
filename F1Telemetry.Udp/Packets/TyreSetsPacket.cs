namespace F1Telemetry.Udp.Packets;

public sealed record TyreSetsPacket(
    byte CarIndex,
    TyreSetData[] TyreSets,
    byte FittedIndex) : IUdpPacket;

public sealed record TyreSetData(
    byte ActualTyreCompound,
    byte VisualTyreCompound,
    byte Wear,
    bool Available,
    byte RecommendedSession,
    byte LifeSpan,
    byte UsableLife,
    short LapDeltaTime,
    bool Fitted);
