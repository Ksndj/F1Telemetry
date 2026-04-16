using F1Telemetry.Udp.Packets;

namespace F1Telemetry.Udp.Parsers;

public sealed class CarStatusPacketParser : FixedSizePacketParser<CarStatusPacket>
{
    public CarStatusPacketParser()
        : base(nameof(CarStatusPacket), UdpPacketConstants.CarStatusBodySize)
    {
    }

    protected override CarStatusPacket Parse(ref PacketBufferReader reader)
    {
        var cars = new CarStatusData[UdpPacketConstants.MaxCarsInSession];

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
                ErsDeployedThisLap: reader.ReadSingle(),
                NetworkPaused: reader.ReadBooleanByte());
        }

        return new CarStatusPacket(cars);
    }
}
