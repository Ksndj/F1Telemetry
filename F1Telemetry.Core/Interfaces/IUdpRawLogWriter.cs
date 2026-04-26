using F1Telemetry.Core.Models;

namespace F1Telemetry.Core.Interfaces;

/// <summary>
/// Records raw UDP datagrams on a non-blocking background path.
/// </summary>
public interface IUdpRawLogWriter : IAsyncDisposable
{
    /// <summary>
    /// Gets the latest raw UDP log writer status.
    /// </summary>
    UdpRawLogStatus Status { get; }

    /// <summary>
    /// Applies raw UDP log options.
    /// </summary>
    void UpdateOptions(UdpRawLogOptions options);

    /// <summary>
    /// Attempts to enqueue a raw UDP datagram without blocking the receiver.
    /// </summary>
    void TryEnqueue(UdpDatagram datagram);
}
