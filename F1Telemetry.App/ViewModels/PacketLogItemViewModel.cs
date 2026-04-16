namespace F1Telemetry.App.ViewModels;

public sealed class PacketLogItemViewModel
{
    public required string ReceivedAt { get; init; }

    public required string PacketType { get; init; }
}
