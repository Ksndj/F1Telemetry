namespace F1Telemetry.Udp.Packets;

public static class UdpPacketConstants
{
    public const int MaxCarsInSession = 22;
    public const int MaxTyreSets = 20;
    public const int MaxMarshalZones = 21;
    public const int MaxWeatherForecastSamples = 64;
    public const int MaxWeekendSessions = 12;
    public const int MaxSessionHistoryLaps = 100;
    public const int MaxSessionHistoryTyreStints = 8;
    public const int MaxFinalClassificationTyreStints = 8;
    public const int MaxLapPositionsLaps = 50;

    public const int PacketHeaderSize = PacketHeader.Size;

    public const int MotionTotalSize = 1349;
    public const int SessionTotalSize = 753;
    public const int LapDataTotalSize = 1285;
    public const int EventTotalSize = 45;
    public const int ParticipantsTotalSize = 1284;
    public const int CarTelemetryTotalSize = 1352;
    public const int CarStatusTotalSize = 1239;
    public const int FinalClassificationTotalSize = 1042;
    public const int CarDamageTotalSize = 1041;
    public const int SessionHistoryTotalSize = 1460;
    public const int TyreSetsTotalSize = 231;
    public const int MotionExTotalSize = 273;
    public const int LapPositionsTotalSize = 1131;

    public const int MotionBodySize = MotionTotalSize - PacketHeaderSize;
    public const int SessionBodySize = SessionTotalSize - PacketHeaderSize;
    public const int LapDataBodySize = LapDataTotalSize - PacketHeaderSize;
    public const int EventBodySize = EventTotalSize - PacketHeaderSize;
    public const int ParticipantsBodySize = ParticipantsTotalSize - PacketHeaderSize;
    public const int CarTelemetryBodySize = CarTelemetryTotalSize - PacketHeaderSize;
    public const int CarStatusBodySize = CarStatusTotalSize - PacketHeaderSize;
    public const int FinalClassificationBodySize = FinalClassificationTotalSize - PacketHeaderSize;
    public const int CarDamageBodySize = CarDamageTotalSize - PacketHeaderSize;
    public const int SessionHistoryBodySize = SessionHistoryTotalSize - PacketHeaderSize;
    public const int TyreSetsBodySize = TyreSetsTotalSize - PacketHeaderSize;
    public const int MotionExBodySize = MotionExTotalSize - PacketHeaderSize;
    public const int LapPositionsBodySize = LapPositionsTotalSize - PacketHeaderSize;
}
