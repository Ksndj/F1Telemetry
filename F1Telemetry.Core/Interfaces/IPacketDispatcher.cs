using F1Telemetry.Core.Models;

namespace F1Telemetry.Core.Interfaces;

public interface IPacketDispatcher<TPacketId, TPacket>
{
    event EventHandler<PacketDispatchResult<TPacketId, TPacket>>? PacketDispatched;

    bool TryDispatch(UdpDatagram datagram, out string? error);
}
