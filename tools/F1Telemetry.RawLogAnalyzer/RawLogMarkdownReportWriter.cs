using System.Globalization;
using System.Text;
using F1Telemetry.Udp.Packets;

namespace F1Telemetry.RawLogAnalyzer;

internal static class RawLogMarkdownReportWriter
{
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

    private static string FormatOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "unavailable" : value;
    }

    private static string FormatOptional(bool? value)
    {
        return value?.ToString() ?? "unavailable";
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
