using F1Telemetry.Udp.Packets;

namespace F1Telemetry.Udp.Parsers;

public sealed class ParticipantsPacketParser : FixedSizePacketParser<ParticipantsPacket>
{
    public ParticipantsPacketParser()
        : base(nameof(ParticipantsPacket), UdpPacketConstants.ParticipantsBodySize)
    {
    }

    protected override ParticipantsPacket Parse(ref PacketBufferReader reader)
    {
        var participants = new ParticipantData[UdpPacketConstants.MaxCarsInSession];
        var numActiveCars = reader.ReadByte();

        for (var index = 0; index < participants.Length; index++)
        {
            var liveryColours = new LiveryColourData[4];

            var isAiControlled = reader.ReadBooleanByte();
            var driverId = reader.ReadByte();
            var networkId = reader.ReadByte();
            var teamId = reader.ReadByte();
            var isMyTeam = reader.ReadBooleanByte();
            var raceNumber = reader.ReadByte();
            var nationality = reader.ReadByte();
            var name = reader.ReadFixedString(32);
            var yourTelemetry = reader.ReadBooleanByte();
            var showOnlineNames = reader.ReadBooleanByte();
            var techLevel = reader.ReadUInt16();
            var platform = reader.ReadByte();
            var numColours = reader.ReadByte();

            for (var colourIndex = 0; colourIndex < liveryColours.Length; colourIndex++)
            {
                liveryColours[colourIndex] = new LiveryColourData(
                    Red: reader.ReadByte(),
                    Green: reader.ReadByte(),
                    Blue: reader.ReadByte());
            }

            participants[index] = new ParticipantData(
                IsAiControlled: isAiControlled,
                DriverId: driverId,
                NetworkId: networkId,
                TeamId: teamId,
                IsMyTeam: isMyTeam,
                RaceNumber: raceNumber,
                Nationality: nationality,
                Name: name,
                YourTelemetry: yourTelemetry,
                ShowOnlineNames: showOnlineNames,
                TechLevel: techLevel,
                Platform: platform,
                NumColours: numColours,
                LiveryColours: liveryColours);
        }

        return new ParticipantsPacket(numActiveCars, participants);
    }
}
