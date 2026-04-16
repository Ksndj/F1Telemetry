namespace F1Telemetry.Udp.Packets;

public sealed record PacketDispatcherLogEntry(
    DateTimeOffset Timestamp,
    PacketId? PacketId,
    string Message);
