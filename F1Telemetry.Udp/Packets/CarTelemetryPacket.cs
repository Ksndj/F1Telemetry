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

/// <summary>F1 26 新增：主动空气动力学 / 超车辅助遥测包（PacketId.CarTelemetry2）。</summary>
public sealed record CarTelemetry2Packet(
    CarTelemetry2Data[] Cars) : IUdpPacket;

/// <summary>F1 26 车辆主动空气动力学 / 超车辅助数据（每车 10 字节）。</summary>
public sealed record CarTelemetry2Data(
    byte ActiveAeroMode,
    byte ActiveAeroAvailable,
    ushort ActiveAeroActivationDistance,
    byte OvertakeAvailable,
    byte OvertakeActive,
    ushort OvertakeActivationDistance,
    byte Regulations2026,
    byte DrivingWrongWay);
