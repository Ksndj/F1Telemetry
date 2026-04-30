using System.Globalization;
using System.Text;
using F1Telemetry.Udp.Packets;

namespace F1Telemetry.RawLogAnalyzer;

internal static class RawLogMarkdownReportWriter
{
    private const int RaceSessionType = 15;

    public static string Build(RawLogAnalysisResult result)
    {
        if (result.RaceReport is null)
        {
            throw new InvalidOperationException("RaceAnalysisReport has not been generated.");
        }

        return BuildRaceReport(result.RaceReport);
    }

    private static string BuildRaceReport(RaceAnalysisReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Race Analysis Report");
        builder.AppendLine();
        builder.AppendLine($"- GeneratedAt: {report.GeneratedAt:O}");
        builder.AppendLine($"- InputFile: `{report.InputFile}`");
        builder.AppendLine($"- SessionUid: {report.SessionUid}");
        builder.AppendLine();
        AppendSessionSummary(builder, report.SessionSummary);
        AppendPlayerSummary(builder, report.PlayerRaceSummary);
        AppendLapSummaries(builder, report.LapSummaries);
        AppendStintSummaries(builder, report.StintSummaries);
        AppendPitStopSummaries(builder, report.PitStopSummaries);
        AppendTyreUsageSummaries(builder, report.TyreUsageSummaries);
        AppendFuelTrendSummary(builder, report.FuelTrendSummary);
        AppendErsTrendSummary(builder, report.ErsTrendSummary);
        AppendGapTrendSummary(builder, report.GapTrendSummary);
        AppendRaceEventTimeline(builder, report);
        AppendDataQualityWarnings(builder, report.DataQualityWarnings);
        return builder.ToString();
    }

    private static void AppendSessionSummary(StringBuilder builder, RaceSessionSummary summary)
    {
        builder.AppendLine("## Session Summary");
        builder.AppendLine();
        builder.AppendLine($"- SessionUid: {summary.SessionUid}");
        builder.AppendLine($"- TrackId: {FormatNullable(summary.TrackId)}");
        builder.AppendLine($"- SessionType: {FormatNullable(summary.SessionType)}");
        builder.AppendLine($"- Total laps: {FormatNullable(summary.TotalLaps)}");
        builder.AppendLine($"- Player car index: {FormatNullable(summary.PlayerCarIndex)}");
        builder.AppendLine($"- Time range UTC: {FormatTimestamp(summary.FirstSeenUtc)} -> {FormatTimestamp(summary.LastSeenUtc)}");
        builder.AppendLine($"- Datagram count: {summary.DatagramCount}");
        builder.AppendLine($"- Packet counts: {FormatPacketCounts(summary.PacketCounts)}");
        builder.AppendLine();
    }

    private static void AppendPlayerSummary(StringBuilder builder, PlayerRaceSummary summary)
    {
        builder.AppendLine("## Player Summary");
        builder.AppendLine();
        builder.AppendLine($"- Grid position: {FormatOptional(summary.GridPosition)}");
        builder.AppendLine($"- Final position: {FormatOptional(summary.FinalPosition)}");
        builder.AppendLine($"- Completed laps: {FormatOptional(summary.CompletedLaps)}");
        builder.AppendLine($"- Points: {FormatOptional(summary.Points)}");
        builder.AppendLine($"- Best lap time ms: {FormatOptional(summary.BestLapTimeInMs)}");
        builder.AppendLine($"- Penalties time seconds: {FormatOptional(summary.PenaltiesTimeSeconds)}");
        builder.AppendLine($"- Penalties count: {FormatOptional(summary.NumPenalties)}");
        builder.AppendLine();
    }

    private static void AppendLapSummaries(StringBuilder builder, IReadOnlyList<RaceLapSummary> lapSummaries)
    {
        builder.AppendLine("## Lap Summaries");
        builder.AppendLine();

        if (lapSummaries.Count == 0)
        {
            builder.AppendLine("- No lap summaries were decoded.");
            builder.AppendLine();
            return;
        }

        builder.AppendLine("| Lap | Lap time ms | Position | Valid | Result status | Samples |");
        builder.AppendLine("| ---: | ---: | ---: | --- | ---: | ---: |");
        foreach (var lap in lapSummaries)
        {
            builder.AppendLine($"| {lap.LapNumber} | {FormatOptional(lap.LapTimeInMs)} | {FormatOptional(lap.Position)} | {FormatOptional(lap.IsValid)} | {FormatOptional(lap.ResultStatus)} | {lap.SampleCount} |");
        }

        builder.AppendLine();
    }

    private static void AppendStintSummaries(StringBuilder builder, IReadOnlyList<StintSummary> stintSummaries)
    {
        builder.AppendLine("## Stint Summaries");
        builder.AppendLine();

        if (stintSummaries.Count == 0)
        {
            builder.AppendLine("- No stint summaries were decoded.");
            builder.AppendLine();
            return;
        }

        builder.AppendLine("| Stint | Start lap | End lap | Laps | Actual | Visual | Start age | End age | Source | Confidence | Notes |");
        builder.AppendLine("| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- | --- | --- |");
        foreach (var stint in stintSummaries)
        {
            builder.AppendLine($"| {stint.StintIndex} | {stint.StartLap} | {stint.EndLap} | {stint.LapCount} | {FormatOptional(stint.ActualTyreCompound)} | {FormatOptional(stint.VisualTyreCompound)} | {FormatOptional(stint.StartTyreAge)} | {FormatOptional(stint.EndTyreAge)} | {stint.Source} | {stint.Confidence} | {stint.Notes} |");
        }

        builder.AppendLine();
    }

    private static void AppendPitStopSummaries(StringBuilder builder, IReadOnlyList<PitStopSummary> pitStopSummaries)
    {
        builder.AppendLine("## Pit Stop Summary");
        builder.AppendLine();

        if (pitStopSummaries.Count == 0)
        {
            builder.AppendLine("- No pit stops were decoded.");
            builder.AppendLine();
            return;
        }

        builder.AppendLine("| Pit lap | Entry lap ms | Exit lap ms | Compound before | Compound after | Tyre age before | Tyre age after | Position before | Position after | Position lost | Estimated pit loss ms | Confidence | Notes |");
        builder.AppendLine("| ---: | ---: | ---: | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | --- | --- |");
        foreach (var pitStop in pitStopSummaries)
        {
            builder.AppendLine($"| {pitStop.PitLap} | {FormatOptional(pitStop.EntryLapTimeInMs)} | {FormatOptional(pitStop.ExitLapTimeInMs)} | {FormatOptional(pitStop.CompoundBefore)} | {FormatOptional(pitStop.CompoundAfter)} | {FormatOptional(pitStop.TyreAgeBefore)} | {FormatOptional(pitStop.TyreAgeAfter)} | {FormatOptional(pitStop.PositionBefore)} | {FormatOptional(pitStop.PositionAfter)} | {FormatOptional(pitStop.PositionLost)} | {FormatOptional(pitStop.EstimatedPitLossInMs)} | {pitStop.Confidence} | {pitStop.Notes} |");
        }

        builder.AppendLine();
    }

    private static void AppendTyreUsageSummaries(StringBuilder builder, IReadOnlyList<TyreUsageSummary> tyreUsageSummaries)
    {
        builder.AppendLine("## Tyre Usage Summary");
        builder.AppendLine();

        if (tyreUsageSummaries.Count == 0)
        {
            builder.AppendLine("- No tyre usage summaries were decoded.");
            builder.AppendLine();
            return;
        }

        builder.AppendLine("| Stint | Start lap | End lap | Laps | Actual | Visual | Start age | End age | Start wear % | End wear % | Max wear % | Wear delta % | Avg wear/lap % | Observed laps | Risk | Confidence | Notes |");
        builder.AppendLine("| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- | --- | --- |");
        foreach (var summary in tyreUsageSummaries)
        {
            builder.AppendLine($"| {summary.StintIndex} | {summary.StartLap} | {summary.EndLap} | {summary.LapCount} | {FormatOptional(summary.ActualTyreCompound)} | {FormatOptional(summary.VisualTyreCompound)} | {FormatOptional(summary.StartTyreAge)} | {FormatOptional(summary.EndTyreAge)} | {FormatOptional(summary.StartWearPercent)} | {FormatOptional(summary.EndWearPercent)} | {FormatOptional(summary.MaxWearPercent)} | {FormatOptional(summary.WearDeltaPercent)} | {FormatOptional(summary.AverageWearPerLapPercent)} | {summary.ObservedLapCount} | {summary.Risk} | {summary.Confidence} | {summary.Notes} |");
        }

        builder.AppendLine();
    }

    private static void AppendFuelTrendSummary(StringBuilder builder, FuelTrendSummary summary)
    {
        builder.AppendLine("## Fuel Trend Summary");
        builder.AppendLine();
        builder.AppendLine($"- Start fuel kg: {FormatOptional(summary.StartFuelKg)}");
        builder.AppendLine($"- End fuel kg: {FormatOptional(summary.EndFuelKg)}");
        builder.AppendLine($"- Min fuel kg: {FormatOptional(summary.MinFuelKg)}");
        builder.AppendLine($"- Max fuel kg: {FormatOptional(summary.MaxFuelKg)}");
        builder.AppendLine($"- Fuel used kg: {FormatOptional(summary.FuelUsedKg)}");
        builder.AppendLine($"- Average fuel per lap kg: {FormatOptional(summary.AverageFuelPerLapKg)}");
        builder.AppendLine($"- Start fuel remaining laps: {FormatOptional(summary.StartFuelRemainingLaps)}");
        builder.AppendLine($"- End fuel remaining laps: {FormatOptional(summary.EndFuelRemainingLaps)}");
        builder.AppendLine($"- Min fuel remaining laps: {FormatOptional(summary.MinFuelRemainingLaps)}");
        builder.AppendLine($"- Observed lap count: {summary.ObservedLapCount}");
        builder.AppendLine($"- Risk: {summary.Risk}");
        builder.AppendLine($"- Confidence: {summary.Confidence}");
        builder.AppendLine($"- Notes: {summary.Notes}");
        builder.AppendLine();
    }

    private static void AppendErsTrendSummary(StringBuilder builder, ErsTrendSummary summary)
    {
        builder.AppendLine("## ERS Trend Summary");
        builder.AppendLine();
        builder.AppendLine($"- Start store energy MJ: {FormatOptional(summary.StartStoreEnergyMJ)}");
        builder.AppendLine($"- End store energy MJ: {FormatOptional(summary.EndStoreEnergyMJ)}");
        builder.AppendLine($"- Min store energy MJ: {FormatOptional(summary.MinStoreEnergyMJ)}");
        builder.AppendLine($"- Max store energy MJ: {FormatOptional(summary.MaxStoreEnergyMJ)}");
        builder.AppendLine($"- Net store energy delta MJ: {FormatOptional(summary.NetStoreEnergyDeltaMJ)}");
        builder.AppendLine($"- Average harvested per lap MJ: {FormatOptional(summary.AverageHarvestedPerLapMJ)}");
        builder.AppendLine($"- Average deployed per lap MJ: {FormatOptional(summary.AverageDeployedPerLapMJ)}");
        builder.AppendLine($"- Last deploy mode: {FormatOptional(summary.LastDeployMode)}");
        builder.AppendLine($"- Low ERS lap count: {summary.LowErsLapCount}");
        builder.AppendLine($"- High usage laps: {summary.HighUsageLaps}");
        builder.AppendLine($"- Recovery laps: {summary.RecoveryLaps}");
        builder.AppendLine($"- Observed lap count: {summary.ObservedLapCount}");
        builder.AppendLine($"- Risk: {summary.Risk}");
        builder.AppendLine($"- Confidence: {summary.Confidence}");
        builder.AppendLine($"- Notes: {summary.Notes}");
        builder.AppendLine();
    }

    private static void AppendGapTrendSummary(StringBuilder builder, GapTrendSummary summary)
    {
        builder.AppendLine("## Gap Trend Summary");
        builder.AppendLine();
        builder.AppendLine($"- Observed lap count: {summary.ObservedLapCount}");
        builder.AppendLine($"- Attack candidate lap count: {summary.AttackWindowLapCount}");
        builder.AppendLine($"- Defense candidate lap count: {summary.DefenseWindowLapCount}");
        builder.AppendLine($"- Traffic impact lap count: {summary.TrafficImpactLapCount}");
        builder.AppendLine($"- Min front gap: {FormatGapSeconds(summary.MinGapFrontMs)}");
        builder.AppendLine($"- Average front gap: {FormatGapSeconds(summary.AverageGapFrontMs)}");
        builder.AppendLine($"- Min behind gap: {FormatGapSeconds(summary.MinGapBehindMs)}");
        builder.AppendLine($"- Average behind gap: {FormatGapSeconds(summary.AverageGapBehindMs)}");
        builder.AppendLine($"- Confidence: {summary.Confidence}");
        builder.AppendLine($"- Notes: {summary.Notes}");
        builder.AppendLine();

        AppendGapWindows(builder, "Attack Candidates", summary.AttackWindows);
        AppendGapWindows(builder, "Defense Candidates", summary.DefenseWindows);
        AppendTrafficImpactLaps(builder, summary.TrafficImpactLaps);
    }

    private static void AppendGapWindows(
        StringBuilder builder,
        string heading,
        IReadOnlyList<GapWindowSummary> windows)
    {
        builder.AppendLine($"### {heading}");
        builder.AppendLine();
        if (windows.Count == 0)
        {
            builder.AppendLine("- None.");
            builder.AppendLine();
            return;
        }

        builder.AppendLine("| Type | Start lap | End lap | Laps | Min front gap | Avg front gap | Min behind gap | Avg behind gap | Start pos | End pos | Confidence | Notes |");
        builder.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- | --- |");
        foreach (var window in windows)
        {
            builder.AppendLine($"| {window.WindowType} | {window.StartLap} | {window.EndLap} | {window.LapCount} | {FormatGapSeconds(window.MinGapFrontMs)} | {FormatGapSeconds(window.AverageGapFrontMs)} | {FormatGapSeconds(window.MinGapBehindMs)} | {FormatGapSeconds(window.AverageGapBehindMs)} | {FormatOptional(window.StartPosition)} | {FormatOptional(window.EndPosition)} | {window.Confidence} | {window.Notes} |");
        }

        builder.AppendLine();
    }

    private static void AppendTrafficImpactLaps(
        StringBuilder builder,
        IReadOnlyList<TrafficImpactLapSummary> trafficImpactLaps)
    {
        builder.AppendLine("### Traffic Impact Laps");
        builder.AppendLine();
        if (trafficImpactLaps.Count == 0)
        {
            builder.AppendLine("- None.");
            builder.AppendLine();
            return;
        }

        builder.AppendLine("| Lap | Position | Front gap | Behind gap | Type | Confidence | Notes |");
        builder.AppendLine("| ---: | ---: | ---: | ---: | --- | --- | --- |");
        foreach (var lap in trafficImpactLaps)
        {
            builder.AppendLine($"| {lap.LapNumber} | {FormatOptional(lap.Position)} | {FormatGapSeconds(lap.GapFrontMs)} | {FormatGapSeconds(lap.GapBehindMs)} | {lap.ImpactType} | {lap.Confidence} | {lap.Notes} |");
        }

        builder.AppendLine();
    }

    private static void AppendRaceEventTimeline(StringBuilder builder, RaceAnalysisReport report)
    {
        builder.AppendLine("## Race Event Timeline");
        builder.AppendLine();

        if (report.SessionSummary.SessionType != RaceSessionType)
        {
            builder.AppendLine("- Notes: 非正赛样本，事件线仅供调试");
            builder.AppendLine();
        }

        if (report.RaceEventTimeline.Count == 0)
        {
            builder.AppendLine("- No key race events were decoded.");
            builder.AppendLine();
            return;
        }

        builder.AppendLine("| Lap | Timestamp UTC | Type | Severity | Source | Related vehicle | Confidence | Message |");
        builder.AppendLine("| ---: | --- | --- | --- | --- | ---: | --- | --- |");
        foreach (var entry in report.RaceEventTimeline)
        {
            builder.AppendLine($"| {entry.Lap} | {FormatTimestamp(entry.TimestampUtc)} | {entry.EventType} | {entry.Severity} | {entry.Source} | {FormatOptional(entry.RelatedVehicleIndex)} | {entry.Confidence} | {entry.Message} |");
        }

        builder.AppendLine();
    }

    private static void AppendDataQualityWarnings(StringBuilder builder, IReadOnlyList<string> warnings)
    {
        builder.AppendLine("## Data Quality Warnings");
        builder.AppendLine();

        foreach (var warning in warnings)
        {
            builder.AppendLine($"- {warning}");
        }

        builder.AppendLine();
    }

    private static string FormatPacketCounts(IReadOnlyDictionary<PacketId, long> counts)
    {
        return counts.Count == 0
            ? "none observed"
            : string.Join(", ", counts.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value}"));
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

    private static string FormatOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "unavailable" : value;
    }

    private static string FormatOptional(bool? value)
    {
        return value?.ToString() ?? "unavailable";
    }

    private static string FormatGapSeconds(uint? valueMs)
    {
        return valueMs is null
            ? "unavailable"
            : FormatGapSeconds((double)valueMs.Value);
    }

    private static string FormatGapSeconds(double? valueMs)
    {
        return valueMs is null
            ? "unavailable"
            : FormatGapSeconds(valueMs.Value);
    }

    private static string FormatGapSeconds(double valueMs)
    {
        return $"{valueMs / 1000d:0.###} s";
    }

    private static string FormatNullable(int value)
    {
        return value < 0
            ? "unknown"
            : value.ToString(CultureInfo.InvariantCulture);
    }

    private static string FormatTimestamp(DateTimeOffset? timestamp)
    {
        return timestamp is null || timestamp == DateTimeOffset.MinValue
            ? "unknown"
            : timestamp.Value.ToString("O", CultureInfo.InvariantCulture);
    }
}
