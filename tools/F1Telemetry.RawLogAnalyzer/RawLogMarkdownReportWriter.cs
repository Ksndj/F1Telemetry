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
