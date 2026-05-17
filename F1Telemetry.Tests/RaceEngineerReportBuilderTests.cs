using F1Telemetry.AI.Reports;
using F1Telemetry.Analytics.Corners;
using F1Telemetry.Analytics.Strategy;
using F1Telemetry.Analytics.Tracks;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies the V3 race engineer report builder keeps AI input compressed and evidence-aware.
/// </summary>
public sealed class RaceEngineerReportBuilderTests
{
    /// <summary>
    /// Verifies reports separate data-supported findings from inferred suggestions.
    /// </summary>
    [Fact]
    public void Build_WithCompactEvidence_SeparatesSupportedAndInferredText()
    {
        var builder = new RaceEngineerReportBuilder();

        var report = builder.Build(new RaceEngineerReportInput
        {
            SessionSummary = "正赛完成，长距离节奏稳定。",
            LapSummaries = ["Lap 8: 91.2s, fuel 1.4L"],
            KeyEvents = ["Lap 9: safety car"],
            Stints =
            [
                new StintSummary
                {
                    StintNumber = 1,
                    StartLap = 1,
                    EndLap = 10,
                    Tyre = "Medium",
                    AdjustedAverageLapTimeMs = 91_200
                }
            ],
            StrategyAdvices =
            [
                new StrategyAdvice
                {
                    InferredSuggestions = ["Undercut pressure may be explored if pit-exit traffic remains clear."]
                }
            ],
            CornerSummaries =
            [
                new CornerSummary
                {
                    Segment = new TrackSegment
                    {
                        SegmentId = "aus-t1",
                        Name = "Turn 1",
                        SegmentType = TrackSegmentType.Corner,
                        CornerNumber = 1,
                        StartDistanceMeters = 100,
                        EndDistanceMeters = 220
                    },
                    MinSpeedKph = 112,
                    TimeLossToReferenceInMs = 280,
                    Confidence = ConfidenceLevel.Low
                }
            ],
            DataQualityWarnings = ["Estimated track map"]
        });

        Assert.Contains(report.DataSupportedFindings, line => line.Contains("Lap evidence", StringComparison.Ordinal));
        Assert.Contains(report.InferredSuggestions, line => line.Contains("Undercut pressure", StringComparison.Ordinal));
        Assert.Contains(report.DataQualityWarnings, line => line.Contains("Estimated", StringComparison.Ordinal));
        Assert.Contains("Data-supported findings", report.Markdown, StringComparison.Ordinal);
        Assert.Contains("Inferred suggestions", report.Markdown, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies AI-safe prompts do not include raw UDP or secret-looking fragments.
    /// </summary>
    [Fact]
    public void Build_WithForbiddenFragments_RedactsPrompt()
    {
        var builder = new RaceEngineerReportBuilder();

        var report = builder.Build(new RaceEngineerReportInput
        {
            SessionSummary = "API Key should not appear. Authorization: Bearer secret. raw.jsonl packetId m_header"
        });

        Assert.DoesNotContain("API Key", report.SafePrompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Authorization:", report.SafePrompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bearer ", report.SafePrompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".jsonl", report.SafePrompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("API Key", report.Markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Authorization:", report.Markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bearer ", report.Markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".jsonl", report.Markdown, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[redacted]", report.SafePrompt, StringComparison.OrdinalIgnoreCase);
    }
}
