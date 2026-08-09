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
            // F1 26 将 DriverId/NetworkId/TeamId 加宽为 uint16，F1 24/25 仍为 byte。
            var driverId = packetFormat == UdpPacketConstants.Format2026 ? reader.ReadUInt16() : reader.ReadByte();
            var networkId = packetFormat == UdpPacketConstants.Format2026 ? reader.ReadUInt16() : reader.ReadByte();
            var teamId = packetFormat == UdpPacketConstants.Format2026 ? reader.ReadUInt16() : reader.ReadByte();
            var isMyTeam = reader.ReadBooleanByte();
            var raceNumber = reader.ReadByte();
            var nationality = reader.ReadByte();
            // F1 24 车手名字段为 48 字节，F1 25 起为 32 字节。
            var nameLength = packetFormat == UdpPacketConstants.Format2024 ? 48 : 32;
            var name = reader.ReadFixedString(nameLength);
            var yourTelemetry = reader.ReadBooleanByte();
            var showOnlineNames = reader.ReadBooleanByte();
            var techLevel = reader.ReadUInt16();
            var platform = reader.ReadByte();

            // F1 24 无 livery 字段（numColours/liveryColours），模型字段保持默认值。
            var numColours = (byte)0;
            if (packetFormat >= UdpPacketConstants.Format2025)
            {
                numColours = reader.ReadByte();

                for (var colourIndex = 0; colourIndex < liveryColours.Length; colourIndex++)
                {
                    liveryColours[colourIndex] = new LiveryColourData(
                        Red: reader.ReadByte(),
                        Green: reader.ReadByte(),
                        Blue: reader.ReadByte());
                }
            }
            else
            {
                // 填充默认元素，避免下游迭代 liveryColours 数组时遇到空引用。
                Array.Fill(liveryColours, new LiveryColourData(0, 0, 0));
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
