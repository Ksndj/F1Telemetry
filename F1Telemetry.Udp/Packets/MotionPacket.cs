using System.Numerics;

namespace F1Telemetry.Udp.Packets;

public sealed record MotionPacket(
    CarMotionData[] Cars) : IUdpPacket;

public sealed record CarMotionData(
    Vector3 WorldPosition,
    Vector3 WorldVelocity,
    short WorldForwardDirectionX,
    short WorldForwardDirectionY,
    short WorldForwardDirectionZ,
    short WorldRightDirectionX,
    short WorldRightDirectionY,
    short WorldRightDirectionZ,
    float GForceLateral,
    float GForceLongitudinal,
    float GForceVertical,
    float Yaw,
    float Pitch,
    float Roll);
