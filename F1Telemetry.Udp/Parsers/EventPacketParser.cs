using System.Text;
using F1Telemetry.Udp.Packets;

namespace F1Telemetry.Udp.Parsers;

public sealed class EventPacketParser : FixedSizePacketParser<EventPacket>
{
    public EventPacketParser()
        : base(nameof(EventPacket), UdpPacketConstants.EventBodySize)
    {
    }

    protected override EventPacket Parse(ref PacketBufferReader reader)
    {
        var rawCodeBytes = reader.ReadBytes(4);
        var rawCode = Encoding.ASCII.GetString(rawCodeBytes);
        var detailBytes = reader.ReadBytes(12);
        var detailReader = new PacketBufferReader(detailBytes);

        return rawCode switch
        {
            "SSTA" => new EventPacket(rawCode, EventCode.SessionStarted, new EmptyEventDetail()),
            "SEND" => new EventPacket(rawCode, EventCode.SessionEnded, new EmptyEventDetail()),
            "FTLP" => new EventPacket(
                rawCode,
                EventCode.FastestLap,
                new FastestLapEventDetail(
                    VehicleIndex: detailReader.ReadByte(),
                    LapTime: detailReader.ReadSingle())),
            "RTMT" => new EventPacket(
                rawCode,
                EventCode.Retirement,
                new RetirementEventDetail(
                    VehicleIndex: detailReader.ReadByte(),
                    Reason: detailReader.ReadByte())),
            "DRSE" => new EventPacket(rawCode, EventCode.DrsEnabled, new EmptyEventDetail()),
            "DRSD" => new EventPacket(
                rawCode,
                EventCode.DrsDisabled,
                new DrsDisabledEventDetail(detailReader.ReadByte())),
            "TMPT" => new EventPacket(
                rawCode,
                EventCode.TeamMateInPits,
                new TeamMateInPitsEventDetail(detailReader.ReadByte())),
            "CHQF" => new EventPacket(rawCode, EventCode.ChequeredFlag, new EmptyEventDetail()),
            "RCWN" => new EventPacket(
                rawCode,
                EventCode.RaceWinner,
                new RaceWinnerEventDetail(detailReader.ReadByte())),
            "PENA" => new EventPacket(
                rawCode,
                EventCode.Penalty,
                new PenaltyEventDetail(
                    PenaltyType: detailReader.ReadByte(),
                    InfringementType: detailReader.ReadByte(),
                    VehicleIndex: detailReader.ReadByte(),
                    OtherVehicleIndex: detailReader.ReadByte(),
                    Time: detailReader.ReadByte(),
                    LapNumber: detailReader.ReadByte(),
                    PlacesGained: detailReader.ReadByte())),
            "SPTP" => new EventPacket(
                rawCode,
                EventCode.SpeedTrap,
                new SpeedTrapEventDetail(
                    VehicleIndex: detailReader.ReadByte(),
                    Speed: detailReader.ReadSingle(),
                    IsOverallFastestInSession: detailReader.ReadBooleanByte(),
                    IsDriverFastestInSession: detailReader.ReadBooleanByte(),
                    FastestVehicleIndexInSession: detailReader.ReadByte(),
                    FastestSpeedInSession: detailReader.ReadSingle())),
            "STLG" => new EventPacket(
                rawCode,
                EventCode.StartLights,
                new StartLightsEventDetail(detailReader.ReadByte())),
            "LGOT" => new EventPacket(rawCode, EventCode.LightsOut, new EmptyEventDetail()),
            "DTSV" => new EventPacket(
                rawCode,
                EventCode.DriveThroughPenaltyServed,
                new DriveThroughPenaltyServedEventDetail(detailReader.ReadByte())),
            "SGSV" => new EventPacket(
                rawCode,
                EventCode.StopGoPenaltyServed,
                new StopGoPenaltyServedEventDetail(
                    VehicleIndex: detailReader.ReadByte(),
                    StopTime: detailReader.ReadSingle())),
            "FLBK" => new EventPacket(
                rawCode,
                EventCode.Flashback,
                new FlashbackEventDetail(
                    FlashbackFrameIdentifier: detailReader.ReadUInt32(),
                    FlashbackSessionTime: detailReader.ReadSingle())),
            "BUTN" => new EventPacket(
                rawCode,
                EventCode.Buttons,
                new ButtonsEventDetail(detailReader.ReadUInt32())),
            "RDFL" => new EventPacket(rawCode, EventCode.RedFlag, new EmptyEventDetail()),
            "OVTK" => new EventPacket(
                rawCode,
                EventCode.Overtake,
                new OvertakeEventDetail(
                    OvertakingVehicleIndex: detailReader.ReadByte(),
                    BeingOvertakenVehicleIndex: detailReader.ReadByte())),
            "SCAR" => new EventPacket(
                rawCode,
                EventCode.SafetyCar,
                new SafetyCarEventDetail(
                    SafetyCarType: detailReader.ReadByte(),
                    EventType: detailReader.ReadByte())),
            "COLL" => new EventPacket(
                rawCode,
                EventCode.Collision,
                new CollisionEventDetail(
                    Vehicle1Index: detailReader.ReadByte(),
                    Vehicle2Index: detailReader.ReadByte())),
            _ => new EventPacket(rawCode, EventCode.Unknown, new UnknownEventDetail(detailBytes))
        };
    }
}
