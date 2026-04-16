using F1Telemetry.Udp.Packets;
using F1Telemetry.Udp.Parsers;
using Xunit;

namespace F1Telemetry.Tests;

public sealed class PacketHeaderParserTests
{
    [Fact]
    public void PacketHeaderParser_ParsesExpectedHeaderFields()
    {
        var payload = ProtocolTestData.BuildHeaderOnlyPacket(PacketId.LapData);

        var parser = new PacketHeaderParser();
        var parsed = parser.TryParse(payload, out var header, out var error);

        Assert.True(parsed);
        Assert.Null(error);
        Assert.Equal(ProtocolTestData.PacketFormat, header.PacketFormat);
        Assert.Equal(ProtocolTestData.GameYear, header.GameYear);
        Assert.Equal(ProtocolTestData.GameMajorVersion, header.GameMajorVersion);
        Assert.Equal(ProtocolTestData.GameMinorVersion, header.GameMinorVersion);
        Assert.Equal(ProtocolTestData.PacketVersion, header.PacketVersion);
        Assert.Equal((byte)PacketId.LapData, header.RawPacketId);
        Assert.Equal(ProtocolTestData.SessionUid, header.SessionUid);
        Assert.Equal(ProtocolTestData.SessionTime, header.SessionTime, precision: 3);
        Assert.Equal(ProtocolTestData.FrameIdentifier, header.FrameIdentifier);
        Assert.Equal(ProtocolTestData.OverallFrameIdentifier, header.OverallFrameIdentifier);
        Assert.Equal(ProtocolTestData.PlayerCarIndex, header.PlayerCarIndex);
        Assert.Equal(ProtocolTestData.SecondaryPlayerCarIndex, header.SecondaryPlayerCarIndex);
        Assert.Equal(PacketId.LapData, header.PacketId);
    }
}
