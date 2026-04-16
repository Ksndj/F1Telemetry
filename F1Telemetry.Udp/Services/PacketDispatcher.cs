using F1Telemetry.Core.Interfaces;
using F1Telemetry.Core.Models;
using F1Telemetry.Udp.Parsers;
using F1Telemetry.Udp.Packets;

namespace F1Telemetry.Udp.Services;

public sealed class PacketDispatcher : IPacketDispatcher<PacketId, PacketHeader>
{
    private readonly IPacketParser<PacketHeader> _headerParser;
    private readonly Dictionary<PacketId, List<Action<PacketDispatchResult<PacketId, PacketHeader>>>> _handlers = new();
    private readonly Dictionary<PacketId, IParserAdapter> _packetParsers;

    public PacketDispatcher(IPacketParser<PacketHeader> headerParser)
    {
        _headerParser = headerParser ?? throw new ArgumentNullException(nameof(headerParser));
        _packetParsers = CreatePacketParsers();
    }

    public event EventHandler<PacketDispatchResult<PacketId, PacketHeader>>? PacketDispatched;

    public event EventHandler<ParsedPacket>? PacketParsed;

    public event EventHandler<PacketParseFailure>? PacketParseFailed;

    public event EventHandler<PacketDispatcherLogEntry>? LogEmitted;

    public void RegisterHandler(
        PacketId packetId,
        Action<PacketDispatchResult<PacketId, PacketHeader>> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        if (!_handlers.TryGetValue(packetId, out var handlers))
        {
            handlers = new List<Action<PacketDispatchResult<PacketId, PacketHeader>>>();
            _handlers[packetId] = handlers;
        }

        handlers.Add(handler);
    }

    public bool TryDispatch(UdpDatagram datagram, out string? error)
    {
        ArgumentNullException.ThrowIfNull(datagram);

        if (!_headerParser.TryParse(datagram.Payload, out var header, out error))
        {
            return false;
        }

        var packetId = header.PacketId;
        var dispatchResult = new PacketDispatchResult<PacketId, PacketHeader>(packetId, header, datagram);

        try
        {
            PacketDispatched?.Invoke(this, dispatchResult);

            if (_handlers.TryGetValue(packetId, out var handlers))
            {
                foreach (var handler in handlers)
                {
                    handler(dispatchResult);
                }
            }

            TryParseTypedPacket(header, datagram);
        }
        catch (Exception ex)
        {
            error = $"Packet dispatch failed: {ex.Message}";
            return false;
        }

        error = null;
        return true;
    }

    private void TryParseTypedPacket(PacketHeader header, UdpDatagram datagram)
    {
        if (!header.IsKnownPacketId)
        {
            EmitLog(
                datagram.ReceivedAt,
                null,
                $"Unknown packet id {header.RawPacketId} received on frame {header.FrameIdentifier}.");
            return;
        }

        if (!_packetParsers.TryGetValue(header.PacketId, out var parser))
        {
            EmitLog(
                datagram.ReceivedAt,
                header.PacketId,
                $"No protocol parser registered for {header.PacketTypeName}.");
            return;
        }

        var payload = datagram.Payload.AsMemory(PacketHeader.Size);
        if (parser.TryParse(payload, out var packet, out var error))
        {
            PacketParsed?.Invoke(this, new ParsedPacket(header.PacketId, header, packet!, datagram));
            return;
        }

        var message = error ?? $"{header.PacketTypeName} packet parse failed.";
        PacketParseFailed?.Invoke(this, new PacketParseFailure(header.PacketId, header, datagram, message));
        EmitLog(datagram.ReceivedAt, header.PacketId, message);
    }

    private void EmitLog(DateTimeOffset timestamp, PacketId? packetId, string message)
    {
        LogEmitted?.Invoke(this, new PacketDispatcherLogEntry(timestamp, packetId, message));
    }

    private static Dictionary<PacketId, IParserAdapter> CreatePacketParsers()
    {
        return new Dictionary<PacketId, IParserAdapter>
        {
            [PacketId.Motion] = new ParserAdapter<MotionPacket>(new MotionPacketParser()),
            [PacketId.Session] = new ParserAdapter<SessionPacket>(new SessionPacketParser()),
            [PacketId.LapData] = new ParserAdapter<LapDataPacket>(new LapDataPacketParser()),
            [PacketId.Event] = new ParserAdapter<EventPacket>(new EventPacketParser()),
            [PacketId.Participants] = new ParserAdapter<ParticipantsPacket>(new ParticipantsPacketParser()),
            [PacketId.CarTelemetry] = new ParserAdapter<CarTelemetryPacket>(new CarTelemetryPacketParser()),
            [PacketId.CarStatus] = new ParserAdapter<CarStatusPacket>(new CarStatusPacketParser()),
            [PacketId.FinalClassification] = new ParserAdapter<FinalClassificationPacket>(new FinalClassificationPacketParser()),
            [PacketId.CarDamage] = new ParserAdapter<CarDamagePacket>(new CarDamagePacketParser()),
            [PacketId.SessionHistory] = new ParserAdapter<SessionHistoryPacket>(new SessionHistoryPacketParser()),
            [PacketId.TyreSets] = new ParserAdapter<TyreSetsPacket>(new TyreSetsPacketParser()),
            [PacketId.MotionEx] = new ParserAdapter<MotionExPacket>(new MotionExPacketParser()),
            [PacketId.LapPositions] = new ParserAdapter<LapPositionsPacket>(new LapPositionsPacketParser())
        };
    }

    private interface IParserAdapter
    {
        bool TryParse(ReadOnlyMemory<byte> payload, out IUdpPacket? packet, out string? error);
    }

    private sealed class ParserAdapter<TPacket> : IParserAdapter
        where TPacket : class, IUdpPacket
    {
        private readonly IPacketParser<TPacket> _parser;

        public ParserAdapter(IPacketParser<TPacket> parser)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        public bool TryParse(ReadOnlyMemory<byte> payload, out IUdpPacket? packet, out string? error)
        {
            if (_parser.TryParse(payload, out var typedPacket, out error))
            {
                packet = typedPacket;
                return true;
            }

            packet = null;
            return false;
        }
    }
}
