using F1Telemetry.Analytics.State;
using F1Telemetry.Udp.Packets;

namespace F1Telemetry.Analytics.Interfaces;

/// <summary>
/// Aggregates parsed UDP packets into a central real-time session state.
/// </summary>
public interface IStateAggregator
{
    /// <summary>
    /// Gets the current session state store.
    /// </summary>
    SessionStateStore SessionStateStore { get; }

    /// <summary>
    /// Applies a parsed UDP packet to the central state.
    /// </summary>
    /// <param name="parsedPacket">The parsed UDP packet to aggregate.</param>
    void ApplyPacket(ParsedPacket parsedPacket);
}
