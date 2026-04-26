namespace F1Telemetry.Core.Models;

/// <summary>
/// Represents user-configurable UDP listener settings.
/// </summary>
public sealed record UdpSettings
{
    /// <summary>
    /// Gets the default F1 25 UDP listen port.
    /// </summary>
    public const int DefaultListenPort = 20777;

    /// <summary>
    /// Gets the smallest valid UDP listen port.
    /// </summary>
    public const int MinListenPort = 1;

    /// <summary>
    /// Gets the largest valid UDP listen port.
    /// </summary>
    public const int MaxListenPort = 65535;

    /// <summary>
    /// Gets the persisted UDP listen port.
    /// </summary>
    public int ListenPort { get; init; } = DefaultListenPort;
}
