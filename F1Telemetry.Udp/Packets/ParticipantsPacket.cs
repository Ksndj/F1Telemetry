namespace F1Telemetry.Udp.Packets;

public sealed record ParticipantsPacket(
    byte NumActiveCars,
    ParticipantData[] Participants) : IUdpPacket;

public sealed record ParticipantData(
    bool IsAiControlled,
    byte DriverId,
    byte NetworkId,
    byte TeamId,
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
