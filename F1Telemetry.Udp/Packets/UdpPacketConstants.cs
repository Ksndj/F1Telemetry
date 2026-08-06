namespace F1Telemetry.Udp.Packets;

/// <summary>
/// UDP 遥测共享常量：单值常量（F1 25 基线，兼容下游）与按协议格式（packetFormat）组织的版本化尺寸表。
/// </summary>
public static class UdpPacketConstants
{
    public const ushort Format2024 = 2024;
    public const ushort Format2025 = 2025;
    public const ushort Format2026 = 2026;

    /// <summary>
    /// 车辆数量上限（F1 26 为 24 辆）。
    /// 解析器请通过 <see cref="GetMaxCarsInSession"/> 按协议格式获取实际数量。
    /// </summary>
    public const int MaxCarsInSession = 24;
    public const int MaxTyreSets = 20;
    public const int MaxMarshalZones = 21;
    public const int MaxWeatherForecastSamples = 64;
    public const int MaxWeekendSessions = 12;
    public const int MaxSessionHistoryLaps = 100;
    public const int MaxSessionHistoryTyreStints = 8;
    public const int MaxFinalClassificationTyreStints = 8;
    public const int MaxLapPositionsLaps = 50;

    public const int PacketHeaderSize = PacketHeader.Size;

    // F1 25 单值尺寸常量（保持兼容，供仍按单赛季假设的下游使用）。
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
    public const int CarTelemetry2TotalSize = 269;

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
    public const int CarTelemetry2BodySize = CarTelemetry2TotalSize - PacketHeaderSize;

    // 版本化总尺寸表（含 29 字节头）。注意：静态初始化按声明顺序执行，Body 表依赖 Total 表。
    // F1 24（2024）：除 Participants/FinalClassification/CarDamage/MotionEx 四包尺寸不同外，其余与 2025 相同；
    // LapPositions 为 2025 新增包、CarTelemetry2 为 2026 新增包，均不注册 2024 尺寸。
    public static IReadOnlyDictionary<ushort, int> MotionTotalSizeByFormat { get; } = new Dictionary<ushort, int>
    {
        [Format2024] = 1349,
        [Format2025] = 1349,
        [Format2026] = 1325,
    };

    public static IReadOnlyDictionary<ushort, int> SessionTotalSizeByFormat { get; } = new Dictionary<ushort, int>
    {
        [Format2024] = 753,
        [Format2025] = 753,
        [Format2026] = 926,
    };

    public static IReadOnlyDictionary<ushort, int> LapDataTotalSizeByFormat { get; } = new Dictionary<ushort, int>
    {
        [Format2024] = 1285,
        [Format2025] = 1285,
        [Format2026] = 1399,
    };

    public static IReadOnlyDictionary<ushort, int> EventTotalSizeByFormat { get; } = new Dictionary<ushort, int>
    {
        [Format2024] = 45,
        [Format2025] = 45,
        [Format2026] = 45,
    };

    public static IReadOnlyDictionary<ushort, int> ParticipantsTotalSizeByFormat { get; } = new Dictionary<ushort, int>
    {
        // F1 24 每车 60 字节（name[48]、无 livery），F1 25 每车 57 字节（name[32] + livery）。
        [Format2024] = 1350,
        [Format2025] = 1284,
        [Format2026] = 1470,
    };

    public static IReadOnlyDictionary<ushort, int> CarTelemetryTotalSizeByFormat { get; } = new Dictionary<ushort, int>
    {
        [Format2024] = 1352,
        [Format2025] = 1352,
        [Format2026] = 1448,
    };

    public static IReadOnlyDictionary<ushort, int> CarStatusTotalSizeByFormat { get; } = new Dictionary<ushort, int>
    {
        [Format2024] = 1239,
        [Format2025] = 1239,
        [Format2026] = 1445,
    };

    public static IReadOnlyDictionary<ushort, int> FinalClassificationTotalSizeByFormat { get; } = new Dictionary<ushort, int>
    {
        // F1 24 每车 45 字节（无 resultReason），F1 25 起每车 46 字节（含 resultReason）。
        [Format2024] = 1020,
        [Format2025] = 1042,
        [Format2026] = 1134,
    };

    public static IReadOnlyDictionary<ushort, int> CarDamageTotalSizeByFormat { get; } = new Dictionary<ushort, int>
    {
        // F1 24 每车 42 字节（无 tyreBlisters），F1 25 起每车 46 字节（含 tyreBlisters[4]）。
        [Format2024] = 953,
        [Format2025] = 1041,
        [Format2026] = 1133,
    };

    public static IReadOnlyDictionary<ushort, int> SessionHistoryTotalSizeByFormat { get; } = new Dictionary<ushort, int>
    {
        [Format2024] = 1460,
        [Format2025] = 1460,
        [Format2026] = 1460,
    };

    public static IReadOnlyDictionary<ushort, int> TyreSetsTotalSizeByFormat { get; } = new Dictionary<ushort, int>
    {
        [Format2024] = 231,
        [Format2025] = 231,
        [Format2026] = 231,
    };

    public static IReadOnlyDictionary<ushort, int> MotionExTotalSizeByFormat { get; } = new Dictionary<ushort, int>
    {
        // F1 24 包体 208 字节（读到 chassisYaw 止），F1 25 起追加 chassisPitch/wheelCamber/wheelCamberGain（+36）。
        [Format2024] = 237,
        [Format2025] = 273,
        [Format2026] = 273,
    };

    // F1 25 新增包，2024 格式不存在，尺寸表不含 2024。
    public static IReadOnlyDictionary<ushort, int> LapPositionsTotalSizeByFormat { get; } = new Dictionary<ushort, int>
    {
        [Format2025] = 1131,
        [Format2026] = 1231,
    };

    /// <summary>F1 26 新增包（PacketId.CarTelemetry2），仅存在于 2026 格式。</summary>
    public static IReadOnlyDictionary<ushort, int> CarTelemetry2TotalSizeByFormat { get; } = new Dictionary<ushort, int>
    {
        [Format2026] = 269,
    };

    // 版本化包体尺寸表（总尺寸 - 头）。
    public static IReadOnlyDictionary<ushort, int> MotionBodySizeByFormat { get; } = ToBodySizes(MotionTotalSizeByFormat);
    public static IReadOnlyDictionary<ushort, int> SessionBodySizeByFormat { get; } = ToBodySizes(SessionTotalSizeByFormat);
    public static IReadOnlyDictionary<ushort, int> LapDataBodySizeByFormat { get; } = ToBodySizes(LapDataTotalSizeByFormat);
    public static IReadOnlyDictionary<ushort, int> EventBodySizeByFormat { get; } = ToBodySizes(EventTotalSizeByFormat);
    public static IReadOnlyDictionary<ushort, int> ParticipantsBodySizeByFormat { get; } = ToBodySizes(ParticipantsTotalSizeByFormat);
    public static IReadOnlyDictionary<ushort, int> CarTelemetryBodySizeByFormat { get; } = ToBodySizes(CarTelemetryTotalSizeByFormat);
    public static IReadOnlyDictionary<ushort, int> CarStatusBodySizeByFormat { get; } = ToBodySizes(CarStatusTotalSizeByFormat);
    public static IReadOnlyDictionary<ushort, int> FinalClassificationBodySizeByFormat { get; } = ToBodySizes(FinalClassificationTotalSizeByFormat);
    public static IReadOnlyDictionary<ushort, int> CarDamageBodySizeByFormat { get; } = ToBodySizes(CarDamageTotalSizeByFormat);
    public static IReadOnlyDictionary<ushort, int> SessionHistoryBodySizeByFormat { get; } = ToBodySizes(SessionHistoryTotalSizeByFormat);
    public static IReadOnlyDictionary<ushort, int> TyreSetsBodySizeByFormat { get; } = ToBodySizes(TyreSetsTotalSizeByFormat);
    public static IReadOnlyDictionary<ushort, int> MotionExBodySizeByFormat { get; } = ToBodySizes(MotionExTotalSizeByFormat);
    public static IReadOnlyDictionary<ushort, int> LapPositionsBodySizeByFormat { get; } = ToBodySizes(LapPositionsTotalSizeByFormat);
    public static IReadOnlyDictionary<ushort, int> CarTelemetry2BodySizeByFormat { get; } = ToBodySizes(CarTelemetry2TotalSizeByFormat);

    /// <summary>
    /// 按协议格式返回赛季车辆数量（F1 26 为 24，其余格式为 22）。
    /// 解析器应以此决定解析循环次数与数组长度（数组分配上限为 <see cref="MaxCarsInSession"/>）。
    /// </summary>
    public static int GetMaxCarsInSession(ushort packetFormat)
    {
        return packetFormat == Format2026 ? 24 : 22;
    }

    private static IReadOnlyDictionary<ushort, int> ToBodySizes(IReadOnlyDictionary<ushort, int> totalSizesByFormat)
    {
        var bodySizes = new Dictionary<ushort, int>(totalSizesByFormat.Count);
        foreach (var (packetFormat, totalSize) in totalSizesByFormat)
        {
            bodySizes[packetFormat] = totalSize - PacketHeaderSize;
        }

        return bodySizes;
    }
}
