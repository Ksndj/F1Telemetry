namespace F1Telemetry.Udp.Packets;

public sealed record CarDamagePacket(
    CarDamageData[] Cars) : IUdpPacket;

public sealed record CarDamageData(
    WheelSet<float> TyreWear,
    WheelSet<byte> TyreDamage,
    WheelSet<byte> BrakesDamage,
    WheelSet<byte> TyreBlisters,
    byte FrontLeftWingDamage,
    byte FrontRightWingDamage,
    byte RearWingDamage,
    byte FloorDamage,
    byte DiffuserDamage,
    byte SidepodDamage,
    bool DrsFault,
    bool ErsFault,
    byte GearBoxDamage,
    byte EngineDamage,
    byte EngineMguhWear,
    byte EngineEsWear,
    byte EngineCeWear,
    byte EngineIceWear,
    byte EngineMgukWear,
    byte EngineTcWear,
    bool EngineBlown,
    bool EngineSeized);
