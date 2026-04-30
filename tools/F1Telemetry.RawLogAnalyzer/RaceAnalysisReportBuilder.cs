using System.Globalization;
using System.Text;
using F1Telemetry.Udp.Packets;

namespace F1Telemetry.RawLogAnalyzer;

/// <summary>
/// Builds the V1.2-M1 RaceAnalysisReport from one selected raw-log session summary.
/// </summary>
public static class RaceAnalysisReportBuilder
{
    private const int RaceSessionType = 15;
    private const int MaxAiInputPreviewCharacters = 8000;
    private const int MaxAiKeyEvents = 15;

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
        var playerRaceSummary = session.BuildPlayerRaceSummary();
        var raceEventTimeline = session.BuildRaceEventTimeline(
            lapSummaries,
            pitStopSummaries,
            tyreUsageSummaries);
        var keyEvents = BuildKeyEventSummaries(raceEventTimeline, sessionSummary.PlayerCarIndex);
        var raceAdviceQuestions = BuildRaceAdviceQuestions();
        var dataQualityWarnings = BuildWarnings(result, session);
        var dataQualityForAi = BuildDataQualityForAi(
            sessionSummary,
            stintSummaries,
            pitStopSummaries,
            raceEventTimeline,
            dataQualityWarnings,
            raceEventTimeline.Count > keyEvents.Count);
        var aiRaceSummary = BuildAiRaceSummary(
            sessionSummary,
            playerRaceSummary,
            lapSummaries,
            stintSummaries,
            pitStopSummaries,
            tyreUsageSummaries,
            fuelTrendSummary,
            ersTrendSummary,
            gapTrendSummary,
            raceEventTimeline,
            keyEvents,
            dataQualityForAi);
        var aiInputPreview = BuildAiInputPreview(aiRaceSummary, raceAdviceQuestions);

        if (aiInputPreview.Length > MaxAiInputPreviewCharacters)
        {
            dataQualityForAi = AddAiWarning(dataQualityForAi, "AI input preview was truncated.");
            aiRaceSummary = aiRaceSummary with
            {
                DataQualityLimitations = dataQualityForAi,
                IsTruncated = true
            };
            aiInputPreview = TruncateAiInputPreview(BuildAiInputPreview(aiRaceSummary, raceAdviceQuestions));
        }

        return new RaceAnalysisReport(
            GeneratedAt: generatedAt,
            InputFile: Path.GetFileName(result.InputPath),
            SessionUid: session.SessionUid,
            SessionSummary: sessionSummary,
            PlayerRaceSummary: playerRaceSummary,
            LapSummaries: lapSummaries,
            StintSummaries: stintSummaries,
            PitStopSummaries: pitStopSummaries,
            TyreUsageSummaries: tyreUsageSummaries,
            FuelTrendSummary: fuelTrendSummary,
            ErsTrendSummary: ersTrendSummary,
            GapTrendSummary: gapTrendSummary,
            RaceEventTimeline: raceEventTimeline,
            AiRaceSummary: aiRaceSummary,
            AiInputPreview: aiInputPreview,
            RaceAdviceQuestions: raceAdviceQuestions,
            DataQualityForAi: dataQualityForAi,
            DataQualityWarnings: dataQualityWarnings);
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

    private static AiRaceSummary BuildAiRaceSummary(
        RaceSessionSummary sessionSummary,
        PlayerRaceSummary playerRaceSummary,
        IReadOnlyList<RaceLapSummary> lapSummaries,
        IReadOnlyList<StintSummary> stintSummaries,
        IReadOnlyList<PitStopSummary> pitStopSummaries,
        IReadOnlyList<TyreUsageSummary> tyreUsageSummaries,
        FuelTrendSummary fuelTrendSummary,
        ErsTrendSummary ersTrendSummary,
        GapTrendSummary gapTrendSummary,
        IReadOnlyList<RaceEventTimelineEntry> raceEventTimeline,
        IReadOnlyList<string> keyEvents,
        IReadOnlyList<string> dataQualityForAi)
    {
        var validLapTimes = lapSummaries
            .Where(lap => lap.LapTimeInMs is > 0 && lap.IsValid != false)
            .Select(lap => lap.LapTimeInMs!.Value)
            .ToArray();
        var positionGain = playerRaceSummary.GridPosition is not null && playerRaceSummary.FinalPosition is not null
            ? playerRaceSummary.GridPosition.Value - playerRaceSummary.FinalPosition.Value
            : (int?)null;

        return new AiRaceSummary(
            TrackName: GetTrackName(sessionSummary.TrackId),
            TrackId: sessionSummary.TrackId,
            SessionType: sessionSummary.SessionType,
            IsRaceSession: sessionSummary.SessionType == RaceSessionType,
            TotalLaps: sessionSummary.TotalLaps,
            CompletedLaps: playerRaceSummary.CompletedLaps,
            GridPosition: playerRaceSummary.GridPosition,
            FinalPosition: playerRaceSummary.FinalPosition,
            PositionGain: positionGain,
            BestLapTimeInMs: playerRaceSummary.BestLapTimeInMs,
            AverageLapTimeInMs: validLapTimes.Length == 0 ? null : validLapTimes.Average(value => (double)value),
            PitStopCount: pitStopSummaries.Count,
            SafetyCarCount: raceEventTimeline.Count(entry => entry.EventType == RaceEventTimelineType.SafetyCar),
            VirtualSafetyCarCount: raceEventTimeline.Count(entry => entry.EventType == RaceEventTimelineType.VirtualSafetyCar),
            RedFlagCount: raceEventTimeline.Count(entry => entry.EventType == RaceEventTimelineType.RedFlag),
            Stints: BuildAiStintSummaries(stintSummaries, tyreUsageSummaries),
            Trends: BuildAiTrendSummaries(fuelTrendSummary, ersTrendSummary, gapTrendSummary, tyreUsageSummaries),
            KeyEvents: keyEvents,
            DataQualityLimitations: dataQualityForAi,
            IsTruncated: dataQualityForAi.Any(warning => warning.Contains("truncated", StringComparison.OrdinalIgnoreCase)));
    }

    private static IReadOnlyList<string> BuildAiStintSummaries(
        IReadOnlyList<StintSummary> stintSummaries,
        IReadOnlyList<TyreUsageSummary> tyreUsageSummaries)
    {
        if (stintSummaries.Count == 0)
        {
            return ["No stint summaries were decoded."];
        }

        return stintSummaries
            .Select(stint =>
            {
                var tyreUsage = tyreUsageSummaries.FirstOrDefault(summary => summary.StintIndex == stint.StintIndex);
                return string.Create(
                    CultureInfo.InvariantCulture,
                    $"Stint {stint.StintIndex}: laps {stint.StartLap}-{stint.EndLap}, compound actual={FormatOptional(stint.ActualTyreCompound)}, visual={FormatOptional(stint.VisualTyreCompound)}, tyreAge={FormatOptional(stint.StartTyreAge)}->{FormatOptional(stint.EndTyreAge)}, wear={FormatOptional(tyreUsage?.StartWearPercent)}%->{FormatOptional(tyreUsage?.EndWearPercent)}%, maxWear={FormatOptional(tyreUsage?.MaxWearPercent)}%, risk={tyreUsage?.Risk.ToString() ?? "Unknown"}, confidence={stint.Confidence}.");
            })
            .ToArray();
    }

    private static IReadOnlyList<string> BuildAiTrendSummaries(
        FuelTrendSummary fuelTrendSummary,
        ErsTrendSummary ersTrendSummary,
        GapTrendSummary gapTrendSummary,
        IReadOnlyList<TyreUsageSummary> tyreUsageSummaries)
    {
        var maxTyreWear = tyreUsageSummaries
            .Where(summary => summary.MaxWearPercent is not null)
            .Select(summary => summary.MaxWearPercent!.Value)
            .DefaultIfEmpty()
            .Max();
        var hasTyreWear = tyreUsageSummaries.Any(summary => summary.MaxWearPercent is not null);
        var tyreRisk = tyreUsageSummaries.Count == 0
            ? RaceTrendRisk.Unknown
            : tyreUsageSummaries.Max(summary => summary.Risk);

        return
        [
            $"Tyres: risk={tyreRisk}, maxWear={(hasTyreWear ? FormatOptional(maxTyreWear) : "unavailable")}%, observedStints={tyreUsageSummaries.Count}.",
            $"Fuel: risk={fuelTrendSummary.Risk}, startKg={FormatOptional(fuelTrendSummary.StartFuelKg)}, endKg={FormatOptional(fuelTrendSummary.EndFuelKg)}, usedKg={FormatOptional(fuelTrendSummary.FuelUsedKg)}, minRemainingLaps={FormatOptional(fuelTrendSummary.MinFuelRemainingLaps)}.",
            $"ERS: risk={ersTrendSummary.Risk}, minStoreMJ={FormatOptional(ersTrendSummary.MinStoreEnergyMJ)}, lowErsLaps={ersTrendSummary.LowErsLapCount}, highUsageLaps={ersTrendSummary.HighUsageLaps}, recoveryLaps={ersTrendSummary.RecoveryLaps}.",
            $"Gaps: confidence={gapTrendSummary.Confidence}, attackCandidateLaps={gapTrendSummary.AttackWindowLapCount}, defenseCandidateLaps={gapTrendSummary.DefenseWindowLapCount}, trafficImpactLaps={gapTrendSummary.TrafficImpactLapCount}, minFrontGap={FormatGapSeconds(gapTrendSummary.MinGapFrontMs)}, minBehindGap={FormatGapSeconds(gapTrendSummary.MinGapBehindMs)}."
        ];
    }

    private static IReadOnlyList<string> BuildKeyEventSummaries(
        IReadOnlyList<RaceEventTimelineEntry> raceEventTimeline,
        int playerCarIndex)
    {
        var ordered = raceEventTimeline
            .OrderBy(entry => GetPlayerRelatedPriority(entry, playerCarIndex))
            .ThenBy(GetSeverityPriority)
            .ThenBy(GetEventTypePriority)
            .ThenBy(entry => entry.Lap)
            .ThenBy(entry => entry.TimestampUtc)
            .Select((entry, index) => new KeyEventCandidate(entry, index))
            .ToArray();

        var selected = new List<KeyEventCandidate>();
        var typeCounts = new Dictionary<RaceEventTimelineType, int>();
        foreach (var candidate in ordered)
        {
            if (selected.Count >= MaxAiKeyEvents)
            {
                break;
            }

            typeCounts.TryGetValue(candidate.Entry.EventType, out var count);
            if (count >= 5)
            {
                continue;
            }

            selected.Add(candidate);
            typeCounts[candidate.Entry.EventType] = count + 1;
        }

        foreach (var candidate in ordered)
        {
            if (selected.Count >= MaxAiKeyEvents)
            {
                break;
            }

            if (selected.Any(item => item.Index == candidate.Index))
            {
                continue;
            }

            selected.Add(candidate);
        }

        return selected
            .OrderBy(candidate => candidate.Index)
            .Select(candidate => candidate.Entry)
            .Select(entry => $"Lap {entry.Lap}: {entry.EventType} ({entry.Severity}, {entry.Source}) - {TrimForPreview(entry.Message, 180)}")
            .ToArray();
    }

    private static IReadOnlyList<string> BuildDataQualityForAi(
        RaceSessionSummary sessionSummary,
        IReadOnlyList<StintSummary> stintSummaries,
        IReadOnlyList<PitStopSummary> pitStopSummaries,
        IReadOnlyList<RaceEventTimelineEntry> raceEventTimeline,
        IReadOnlyList<string> dataQualityWarnings,
        bool keyEventsTruncated)
    {
        var warnings = new List<string>();
        if (sessionSummary.SessionType != RaceSessionType)
        {
            warnings.Add("非正赛样本，仅供调试");
        }

        warnings.Add("TrackName is unavailable; using TrackId fallback.");
        if (stintSummaries.Count == 0)
        {
            warnings.Add("No stint summaries were decoded; tyre strategy advice may be limited.");
        }

        if (pitStopSummaries.Count == 0)
        {
            warnings.Add("No pit stop summaries were decoded; pit timing advice may be limited.");
        }

        if (raceEventTimeline.Count == 0)
        {
            warnings.Add("No key race events were decoded; event-based advice may be limited.");
        }

        if (keyEventsTruncated)
        {
            warnings.Add("AI input preview was truncated.");
        }

        foreach (var warning in dataQualityWarnings)
        {
            if (!string.Equals(warning, "No data quality warnings.", StringComparison.Ordinal))
            {
                warnings.Add(warning);
            }
        }

        return warnings.Count == 0
            ? ["No AI data quality warnings."]
            : warnings.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<string> BuildRaceAdviceQuestions()
    {
        return
        [
            "本场策略是否亏损？",
            "进站时机是否合理？",
            "轮胎是否用太久？",
            "哪个 stint 掉速最明显？",
            "是否存在低油或低电风险？",
            "前后车差距是否影响比赛？",
            "下次正赛应优先改进什么？"
        ];
    }

    private static string BuildAiInputPreview(
        AiRaceSummary summary,
        IReadOnlyList<string> raceAdviceQuestions)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Instructions:");
        builder.AppendLine("- 输出短结论。");
        builder.AppendLine("- 明确关键原因。");
        builder.AppendLine("- 给下次正赛建议。");
        builder.AppendLine("- 不要长篇泛泛分析。");
        builder.AppendLine("- 不要编造无数据支持的判断。");
        builder.AppendLine("- 数据不足时说明。");
        builder.AppendLine("- 区分“数据支持”和“推断”。");
        if (!summary.IsRaceSession)
        {
            builder.AppendLine("- 非正赛样本，仅供调试；不要输出强策略结论。");
        }

        builder.AppendLine();
        builder.AppendLine("RaceContext:");
        builder.AppendLine(CultureInfo.InvariantCulture, $"- TrackName: {summary.TrackName}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"- TrackId: {FormatNullable(summary.TrackId)}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"- SessionType: {FormatNullable(summary.SessionType)}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"- TotalLaps: {FormatNullable(summary.TotalLaps)}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"- CompletedLaps: {FormatOptional(summary.CompletedLaps)}");
        builder.AppendLine();
        builder.AppendLine("Result:");
        builder.AppendLine(CultureInfo.InvariantCulture, $"- GridPosition: {FormatOptional(summary.GridPosition)}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"- FinalPosition: {FormatOptional(summary.FinalPosition)}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"- PositionGain: {FormatOptional(summary.PositionGain)}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"- BestLapTimeMs: {FormatOptional(summary.BestLapTimeInMs)}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"- AverageLapTimeMs: {FormatOptional(summary.AverageLapTimeInMs)}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"- PitStops: {summary.PitStopCount}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"- SafetyCarEvents: {summary.SafetyCarCount}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"- VirtualSafetyCarEvents: {summary.VirtualSafetyCarCount}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"- RedFlagEvents: {summary.RedFlagCount}");
        AppendAiList(builder, "Stints", summary.Stints);
        AppendAiList(builder, "Trends", summary.Trends);
        AppendAiList(builder, "KeyEvents", summary.KeyEvents.Count == 0 ? ["No key race events were decoded."] : summary.KeyEvents);
        AppendAiList(builder, "DataQuality", summary.DataQualityLimitations);
        AppendAiList(builder, "Questions", raceAdviceQuestions);
        return builder.ToString();
    }

    private static IReadOnlyList<string> AddAiWarning(IReadOnlyList<string> warnings, string warning)
    {
        return warnings.Contains(warning, StringComparer.Ordinal)
            ? warnings
            : warnings.Concat([warning]).ToArray();
    }

    private static void AppendAiList(StringBuilder builder, string heading, IReadOnlyList<string> values)
    {
        builder.AppendLine();
        builder.AppendLine($"{heading}:");
        foreach (var value in values)
        {
            builder.AppendLine($"- {value}");
        }
    }

    private static string GetTrackName(int trackId)
    {
        return trackId < 0
            ? "Unknown track"
            : string.Create(CultureInfo.InvariantCulture, $"Unknown track (TrackId {trackId})");
    }

    private static int GetSeverityPriority(RaceEventTimelineEntry entry)
    {
        return entry.Severity switch
        {
            RaceEventTimelineSeverity.Critical => 0,
            RaceEventTimelineSeverity.Warning => 1,
            _ => 2
        };
    }

    private static int GetPlayerRelatedPriority(RaceEventTimelineEntry entry, int playerCarIndex)
    {
        if (playerCarIndex >= 0 && entry.RelatedVehicleIndex == playerCarIndex)
        {
            return 0;
        }

        return entry.EventType switch
        {
            RaceEventTimelineType.PitStop => 0,
            RaceEventTimelineType.TyreChange => 0,
            RaceEventTimelineType.Overtake => 0,
            RaceEventTimelineType.PositionLost => 0,
            RaceEventTimelineType.InvalidLap => 0,
            RaceEventTimelineType.LowFuel => 0,
            RaceEventTimelineType.HighTyreWear => 0,
            RaceEventTimelineType.LowErs => 0,
            RaceEventTimelineType.FinalClassification => 0,
            RaceEventTimelineType.SafetyCar => 1,
            RaceEventTimelineType.VirtualSafetyCar => 1,
            RaceEventTimelineType.RedFlag => 1,
            RaceEventTimelineType.YellowFlag => 1,
            RaceEventTimelineType.Start => 1,
            _ => 2
        };
    }

    private static int GetEventTypePriority(RaceEventTimelineEntry entry)
    {
        return entry.EventType switch
        {
            RaceEventTimelineType.Penalty => 0,
            RaceEventTimelineType.PitStop => 1,
            RaceEventTimelineType.TyreChange => 2,
            RaceEventTimelineType.SafetyCar => 3,
            RaceEventTimelineType.VirtualSafetyCar => 3,
            RaceEventTimelineType.RedFlag => 3,
            RaceEventTimelineType.LowFuel => 4,
            RaceEventTimelineType.HighTyreWear => 5,
            RaceEventTimelineType.LowErs => 6,
            RaceEventTimelineType.FinalClassification => 7,
            RaceEventTimelineType.Overtake => 8,
            RaceEventTimelineType.PositionLost => 8,
            RaceEventTimelineType.InvalidLap => 9,
            RaceEventTimelineType.Start => 10,
            RaceEventTimelineType.RaceWinner => 11,
            _ => 12
        };
    }

    private static string TruncateAiInputPreview(string preview)
    {
        if (preview.Length <= MaxAiInputPreviewCharacters)
        {
            return preview;
        }

        const string suffix = "\n[truncated]";
        return preview[..(MaxAiInputPreviewCharacters - suffix.Length)] + suffix;
    }

    private static string TrimForPreview(string value, int maxCharacters)
    {
        if (value.Length <= maxCharacters)
        {
            return value;
        }

        const string suffix = "...";
        return value[..(maxCharacters - suffix.Length)] + suffix;
    }

    private static string FormatOptional(int? value)
    {
        return value?.ToString(CultureInfo.InvariantCulture) ?? "unavailable";
    }

    private static string FormatOptional(uint? value)
    {
        return value?.ToString(CultureInfo.InvariantCulture) ?? "unavailable";
    }

    private static string FormatOptional(float? value)
    {
        return value?.ToString("0.###", CultureInfo.InvariantCulture) ?? "unavailable";
    }

    private static string FormatOptional(double? value)
    {
        return value?.ToString("0.###", CultureInfo.InvariantCulture) ?? "unavailable";
    }

    private static string FormatGapSeconds(uint? valueMs)
    {
        return valueMs is null
            ? "unavailable"
            : $"{valueMs.Value / 1000d:0.###} s";
    }

    private static string FormatNullable(int value)
    {
        return value < 0
            ? "unknown"
            : value.ToString(CultureInfo.InvariantCulture);
    }

    private sealed record KeyEventCandidate(RaceEventTimelineEntry Entry, int Index);
}
