using F1Telemetry.Udp.Packets;

namespace F1Telemetry.Udp.Parsers;

public sealed class LapDataPacketParser : FixedSizePacketParser<LapDataPacket>
{
    public LapDataPacketParser()
        : base(nameof(LapDataPacket), UdpPacketConstants.LapDataBodySize)
    {
    }

    protected override LapDataPacket Parse(ref PacketBufferReader reader)
    {
        var cars = new LapDataEntry[UdpPacketConstants.MaxCarsInSession];

        for (var index = 0; index < cars.Length; index++)
        {
            cars[index] = new LapDataEntry(
                LastLapTimeInMs: reader.ReadUInt32(),
                CurrentLapTimeInMs: reader.ReadUInt32(),
                Sector1TimeInMs: reader.ReadUInt16(),
                Sector1TimeMinutes: reader.ReadByte(),
                Sector2TimeInMs: reader.ReadUInt16(),
                Sector2TimeMinutes: reader.ReadByte(),
                DeltaToCarInFrontInMs: reader.ReadUInt16(),
                DeltaToCarInFrontMinutes: reader.ReadByte(),
                DeltaToRaceLeaderInMs: reader.ReadUInt16(),
                DeltaToRaceLeaderMinutes: reader.ReadByte(),
                LapDistance: reader.ReadSingle(),
                TotalDistance: reader.ReadSingle(),
                SafetyCarDelta: reader.ReadSingle(),
                CarPosition: reader.ReadByte(),
                CurrentLapNumber: reader.ReadByte(),
                PitStatus: reader.ReadByte(),
                NumPitStops: reader.ReadByte(),
                Sector: reader.ReadByte(),
                IsCurrentLapInvalid: reader.ReadBooleanByte(),
                Penalties: reader.ReadByte(),
                TotalWarnings: reader.ReadByte(),
                CornerCuttingWarnings: reader.ReadByte(),
                NumUnservedDriveThroughPenalties: reader.ReadByte(),
                NumUnservedStopGoPenalties: reader.ReadByte(),
                GridPosition: reader.ReadByte(),
                DriverStatus: reader.ReadByte(),
                ResultStatus: reader.ReadByte(),
                IsPitLaneTimerActive: reader.ReadBooleanByte(),
                PitLaneTimeInLaneInMs: reader.ReadUInt16(),
                PitStopTimerInMs: reader.ReadUInt16(),
                ShouldServePitStopPenalty: reader.ReadBooleanByte(),
                SpeedTrapFastestSpeed: reader.ReadSingle(),
                SpeedTrapFastestLap: reader.ReadByte());
        }

        return new LapDataPacket(
            Cars: cars,
            TimeTrialPersonalBestCarIndex: reader.ReadByte(),
            TimeTrialRivalCarIndex: reader.ReadByte());
    }
}
