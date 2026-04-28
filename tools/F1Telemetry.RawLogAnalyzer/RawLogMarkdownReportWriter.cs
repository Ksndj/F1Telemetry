using System.Globalization;
using System.Text;
using F1Telemetry.Udp.Packets;

namespace F1Telemetry.RawLogAnalyzer;

internal static class RawLogMarkdownReportWriter
{
    public static string Build(RawLogAnalysisResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# F1Telemetry UDP Raw Log Analysis");
        builder.AppendLine();
        builder.AppendLine($"- Raw log: `{Path.GetFileName(result.InputPath)}`");
        builder.AppendLine($"- Generated UTC: {DateTimeOffset.UtcNow:O}");
        builder.AppendLine($"- Total JSONL lines: {result.TotalLines}");
        builder.AppendLine($"- Parsed packets: {result.ParsedPacketCount}");
        builder.AppendLine($"- Sessions: {result.Sessions.Count}");
        builder.AppendLine();
        builder.AppendLine("## Format Issues");
        builder.AppendLine();
        builder.AppendLine($"- Invalid JSON lines: {result.InvalidJsonLineCount}{FormatExamples(result.InvalidJsonLineExamples)}");
        builder.AppendLine($"- Invalid base64 lines: {result.InvalidBase64LineCount}{FormatExamples(result.InvalidBase64LineExamples)}");
        builder.AppendLine($"- Header parse failures: {result.HeaderParseFailureCount}");
        builder.AppendLine($"- Payload length mismatches: {result.PayloadLengthMismatchCount}{FormatExamples(result.PayloadLengthMismatchLineExamples)}");
        builder.AppendLine($"- Unknown packet ids: {result.UnknownPacketIdCount}");
        builder.AppendLine($"- Unsupported known packet ids: {result.UnsupportedPacketIdCount}");
        builder.AppendLine($"- Packet parse failures: {result.PacketParseFailureCount}{FormatExamples(result.PacketParseFailureLineExamples)}");
        builder.AppendLine($"- Dispatch failures: {result.DispatchFailureCount}");
        builder.AppendLine();
        AppendPacketDistribution(builder, result);
        AppendSessions(builder, result);
        builder.AppendLine("## Overview Notes");
        builder.AppendLine();
        builder.AppendLine("- Chart observations are emitted only from observed decoded samples; missing streams are marked as unavailable instead of treated as zero-value lines.");
        builder.AppendLine("- Tyre compound output reports observed visual/actual ids only and does not infer dry compound names from actual compound values.");
        builder.AppendLine("- Full-race validation should still compare session boundaries, final laps, and late-race event coverage against a complete race sample.");
        return builder.ToString();
    }

    private static void AppendPacketDistribution(StringBuilder builder, RawLogAnalysisResult result)
    {
        builder.AppendLine("## Packet Distribution");
        builder.AppendLine();

        if (result.PacketIdCounts.Count == 0)
        {
            builder.AppendLine("- No decoded packet ids.");
        }
        else
        {
            foreach (var pair in result.PacketIdCounts.OrderBy(pair => pair.Key))
            {
                builder.AppendLine($"- {(byte)pair.Key} {pair.Key}: {pair.Value}");
            }
        }

        foreach (var pair in result.UnknownPacketIdCounts)
        {
            builder.AppendLine($"- {pair.Key} Unknown: {pair.Value}");
        }

        foreach (var pair in result.UnsupportedPacketIdCounts)
        {
            builder.AppendLine($"  - note: {(byte)pair.Key} {pair.Key} has no typed parser in the current analyzer: {pair.Value}");
        }

        builder.AppendLine();
    }

    private static void AppendSessions(StringBuilder builder, RawLogAnalysisResult result)
    {
        builder.AppendLine("## Sessions");
        builder.AppendLine();

        if (result.Sessions.Count == 0)
        {
            builder.AppendLine("- No sessions were decoded.");
            builder.AppendLine();
            return;
        }

        foreach (var session in result.Sessions.Values)
        {
            builder.AppendLine($"### Session {session.SessionUid}");
            builder.AppendLine();
            builder.AppendLine($"- TrackId: {FormatNullable(session.TrackId)}");
            builder.AppendLine($"- SessionType: {FormatNullable(session.SessionType)}");
            builder.AppendLine($"- Total laps: {FormatNullable(session.TotalLaps)}");
            builder.AppendLine($"- Time range UTC: {FormatTimestamp(session.FirstSeenUtc)} -> {FormatTimestamp(session.LastSeenUtc)}");
            builder.AppendLine($"- Player lap max: {FormatNullable(session.MaxPlayerLapNumber)}");
            builder.AppendLine($"- Player distance max: {FormatFloat(session.MaxPlayerTotalDistance, session.LapSampleCount)}");
            builder.AppendLine($"- Speed max: {FormatNumber(session.MaxPlayerSpeed, session.SpeedSampleCount)}");
            builder.AppendLine($"- Throttle max: {FormatFloat(session.MaxPlayerThrottle, session.SpeedSampleCount)}");
            builder.AppendLine($"- Fuel in tank: {FormatFloat(session.MinPlayerFuelInTank, session.FuelSampleCount)} -> {FormatFloat(session.MaxPlayerFuelInTank, session.FuelSampleCount)}");
            builder.AppendLine($"- Tyre age max: {FormatNumber(session.MaxPlayerTyreAgeLaps, session.FuelSampleCount)}");
            builder.AppendLine($"- Tyre wear max: {FormatFloat(session.MaxPlayerTyreWear, session.TyreWearSampleCount)}");
            builder.AppendLine($"- Tyre compounds: {FormatSet(session.TyreCompoundPairs)}");
            builder.AppendLine($"- Event codes: {FormatCounts(session.EventCodeCounts)}");
            builder.AppendLine($"- Chart data: lap={session.LapSampleCount}, speed={session.SpeedSampleCount}, fuel={session.FuelSampleCount}, tyreWear={session.TyreWearSampleCount}");
            builder.AppendLine($"- Packet parse failures: {session.PacketParseFailureCount}");
            builder.AppendLine($"- Unknown packet ids: {session.UnknownPacketIdCount}");
            builder.AppendLine($"- Unsupported known packet ids: {session.UnsupportedPacketIdCount}");
            builder.AppendLine();
        }
    }

    private static string FormatExamples(IReadOnlyCollection<long> examples)
    {
        return examples.Count == 0
            ? string.Empty
            : $" (examples: {string.Join(", ", examples)})";
    }

    private static string FormatCounts(SortedDictionary<string, long> counts)
    {
        return counts.Count == 0
            ? "none observed"
            : string.Join(", ", counts.Select(pair => $"{pair.Key}={pair.Value}"));
    }

    private static string FormatSet(SortedSet<string> values)
    {
        return values.Count == 0
            ? "none observed"
            : string.Join(", ", values);
    }

    private static string FormatNullable(int value)
    {
        return value < 0
            ? "unknown"
            : value.ToString(CultureInfo.InvariantCulture);
    }

    private static string FormatNumber(int value, long sampleCount)
    {
        return sampleCount == 0
            ? "unavailable"
            : value.ToString(CultureInfo.InvariantCulture);
    }

    private static string FormatFloat(float value, long sampleCount)
    {
        return sampleCount == 0
            ? "unavailable"
            : value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string FormatTimestamp(DateTimeOffset? timestamp)
    {
        return timestamp is null || timestamp == DateTimeOffset.MinValue
            ? "unknown"
            : timestamp.Value.ToString("O", CultureInfo.InvariantCulture);
    }
}
