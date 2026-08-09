namespace F1Telemetry.Udp.Packets;

public sealed record ParticipantsPacket(
    byte NumActiveCars,
    ParticipantData[] Participants) : IUdpPacket;

/// <summary>
/// 参赛车手数据。F1 26 中 DriverId/NetworkId/TeamId 由 byte 加宽为 uint16，
/// 因此模型字段使用 ushort（F1 25 数值仍在此范围内）。
/// </summary>
public sealed record ParticipantData(
    bool IsAiControlled,
    ushort DriverId,
    ushort NetworkId,
    ushort TeamId,
    bool IsMyTeam,
    byte RaceNumber,
    byte Nationality,
    string Name,
    bool YourTelemetry,
    bool ShowOnlineNames,
    ushort TechLevel,
    byte Platform,
    byte NumColours,
    LiveryColourData[] LiveryColours);

public sealed record LiveryColourData(
    byte Red,
    byte Green,
    byte Blue);
