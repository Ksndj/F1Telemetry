using F1Telemetry.Udp.Packets;

namespace F1Telemetry.Udp.Parsers;

public sealed class ParticipantsPacketParser : FixedSizePacketParser<ParticipantsPacket>
{
    public ParticipantsPacketParser()
        : base(nameof(ParticipantsPacket), UdpPacketConstants.ParticipantsBodySizeByFormat)
    {
    }

    protected override ParticipantsPacket Parse(ref PacketBufferReader reader, ushort packetFormat)
    {
        var participantCount = UdpPacketConstants.GetMaxCarsInSession(packetFormat);
        var participants = new ParticipantData[participantCount];
        var numActiveCars = reader.ReadByte();

        for (var index = 0; index < participants.Length; index++)
        {
            var liveryColours = new LiveryColourData[4];

            var isAiControlled = reader.ReadBooleanByte();
            // F1 26 将 DriverId/NetworkId/TeamId 加宽为 uint16，F1 25 仍为 byte。
            var driverId = packetFormat == UdpPacketConstants.Format2026 ? reader.ReadUInt16() : reader.ReadByte();
            var networkId = packetFormat == UdpPacketConstants.Format2026 ? reader.ReadUInt16() : reader.ReadByte();
            var teamId = packetFormat == UdpPacketConstants.Format2026 ? reader.ReadUInt16() : reader.ReadByte();
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
