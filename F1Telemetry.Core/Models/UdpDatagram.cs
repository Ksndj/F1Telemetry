using System.Net;

namespace F1Telemetry.Core.Models;

public sealed record UdpDatagram(
    byte[] Payload,
    IPEndPoint RemoteEndPoint,
    DateTimeOffset ReceivedAt);
