namespace F1Telemetry.Core.Models;

public sealed record PacketDispatchResult<TPacketId, TPacket>(
    TPacketId PacketId,
    TPacket Packet,
    UdpDatagram Datagram);
