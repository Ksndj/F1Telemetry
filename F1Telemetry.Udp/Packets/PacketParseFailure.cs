using F1Telemetry.Core.Models;

namespace F1Telemetry.Udp.Packets;

public sealed record PacketParseFailure(
    PacketId PacketId,
    PacketHeader Header,
    UdpDatagram Datagram,
    string ErrorMessage);
