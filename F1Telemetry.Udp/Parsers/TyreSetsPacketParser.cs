using F1Telemetry.Udp.Packets;

namespace F1Telemetry.Udp.Parsers;

public sealed class TyreSetsPacketParser : FixedSizePacketParser<TyreSetsPacket>
{
    public TyreSetsPacketParser()
        : base(nameof(TyreSetsPacket), UdpPacketConstants.TyreSetsBodySize)
    {
    }

    protected override TyreSetsPacket Parse(ref PacketBufferReader reader)
    {
        var tyreSets = new TyreSetData[UdpPacketConstants.MaxTyreSets];
        var carIndex = reader.ReadByte();

        for (var index = 0; index < tyreSets.Length; index++)
        {
            tyreSets[index] = new TyreSetData(
                ActualTyreCompound: reader.ReadByte(),
                VisualTyreCompound: reader.ReadByte(),
                Wear: reader.ReadByte(),
                Available: reader.ReadBooleanByte(),
                RecommendedSession: reader.ReadByte(),
                LifeSpan: reader.ReadByte(),
                UsableLife: reader.ReadByte(),
                LapDeltaTime: reader.ReadInt16(),
                Fitted: reader.ReadBooleanByte());
        }

        return new TyreSetsPacket(
            CarIndex: carIndex,
            TyreSets: tyreSets,
            FittedIndex: reader.ReadByte());
    }
}
