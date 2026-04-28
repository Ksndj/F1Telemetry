using F1Telemetry.AI.Models;
using F1Telemetry.AI.Services;
using F1Telemetry.Analytics.Laps;
using F1Telemetry.Core.Formatting;
using F1Telemetry.Core.Models;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies that AI prompts use the fixed JSON contract and only lap/state/event summaries.
/// </summary>
public sealed class PromptBuilderTests
{
    /// <summary>
    /// Verifies that the system message asks for the fixed JSON response fields.
    /// </summary>
    [Fact]
    public void BuildMessages_IncludesFixedJsonContract()
    {
        var builder = new PromptBuilder();
        var prompt = builder.BuildMessages(CreateContext());

        Assert.Contains("valid JSON", prompt.SystemMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("exactly these keys", prompt.SystemMessage, StringComparison.OrdinalIgnoreCase);

        foreach (var key in new[]
        {
            "summary",
            "tyreAdvice",
            "fuelAdvice",
            "trafficAdvice",
            "ttsText"
        })
        {
            Assert.Contains(key, prompt.SystemMessage, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Verifies that the user message stays in lap, state, and event summary territory.
    /// </summary>
    [Fact]
    public void BuildMessages_UsesLapStateAndEventSummaries()
    {
        var builder = new PromptBuilder();
        var prompt = builder.BuildMessages(CreateContext());

        Assert.Contains("Latest lap:", prompt.UserMessage, StringComparison.Ordinal);
        Assert.Contains("Best lap:", prompt.UserMessage, StringComparison.Ordinal);
        Assert.Contains("Recent laps:", prompt.UserMessage, StringComparison.Ordinal);
        Assert.Contains("Current fuel remaining laps:", prompt.UserMessage, StringComparison.Ordinal);
        Assert.Contains("Current tyre:", prompt.UserMessage, StringComparison.Ordinal);
        Assert.Contains("Recent events:", prompt.UserMessage, StringComparison.Ordinal);
        Assert.Contains("Lap 14", prompt.UserMessage, StringComparison.Ordinal);
        Assert.Contains("fuel used 1.24 L", prompt.UserMessage, StringComparison.Ordinal);
        Assert.Contains("Rear car pitted.", prompt.UserMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("V16", prompt.UserMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("A16", prompt.UserMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("L16", prompt.UserMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("packet", prompt.UserMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("udp", prompt.UserMessage, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies that null recent collections do not break prompt generation.
    /// </summary>
    [Fact]
    public void BuildMessages_AllowsNullRecentCollections()
    {
        var builder = new PromptBuilder();
        var prompt = builder.BuildMessages(CreateContext(includeRecentCollections: false));

        Assert.Contains("Latest lap:", prompt.UserMessage, StringComparison.Ordinal);
        Assert.Contains("Best lap:", prompt.UserMessage, StringComparison.Ordinal);
        Assert.Contains("Current fuel in tank:", prompt.UserMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("Recent laps:", prompt.UserMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("Recent events:", prompt.UserMessage, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that the prompt explicitly asks for short, broadcast-ready output.
    /// </summary>
    [Fact]
    public void BuildMessages_RequestsShortTtsFriendlyConclusions()
    {
        var builder = new PromptBuilder();
        var prompt = builder.BuildMessages(CreateContext());
        var combinedPrompt = prompt.SystemMessage + Environment.NewLine + prompt.UserMessage;

        Assert.Contains("短结论", combinedPrompt, StringComparison.Ordinal);
        Assert.Contains("禁止长段分析", combinedPrompt, StringComparison.Ordinal);
        Assert.Contains("TTS", combinedPrompt, StringComparison.Ordinal);
        Assert.Contains("ttsText", combinedPrompt, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that qualifying prompts do not steer the AI toward race strategy calls.
    /// </summary>
    [Fact]
    public void BuildMessages_QualifyingPromptAvoidsRaceStrategyInstructions()
    {
        var builder = new PromptBuilder();
        var prompt = builder.BuildMessages(CreateContext(sessionMode: SessionMode.Qualifying));

        Assert.Contains("Session mode: Qualifying", prompt.UserMessage, StringComparison.Ordinal);
        Assert.Contains("排位赛", prompt.UserMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("进站窗口", prompt.UserMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("undercut", prompt.UserMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("overcut", prompt.UserMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("长距离策略", prompt.UserMessage, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies that race prompts keep the real-race priorities visible.
    /// </summary>
    [Fact]
    public void BuildMessages_RacePromptIncludesRealRacePriorities()
    {
        var builder = new PromptBuilder();
        var prompt = builder.BuildMessages(CreateContext(sessionMode: SessionMode.Race));

        Assert.Contains("轮胎", prompt.UserMessage, StringComparison.Ordinal);
        Assert.Contains("燃油", prompt.UserMessage, StringComparison.Ordinal);
        Assert.Contains("交通", prompt.UserMessage, StringComparison.Ordinal);
        Assert.Contains("进站", prompt.UserMessage, StringComparison.Ordinal);
        Assert.Contains("前后车风险", prompt.UserMessage, StringComparison.Ordinal);
    }

    private static AIAnalysisContext CreateContext(bool includeRecentCollections = true, SessionMode sessionMode = SessionMode.Race)
    {
        return new AIAnalysisContext
        {
            SessionMode = sessionMode,
            SessionTypeText = SessionModeFormatter.FormatDisplayName(sessionMode),
            SessionFocusText = SessionModeFormatter.FormatFocus(sessionMode),
            LatestLap = new LapSummary { LapNumber = 14, LapTimeInMs = 91_000, FuelUsedLitres = 1.24f, IsValid = true },
            BestLap = new LapSummary { LapNumber = 10, LapTimeInMs = 90_300, FuelUsedLitres = 1.18f, IsValid = true },
            RecentLaps = includeRecentCollections
                ? [
                    new LapSummary { LapNumber = 14, LapTimeInMs = 91_000, FuelUsedLitres = 1.24f, IsValid = true },
                    new LapSummary { LapNumber = 13, LapTimeInMs = 91_500, FuelUsedLitres = 1.31f, IsValid = false }
                ]
                : null!,
            CurrentFuelInTank = 8.4f,
            CurrentFuelRemainingLaps = 5.1f,
            CurrentErsStoreEnergy = 2_250_000f,
            CurrentTyre = "红胎",
            CurrentTyreAgeLaps = 7,
            GapToFrontInMs = 1_250,
            GapToBehindInMs = 980,
            RecentEvents = includeRecentCollections
                ? ["Rear car pitted."]
                : null!
        };
    }
}
