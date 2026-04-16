namespace F1Telemetry.Udp.Packets;

public sealed record CarTelemetryPacket(
    CarTelemetryData[] Cars,
    byte MfdPanelIndex,
    byte MfdPanelIndexSecondaryPlayer,
    sbyte SuggestedGear) : IUdpPacket;

public sealed record CarTelemetryData(
    ushort Speed,
    float Throttle,
    float Steer,
    float Brake,
    byte Clutch,
    sbyte Gear,
    ushort EngineRpm,
    bool Drs,
    byte RevLightsPercent,
    ushort RevLightsBitValue,
    WheelSet<ushort> BrakesTemperature,
    WheelSet<byte> TyresSurfaceTemperature,
    WheelSet<byte> TyresInnerTemperature,
    ushort EngineTemperature,
    WheelSet<float> TyresPressure,
    WheelSet<byte> SurfaceType);
