using System.Globalization;
using System.Text;
using F1Telemetry.Analytics.Corners;
using F1Telemetry.Analytics.Tracks;
using F1Telemetry.Core.Security;

namespace F1Telemetry.AI.Reports;

/// <summary>
/// Builds a compressed V3 race engineer report without raw UDP payloads or secrets.
/// </summary>
public sealed class RaceEngineerReportBuilder
{
    /// <summary>
    /// Builds a deterministic race engineer report from compressed evidence.
    /// </summary>
    /// <param name="input">The compressed report input.</param>
    public RaceEngineerReport Build(RaceEngineerReportInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var supportedFindings = BuildSupportedFindings(input);
        var inferredSuggestions = BuildInferredSuggestions(input);
        var warnings = BuildDataQualityWarnings(input);
        var summary = string.IsNullOrWhiteSpace(input.SessionSummary)
            ? "V3 race engineer report generated from compressed telemetry summaries."
            : SensitiveContentSanitizer.Sanitize(input.SessionSummary);
        supportedFindings = SanitizeLines(supportedFindings);
        inferredSuggestions = SanitizeLines(inferredSuggestions);
        var markdown = BuildMarkdown(summary, supportedFindings, inferredSuggestions, warnings);
        var prompt = BuildSafePrompt(markdown);

        return new RaceEngineerReport
        {
            Summary = summary,
            DataSupportedFindings = supportedFindings,
            InferredSuggestions = inferredSuggestions,
            DataQualityWarnings = warnings,
            Markdown = markdown,
            SafePrompt = prompt
        };
    }

    private static IReadOnlyList<string> BuildSupportedFindings(RaceEngineerReportInput input)
    {
        var findings = new List<string>();
        findings.AddRange(NormalizeLines(input.LapSummaries).Select(line => $"Lap evidence: {line}"));
        findings.AddRange(NormalizeLines(input.KeyEvents).Select(line => $"Key event: {line}"));
        findings.AddRange(input.Stints.Select(stint => string.Format(
            CultureInfo.InvariantCulture,
            "Stint {0}: lap {1}-{2}, tyre {3}, adjusted average {4}.",
            stint.StintNumber,
            stint.StartLap,
            stint.EndLap,
            stint.Tyre,
            stint.AdjustedAverageLapTimeMs is null ? "n/a" : $"{stint.AdjustedAverageLapTimeMs.Value:0} ms")));
        findings.AddRange(input.CornerSummaries.Select(corner => string.Format(
            CultureInfo.InvariantCulture,
            "Corner {0}: min speed {1}, time loss {2}, confidence {3}, warnings {4}.",
            corner.Segment.Name,
            corner.MinSpeedKph is null ? "n/a" : $"{corner.MinSpeedKph.Value:0.0} kph",
            corner.TimeLossToReferenceInMs is null ? "n/a" : $"{corner.TimeLossToReferenceInMs.Value} ms",
            corner.Confidence,
            FormatCornerWarnings(corner))));

        return findings.Count == 0
            ? new[] { "No compact race evidence was supplied." }
            : findings.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<string> BuildInferredSuggestions(RaceEngineerReportInput input)
    {
        var suggestions = new List<string>();
        suggestions.AddRange(input.StrategyAdvices.SelectMany(advice => advice.InferredSuggestions));

        var highestLossCorner = input.CornerSummaries
            .Where(corner => corner.TimeLossToReferenceInMs is > 0)
            .OrderByDescending(corner => corner.TimeLossToReferenceInMs)
            .FirstOrDefault();
        if (highestLossCorner is not null)
        {
            suggestions.Add($"Practice focus: review {highestLossCorner.Segment.Name} because it shows the largest supported corner loss.");
        }

        if (suggestions.Count == 0)
        {
            suggestions.Add("Keep this report observational until more V3 strategy and corner evidence is available.");
        }

        return NormalizeLines(suggestions);
    }

    private static IReadOnlyList<string> BuildDataQualityWarnings(RaceEngineerReportInput input)
    {
        var warnings = new List<string>();
        warnings.AddRange(NormalizeLines(input.DataQualityWarnings));
        warnings.AddRange(input.StrategyAdvices.SelectMany(advice => NormalizeLines(advice.DataQualityWarnings)));

        foreach (var corner in input.CornerSummaries)
        {
            warnings.AddRange(corner.Warnings.Select(FormatDataQualityWarning));
            if (corner.Confidence is ConfidenceLevel.Low or ConfidenceLevel.Unknown)
            {
                warnings.Add($"Corner {corner.Segment.Name}: confidence {corner.Confidence}; treat the conclusion as a data-quality-limited observation.");
            }
        }

        return SanitizeLines(warnings);
    }

    private static string FormatCornerWarnings(CornerSummary corner)
    {
        return corner.Warnings.Count == 0
            ? "none"
            : string.Join("; ", corner.Warnings.Select(FormatDataQualityWarning));
    }

    private static string FormatDataQualityWarning(DataQualityWarning warning)
    {
        return warning switch
        {
            DataQualityWarning.EstimatedTrackMap => "EstimatedTrackMap: 赛道分段为估算，结论仅供参考。",
            DataQualityWarning.UnsupportedTrack => "UnsupportedTrack: unsupported track map; do not draw deterministic conclusions.",
            DataQualityWarning.LowSampleDensity => "LowSampleDensity: sample density is low; treat the conclusion as observational.",
            DataQualityWarning.MissingReferenceLap => "MissingReferenceLap: no reference lap was supplied; time-loss conclusions are unavailable.",
            _ => $"{warning}: data quality limitation."
        };
    }

    private static string BuildMarkdown(
        string summary,
        IReadOnlyList<string> supportedFindings,
        IReadOnlyList<string> inferredSuggestions,
        IReadOnlyList<string> warnings)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# V3 Race Engineer Report");
        builder.AppendLine();
        builder.AppendLine(summary);
        AppendSection(builder, "Data-supported findings", supportedFindings);
        AppendSection(builder, "Inferred suggestions", inferredSuggestions);
        AppendSection(builder, "Data quality warnings", warnings.Count == 0 ? ["No data quality warnings were supplied."] : warnings);
        return builder.ToString().TrimEnd();
    }

    private static string BuildSafePrompt(string markdown)
    {
        var prompt = "Generate a concise Chinese race engineer report from the following compressed evidence only. Do not invent precision beyond the listed confidence and warnings.\n\n" + markdown;
        return SensitiveContentSanitizer.Sanitize(prompt);
    }

    private static void AppendSection(StringBuilder builder, string title, IReadOnlyList<string> lines)
    {
        builder.AppendLine();
        builder.AppendLine($"## {title}");
        foreach (var line in lines)
        {
            builder.Append("- ");
            builder.AppendLine(line);
        }
    }

    private static IReadOnlyList<string> NormalizeLines(IEnumerable<string> lines)
    {
        return lines
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> SanitizeLines(IEnumerable<string> lines)
    {
        return lines
            .Select(SensitiveContentSanitizer.Sanitize)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}
