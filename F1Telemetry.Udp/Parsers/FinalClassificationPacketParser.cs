using F1Telemetry.Udp.Packets;

namespace F1Telemetry.Udp.Parsers;

public sealed class FinalClassificationPacketParser : FixedSizePacketParser<FinalClassificationPacket>
{
    public FinalClassificationPacketParser()
        : base(nameof(FinalClassificationPacket), UdpPacketConstants.FinalClassificationBodySizeByFormat)
    {
    }

    protected override FinalClassificationPacket Parse(ref PacketBufferReader reader, ushort packetFormat)
    {
        var carCount = UdpPacketConstants.GetMaxCarsInSession(packetFormat);
        var cars = new FinalClassificationData[carCount];
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
                // F1 24 无 resultReason 字段（resultStatus 后直接 bestLapTimeInMS），模型保持默认值 0。
                ResultReason: packetFormat >= UdpPacketConstants.Format2025 ? reader.ReadByte() : (byte)0,
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
