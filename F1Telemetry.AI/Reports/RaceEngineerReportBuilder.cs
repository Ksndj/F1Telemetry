using System.Globalization;
using System.Text;

namespace F1Telemetry.AI.Reports;

/// <summary>
/// Builds a compressed V3 race engineer report without raw UDP payloads or secrets.
/// </summary>
public sealed class RaceEngineerReportBuilder
{
    private static readonly string[] ForbiddenFragments =
    [
        "api key",
        "apikey",
        "authorization:",
        "bearer ",
        "\"m_header\"",
        "\"packetId\"",
        ".jsonl"
    ];

    /// <summary>
    /// Builds a deterministic race engineer report from compressed evidence.
    /// </summary>
    /// <param name="input">The compressed report input.</param>
    public RaceEngineerReport Build(RaceEngineerReportInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var supportedFindings = BuildSupportedFindings(input);
        var inferredSuggestions = BuildInferredSuggestions(input);
        var warnings = SanitizeLines(NormalizeLines(input.DataQualityWarnings));
        var summary = string.IsNullOrWhiteSpace(input.SessionSummary)
            ? "V3 race engineer report generated from compressed telemetry summaries."
            : SanitizeForbiddenFragments(input.SessionSummary.Trim());
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
            "Corner {0}: min speed {1}, time loss {2}, confidence {3}.",
            corner.Segment.Name,
            corner.MinSpeedKph is null ? "n/a" : $"{corner.MinSpeedKph.Value:0.0} kph",
            corner.TimeLossToReferenceInMs is null ? "n/a" : $"{corner.TimeLossToReferenceInMs.Value} ms",
            corner.Confidence)));

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
        foreach (var forbiddenFragment in ForbiddenFragments)
        {
            if (prompt.Contains(forbiddenFragment, StringComparison.OrdinalIgnoreCase))
            {
                prompt = prompt.Replace(forbiddenFragment, "[redacted]", StringComparison.OrdinalIgnoreCase);
            }
        }

        return prompt;
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
            .Select(SanitizeForbiddenFragments)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string SanitizeForbiddenFragments(string text)
    {
        var sanitized = text;
        foreach (var forbiddenFragment in ForbiddenFragments)
        {
            if (sanitized.Contains(forbiddenFragment, StringComparison.OrdinalIgnoreCase))
            {
                sanitized = sanitized.Replace(forbiddenFragment, "[redacted]", StringComparison.OrdinalIgnoreCase);
            }
        }

        return sanitized;
    }
}
