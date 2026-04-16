using F1Telemetry.Udp.Packets;

namespace F1Telemetry.Udp.Parsers;

public sealed class FinalClassificationPacketParser : FixedSizePacketParser<FinalClassificationPacket>
{
    public FinalClassificationPacketParser()
        : base(nameof(FinalClassificationPacket), UdpPacketConstants.FinalClassificationBodySize)
    {
    }

    protected override FinalClassificationPacket Parse(ref PacketBufferReader reader)
    {
        var cars = new FinalClassificationData[UdpPacketConstants.MaxCarsInSession];
        var numCars = reader.ReadByte();

        for (var index = 0; index < cars.Length; index++)
        {
            cars[index] = new FinalClassificationData(
                Position: reader.ReadByte(),
                NumLaps: reader.ReadByte(),
                GridPosition: reader.ReadByte(),
                Points: reader.ReadByte(),
                NumPitStops: reader.ReadByte(),
                ResultStatus: reader.ReadByte(),
                ResultReason: reader.ReadByte(),
                BestLapTimeInMs: reader.ReadUInt32(),
                TotalRaceTime: reader.ReadDouble(),
                PenaltiesTime: reader.ReadByte(),
                NumPenalties: reader.ReadByte(),
                NumTyreStints: reader.ReadByte(),
                TyreStintsActual: reader.ReadBytes(UdpPacketConstants.MaxFinalClassificationTyreStints),
                TyreStintsVisual: reader.ReadBytes(UdpPacketConstants.MaxFinalClassificationTyreStints),
                TyreStintsEndLaps: reader.ReadBytes(UdpPacketConstants.MaxFinalClassificationTyreStints));
        }

        return new FinalClassificationPacket(numCars, cars);
    }
}
