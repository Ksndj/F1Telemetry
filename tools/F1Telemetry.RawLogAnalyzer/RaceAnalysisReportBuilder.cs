using F1Telemetry.Udp.Packets;

namespace F1Telemetry.RawLogAnalyzer;

/// <summary>
/// Builds the V1.2-M1 RaceAnalysisReport from one selected raw-log session summary.
/// </summary>
public static class RaceAnalysisReportBuilder
{
    private const int RaceSessionType = 15;

    /// <summary>
    /// Creates the stable M1 race report from aggregate raw-log counters.
    /// </summary>
    public static RaceAnalysisReport Build(
        RawLogAnalysisResult result,
        RawLogSessionSummary session,
        DateTimeOffset generatedAt)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(session);

        var sessionSummary = new RaceSessionSummary(
            SessionUid: session.SessionUid,
            TrackId: session.TrackId,
            SessionType: session.SessionType,
            TotalLaps: session.TotalLaps,
            PlayerCarIndex: session.PlayerCarIndex,
            FirstSeenUtc: session.FirstSeenUtc,
            LastSeenUtc: session.LastSeenUtc,
            DatagramCount: session.DatagramCount,
            PacketCounts: new SortedDictionary<PacketId, long>(session.PacketCounts));

        var lapSummaries = session.BuildRaceLapSummaries();
        var stintSummaries = session.BuildStintSummaries();
        var pitStopSummaries = session.BuildPitStopSummaries();
        var tyreUsageSummaries = session.BuildTyreUsageSummaries(stintSummaries);
        var fuelTrendSummary = session.BuildFuelTrendSummary();
        var ersTrendSummary = session.BuildErsTrendSummary();
        var gapTrendSummary = session.BuildGapTrendSummary();

        return new RaceAnalysisReport(
            GeneratedAt: generatedAt,
            InputFile: Path.GetFileName(result.InputPath),
            SessionUid: session.SessionUid,
            SessionSummary: sessionSummary,
            PlayerRaceSummary: session.BuildPlayerRaceSummary(),
            LapSummaries: lapSummaries,
            StintSummaries: stintSummaries,
            PitStopSummaries: pitStopSummaries,
            TyreUsageSummaries: tyreUsageSummaries,
            FuelTrendSummary: fuelTrendSummary,
            ErsTrendSummary: ersTrendSummary,
            GapTrendSummary: gapTrendSummary,
            RaceEventTimeline: session.BuildRaceEventTimeline(
                lapSummaries,
                pitStopSummaries,
                tyreUsageSummaries),
            DataQualityWarnings: BuildWarnings(result, session));
    }

    /// <summary>
    /// Returns true when a session is a non-zero Race session.
    /// </summary>
    public static bool IsValidRaceSession(RawLogSessionSummary session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.SessionUid != 0 && session.SessionType == RaceSessionType;
    }

    private static IReadOnlyList<string> BuildWarnings(RawLogAnalysisResult result, RawLogSessionSummary session)
    {
        var warnings = new List<string>();

        AppendCountWarning(warnings, "Invalid JSON lines", result.InvalidJsonLineCount);
        AppendCountWarning(warnings, "Invalid base64 lines", result.InvalidBase64LineCount);
        AppendCountWarning(warnings, "Header parse failures", result.HeaderParseFailureCount);
        AppendCountWarning(warnings, "Payload length mismatches", result.PayloadLengthMismatchCount);
        AppendCountWarning(warnings, "Unknown packet ids", result.UnknownPacketIdCount);
        AppendCountWarning(warnings, "Unsupported known packet ids", result.UnsupportedPacketIdCount);
        AppendCountWarning(warnings, "Packet parse failures", result.PacketParseFailureCount);
        AppendCountWarning(warnings, "Dispatch failures", result.DispatchFailureCount);

        foreach (var warning in session.DataQualityWarnings)
        {
            warnings.Add(warning);
        }

        if (!IsValidRaceSession(session))
        {
            warnings.Add("非正赛样本，事件线仅供调试");
        }

        AddMissingPacketWarning(warnings, session, PacketId.Session);
        AddMissingPacketWarning(warnings, session, PacketId.LapData);
        AddMissingPacketWarning(warnings, session, PacketId.FinalClassification);
        AddMissingPacketWarning(warnings, session, PacketId.SessionHistory);

        if (warnings.Count == 0)
        {
            warnings.Add("No data quality warnings.");
        }

        return warnings;
    }

    private static void AppendCountWarning(ICollection<string> warnings, string label, long count)
    {
        if (count > 0)
        {
            warnings.Add($"{label}: {count}");
        }
    }

    private static void AddMissingPacketWarning(
        ICollection<string> warnings,
        RawLogSessionSummary session,
        PacketId packetId)
    {
        if (!session.PacketCounts.ContainsKey(packetId))
        {
            warnings.Add($"Missing packet type: {packetId}");
        }
    }
}
