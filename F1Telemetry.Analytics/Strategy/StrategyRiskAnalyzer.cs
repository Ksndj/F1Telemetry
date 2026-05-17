using System.Globalization;

namespace F1Telemetry.Analytics.Strategy;

/// <summary>
/// Produces semi-empirical undercut and overcut risk advice from compact strategy evidence.
/// </summary>
public sealed class StrategyRiskAnalyzer
{
    private static readonly string[] RequiredDataFields =
    [
        "gap-to-front-ms",
        "gap-to-behind-ms",
        "estimated-pit-loss-ms",
        "current-tyre",
        "current-tyre-age-laps",
        "fresh-tyre-pace-gain-ms"
    ];

    /// <summary>
    /// Analyzes undercut and overcut evidence without issuing absolute strategy commands.
    /// </summary>
    /// <param name="input">The compact strategy risk evidence.</param>
    /// <returns>Conditional strategy advice with confidence, risk, required data, and missing data.</returns>
    public StrategyAdvice Analyze(StrategyRiskInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var missingData = FindMissingData(input);
        if (missingData.Count > 0)
        {
            return BuildInsufficientDataAdvice(input, missingData);
        }

        var riskLevel = EstimateRiskLevel(input);
        var adviceType = SelectAdviceType(input);
        return adviceType switch
        {
            StrategyAdviceType.Undercut => BuildUndercutAdvice(input, riskLevel),
            StrategyAdviceType.Overcut => BuildOvercutAdvice(input, riskLevel),
            _ => BuildObserveAdvice(input, riskLevel)
        };
    }

    private static IReadOnlyList<string> FindMissingData(StrategyRiskInput input)
    {
        var missingData = new List<string>();
        if (input.GapToCarAheadMs is null)
        {
            missingData.Add("gap-to-front-ms");
        }

        if (input.GapToCarBehindMs is null)
        {
            missingData.Add("gap-to-behind-ms");
        }

        if (input.EstimatedPitLossMs is null)
        {
            missingData.Add("estimated-pit-loss-ms");
        }

        if (string.IsNullOrWhiteSpace(input.CurrentTyre) || input.CurrentTyre == "-")
        {
            missingData.Add("current-tyre");
        }

        if (input.CurrentTyreAgeLaps is null)
        {
            missingData.Add("current-tyre-age-laps");
        }

        if (input.FreshTyrePaceGainPerLapMs is null)
        {
            missingData.Add("fresh-tyre-pace-gain-ms");
        }

        return missingData;
    }

    private static StrategyAdvice BuildInsufficientDataAdvice(
        StrategyRiskInput input,
        IReadOnlyList<string> missingData)
    {
        return new StrategyAdvice
        {
            AdviceType = StrategyAdviceType.InsufficientData,
            Confidence = 0.15,
            RiskLevel = StrategyRiskLevel.Unknown,
            Summary = "Insufficient data: observe the window until pit loss, gaps, and tyre state are available.",
            RequiredData = RequiredDataFields,
            MissingData = missingData,
            SupportedFindings = BuildSupportedFindings(input),
            InferredSuggestions =
            [
                "Keep this as observation mode until the missing strategy evidence is captured."
            ],
            DataQualityWarnings = MergeWarnings(
                input.DataQualityWarnings,
                "Undercut and overcut estimates require pit loss, gap, tyre, and pace-gain evidence.")
        };
    }

    private static StrategyAdvice BuildUndercutAdvice(StrategyRiskInput input, StrategyRiskLevel riskLevel)
    {
        return new StrategyAdvice
        {
            AdviceType = StrategyAdviceType.Undercut,
            Confidence = EstimateConfidence(input, StrategyAdviceType.Undercut),
            RiskLevel = riskLevel,
            Summary = string.Format(
                CultureInfo.InvariantCulture,
                "Consider an undercut scenario: the car ahead is {0:0.0}s away and estimated fresh-tyre gain is {1:0.0}s/lap; keep it conditional on pit-exit traffic.",
                input.GapToCarAheadMs!.Value / 1000d,
                input.FreshTyrePaceGainPerLapMs!.Value / 1000d),
            RequiredData = RequiredDataFields,
            MissingData = Array.Empty<string>(),
            SupportedFindings = BuildSupportedFindings(input),
            InferredSuggestions =
            [
                "Undercut pressure may be explored if pit-exit traffic remains clear and tyre warm-up is acceptable."
            ],
            DataQualityWarnings = BuildStandardWarnings(input)
        };
    }

    private static StrategyAdvice BuildOvercutAdvice(StrategyRiskInput input, StrategyRiskLevel riskLevel)
    {
        return new StrategyAdvice
        {
            AdviceType = StrategyAdviceType.Overcut,
            Confidence = EstimateConfidence(input, StrategyAdviceType.Overcut),
            RiskLevel = riskLevel,
            Summary = string.Format(
                CultureInfo.InvariantCulture,
                "Consider an overcut or stint extension: tyre age is {0} laps and estimated fresh-tyre gain is only {1:0.0}s/lap; keep monitoring drop-off and traffic.",
                input.CurrentTyreAgeLaps!.Value,
                input.FreshTyrePaceGainPerLapMs!.Value / 1000d),
            RequiredData = RequiredDataFields,
            MissingData = Array.Empty<string>(),
            SupportedFindings = BuildSupportedFindings(input),
            InferredSuggestions =
            [
                "Overcut pressure may be explored while lap-time degradation stays controlled and rear pressure remains manageable."
            ],
            DataQualityWarnings = BuildStandardWarnings(input)
        };
    }

    private static StrategyAdvice BuildObserveAdvice(StrategyRiskInput input, StrategyRiskLevel riskLevel)
    {
        return new StrategyAdvice
        {
            AdviceType = StrategyAdviceType.Observe,
            Confidence = EstimateConfidence(input, StrategyAdviceType.Observe),
            RiskLevel = riskLevel,
            Summary = "Observe the strategy window: current evidence does not make undercut or overcut clearly stronger.",
            RequiredData = RequiredDataFields,
            MissingData = Array.Empty<string>(),
            SupportedFindings = BuildSupportedFindings(input),
            InferredSuggestions =
            [
                "Keep comparing pace drop-off, pit-exit traffic, and gaps before turning this into a stronger call."
            ],
            DataQualityWarnings = BuildStandardWarnings(input)
        };
    }

    private static StrategyAdviceType SelectAdviceType(StrategyRiskInput input)
    {
        var frontGapMs = input.GapToCarAheadMs!.Value;
        var behindGapMs = input.GapToCarBehindMs!.Value;
        var tyreAge = input.CurrentTyreAgeLaps!.Value;
        var freshTyreGainMs = input.FreshTyrePaceGainPerLapMs!.Value;

        if (frontGapMs <= 3_000d && tyreAge >= 8 && freshTyreGainMs >= 450d)
        {
            return StrategyAdviceType.Undercut;
        }

        if (frontGapMs >= 3_000d && behindGapMs >= 3_000d && tyreAge <= 6 && freshTyreGainMs <= 500d)
        {
            return StrategyAdviceType.Overcut;
        }

        return StrategyAdviceType.Observe;
    }

    private static StrategyRiskLevel EstimateRiskLevel(StrategyRiskInput input)
    {
        if (input.PitExitTrafficRisk == true)
        {
            return StrategyRiskLevel.High;
        }

        var behindGapMs = input.GapToCarBehindMs!.Value;
        if (behindGapMs < 2_000d)
        {
            return StrategyRiskLevel.High;
        }

        if (behindGapMs < 4_000d || input.EstimatedPitLossMs!.Value >= 27_000d)
        {
            return StrategyRiskLevel.Medium;
        }

        return StrategyRiskLevel.Low;
    }

    private static double EstimateConfidence(StrategyRiskInput input, StrategyAdviceType adviceType)
    {
        var confidence = adviceType switch
        {
            StrategyAdviceType.Undercut => 0.64d,
            StrategyAdviceType.Overcut => 0.60d,
            _ => 0.46d
        };

        if (input.PitExitTrafficRisk is not null)
        {
            confidence += 0.04d;
        }

        if (adviceType == StrategyAdviceType.Undercut && input.FreshTyrePaceGainPerLapMs!.Value >= 750d)
        {
            confidence += 0.08d;
        }

        if (adviceType == StrategyAdviceType.Overcut && input.CurrentTyreAgeLaps!.Value <= 4)
        {
            confidence += 0.06d;
        }

        return Math.Round(Math.Clamp(confidence, 0d, 0.85d), 2);
    }

    private static IReadOnlyList<string> BuildSupportedFindings(StrategyRiskInput input)
    {
        var findings = new List<string>();
        if (input.CurrentLapNumber is not null)
        {
            findings.Add(string.Format(CultureInfo.InvariantCulture, "Current lap: {0}.", input.CurrentLapNumber.Value));
        }

        if (input.GapToCarAheadMs is not null)
        {
            findings.Add(string.Format(CultureInfo.InvariantCulture, "Gap to car ahead: {0:0.0}s.", input.GapToCarAheadMs.Value / 1000d));
        }

        if (input.GapToCarBehindMs is not null)
        {
            findings.Add(string.Format(CultureInfo.InvariantCulture, "Gap to car behind: {0:0.0}s.", input.GapToCarBehindMs.Value / 1000d));
        }

        if (input.EstimatedPitLossMs is not null)
        {
            findings.Add(string.Format(CultureInfo.InvariantCulture, "Estimated pit loss: {0:0.0}s.", input.EstimatedPitLossMs.Value / 1000d));
        }

        if (!string.IsNullOrWhiteSpace(input.CurrentTyre))
        {
            findings.Add(string.Format(CultureInfo.InvariantCulture, "Current tyre: {0}.", input.CurrentTyre!.Trim()));
        }

        if (input.CurrentTyreAgeLaps is not null)
        {
            findings.Add(string.Format(CultureInfo.InvariantCulture, "Current tyre age: {0} laps.", input.CurrentTyreAgeLaps.Value));
        }

        if (input.FreshTyrePaceGainPerLapMs is not null)
        {
            findings.Add(string.Format(CultureInfo.InvariantCulture, "Estimated fresh-tyre gain: {0:0.0}s/lap.", input.FreshTyrePaceGainPerLapMs.Value / 1000d));
        }

        return findings;
    }

    private static IReadOnlyList<string> BuildStandardWarnings(StrategyRiskInput input)
    {
        return MergeWarnings(
            input.DataQualityWarnings,
            "Semi-empirical estimate only; confirm live traffic, compound rules, and neutralized-race status.");
    }

    private static IReadOnlyList<string> MergeWarnings(
        IReadOnlyList<string> existingWarnings,
        string warning)
    {
        return existingWarnings
            .Where(existingWarning => !string.IsNullOrWhiteSpace(existingWarning))
            .Select(existingWarning => existingWarning.Trim())
            .Append(warning)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}
