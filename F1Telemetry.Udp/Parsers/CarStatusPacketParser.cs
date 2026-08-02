using F1Telemetry.Udp.Packets;

namespace F1Telemetry.Udp.Parsers;

public sealed class CarStatusPacketParser : FixedSizePacketParser<CarStatusPacket>
{
    public CarStatusPacketParser()
        : base(nameof(CarStatusPacket), UdpPacketConstants.CarStatusBodySizeByFormat)
    {
    }

    protected override CarStatusPacket Parse(ref PacketBufferReader reader, ushort packetFormat)
    {
        var carCount = UdpPacketConstants.GetMaxCarsInSession(packetFormat);
        var cars = new CarStatusData[carCount];

        for (var index = 0; index < cars.Length; index++)
        {
            cars[index] = new CarStatusData(
                TractionControl: reader.ReadByte(),
                AntiLockBrakes: reader.ReadBooleanByte(),
                FuelMix: reader.ReadByte(),
                FrontBrakeBias: reader.ReadByte(),
                PitLimiterStatus: reader.ReadBooleanByte(),
                FuelInTank: reader.ReadSingle(),
                FuelCapacity: reader.ReadSingle(),
                FuelRemainingLaps: reader.ReadSingle(),
                MaxRpm: reader.ReadUInt16(),
                IdleRpm: reader.ReadUInt16(),
                MaxGears: reader.ReadByte(),
                DrsAllowed: reader.ReadBooleanByte(),
                DrsActivationDistance: reader.ReadUInt16(),
                ActualTyreCompound: reader.ReadByte(),
                VisualTyreCompound: reader.ReadByte(),
                TyresAgeLaps: reader.ReadByte(),
                VehicleFiaFlags: reader.ReadSByte(),
                EnginePowerIce: reader.ReadSingle(),
                EnginePowerMguk: reader.ReadSingle(),
                ErsStoreEnergy: reader.ReadSingle(),
                ErsDeployMode: reader.ReadByte(),
                ErsHarvestedThisLapMguk: reader.ReadSingle(),
                ErsHarvestedThisLapMguh: reader.ReadSingle(),
                // F1 26 在 ersHarvestedThisLapMGUH 之后追加该字段，F1 25 恒为 0。
                ErsHarvestedLimitPerLap: packetFormat == UdpPacketConstants.Format2026 ? reader.ReadSingle() : 0f,
                ErsDeployedThisLap: reader.ReadSingle(),
                NetworkPaused: reader.ReadBooleanByte());
        }

        return new CarStatusPacket(cars);
    }
}
