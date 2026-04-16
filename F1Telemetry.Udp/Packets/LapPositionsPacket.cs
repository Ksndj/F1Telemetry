namespace F1Telemetry.Udp.Packets;

public sealed record LapPositionsPacket(
    byte NumLaps,
    byte LapStart,
    byte[][] PositionForVehicleIndexByLap) : IUdpPacket;
