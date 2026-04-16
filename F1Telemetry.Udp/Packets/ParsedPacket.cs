using F1Telemetry.Core.Models;

namespace F1Telemetry.Udp.Packets;

public sealed record ParsedPacket(
    PacketId PacketId,
    PacketHeader Header,
    IUdpPacket Packet,
    UdpDatagram Datagram);
