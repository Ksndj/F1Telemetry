namespace F1Telemetry.Udp.Packets;

public sealed record CarStatusPacket(
    CarStatusData[] Cars) : IUdpPacket;

public sealed record CarStatusData(
    byte TractionControl,
    bool AntiLockBrakes,
    byte FuelMix,
    byte FrontBrakeBias,
    bool PitLimiterStatus,
    float FuelInTank,
    float FuelCapacity,
    float FuelRemainingLaps,
    ushort MaxRpm,
    ushort IdleRpm,
    byte MaxGears,
    bool DrsAllowed,
    ushort DrsActivationDistance,
    byte ActualTyreCompound,
    byte VisualTyreCompound,
    byte TyresAgeLaps,
    sbyte VehicleFiaFlags,
    float EnginePowerIce,
    float EnginePowerMguk,
    float ErsStoreEnergy,
    byte ErsDeployMode,
    float ErsHarvestedThisLapMguk,
    float ErsHarvestedThisLapMguh,
    float ErsDeployedThisLap,
    bool NetworkPaused);
