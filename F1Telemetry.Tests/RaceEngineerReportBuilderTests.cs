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
            SessionSummary = "API Key should not appear. Authorization: Bearer secret. raw.jsonl packetId m_header",
            LapSummaries = ["api_key=lap-secret token=lap-token"],
            KeyEvents = ["HTTP Authorization: Bearer event-token"]
        });

        Assert.DoesNotContain("API Key", report.SafePrompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Authorization:", report.SafePrompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bearer secret", report.SafePrompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("lap-secret", report.SafePrompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("event-token", report.SafePrompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("packetId", report.SafePrompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("m_header", report.SafePrompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".jsonl", report.SafePrompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("API Key", report.Markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Authorization:", report.Markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bearer secret", report.Markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("lap-secret", report.Markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("event-token", report.Markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("packetId", report.Markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("m_header", report.Markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".jsonl", report.Markdown, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[REDACTED]", report.SafePrompt, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies estimated track-map warnings are included even when no global warning was supplied.
    /// </summary>
    [Fact]
    public void Build_WithEstimatedCornerWarning_IncludesReferenceOnlyLimitation()
    {
        var builder = new RaceEngineerReportBuilder();

        var report = builder.Build(new RaceEngineerReportInput
        {
            CornerSummaries =
            [
                new CornerSummary
                {
                    Segment = new TrackSegment { Name = "Turn 1", SegmentType = TrackSegmentType.Corner, CornerNumber = 1 },
                    MinSpeedKph = 118,
                    TimeLossToReferenceInMs = 200,
                    Confidence = ConfidenceLevel.Low,
                    Warnings = [DataQualityWarning.EstimatedTrackMap]
                }
            ]
        });

        Assert.Contains(report.DataQualityWarnings, warning => warning.Contains("EstimatedTrackMap", StringComparison.Ordinal));
        Assert.Contains("赛道分段为估算，结论仅供参考", report.Markdown, StringComparison.Ordinal);
        Assert.Contains("赛道分段为估算，结论仅供参考", report.SafePrompt, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies corner-level warnings are preserved in findings and quality warnings.
    /// </summary>
    [Fact]
    public void Build_WithCornerWarnings_DoesNotDropDataQualityWarnings()
    {
        var builder = new RaceEngineerReportBuilder();

        var report = builder.Build(new RaceEngineerReportInput
        {
            CornerSummaries =
            [
                new CornerSummary
                {
                    Segment = new TrackSegment { Name = "Turn 3", SegmentType = TrackSegmentType.Corner, CornerNumber = 3 },
                    Confidence = ConfidenceLevel.Low,
                    Warnings = [DataQualityWarning.LowSampleDensity, DataQualityWarning.MissingReferenceLap]
                }
            ]
        });

        Assert.Contains(report.DataSupportedFindings, finding => finding.Contains("LowSampleDensity", StringComparison.Ordinal));
        Assert.Contains(report.DataQualityWarnings, warning => warning.Contains("MissingReferenceLap", StringComparison.Ordinal));
        Assert.Contains("sample density is low", report.Markdown, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies unsupported or unknown-confidence corners are clearly marked as data-quality limited.
    /// </summary>
    [Fact]
    public void Build_WithUnsupportedLowConfidenceCorner_MarksDataQualityLimitation()
    {
        var builder = new RaceEngineerReportBuilder();

        var report = builder.Build(new RaceEngineerReportInput
        {
            CornerSummaries =
            [
                new CornerSummary
                {
                    Segment = new TrackSegment { Name = "Unknown", SegmentType = TrackSegmentType.Corner },
                    Confidence = ConfidenceLevel.Unknown,
                    Warnings = [DataQualityWarning.UnsupportedTrack]
                }
            ]
        });

        Assert.Contains(report.DataQualityWarnings, warning => warning.Contains("UnsupportedTrack", StringComparison.Ordinal));
        Assert.Contains(report.DataQualityWarnings, warning => warning.Contains("confidence Unknown", StringComparison.Ordinal));
        Assert.Contains("do not draw deterministic conclusions", report.SafePrompt, StringComparison.OrdinalIgnoreCase);
    }
}
