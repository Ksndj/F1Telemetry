namespace F1Telemetry.Udp.Packets;

public sealed record PacketHeader(
    ushort PacketFormat,
    byte GameYear,
    byte GameMajorVersion,
    byte GameMinorVersion,
    byte PacketVersion,
    byte RawPacketId,
    ulong SessionUid,
    float SessionTime,
    uint FrameIdentifier,
    uint OverallFrameIdentifier,
    byte PlayerCarIndex,
    byte SecondaryPlayerCarIndex)
{
    public const int Size = 29;

    public PacketId PacketId => (PacketId)RawPacketId;

    public bool IsKnownPacketId => Enum.IsDefined(typeof(PacketId), PacketId);

    public string PacketTypeName =>
        IsKnownPacketId
            ? PacketId.ToString()
            : $"Unknown({RawPacketId})";
}
