using System.IO;
using System.Xml.Linq;
using F1Telemetry.AI.Models;
using F1Telemetry.AI.Services;
using F1Telemetry.Analytics.Events;
using F1Telemetry.Analytics.Laps;
using F1Telemetry.App.ViewModels;
using F1Telemetry.Core.Formatting;
using F1Telemetry.Core.Models;
using F1Telemetry.TTS.Models;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies session-aware overview, AI prompt, and TTS behavior.
/// </summary>
public sealed class SessionAwareRaceAssistantTests
{
    /// <summary>
    /// Verifies raw F1 25 session type identifiers are grouped into assistant modes.
    /// </summary>
    [Theory]
    [InlineData(1, SessionMode.Practice)]
    [InlineData(5, SessionMode.Qualifying)]
    [InlineData(10, SessionMode.SprintQualifying)]
    [InlineData(16, SessionMode.SprintRace)]
    [InlineData(15, SessionMode.Race)]
    [InlineData(17, SessionMode.Race)]
    [InlineData(18, SessionMode.TimeTrial)]
    [InlineData(255, SessionMode.Unknown)]
    public void SessionModeFormatter_Resolve_ReturnsExpectedMode(byte sessionType, SessionMode expected)
    {
        Assert.Equal(expected, SessionModeFormatter.Resolve(sessionType));
    }

    /// <summary>
    /// Verifies sprint-weekend context disambiguates F1 25 Race packets that represent sprint races.
    /// </summary>
    [Fact]
    public void SessionModeFormatter_Resolve_WithSprintWeekendContext_ReturnsSprintRace()
    {
        var mode = SessionModeFormatter.Resolve(
            sessionType: 15,
            totalLaps: 10,
            weekendStructure: [1, 10, 15, 5, 6, 7, 17]);

        Assert.Equal(SessionMode.SprintRace, mode);
    }

    /// <summary>
    /// Verifies qualifying overview focus avoids race pit-window language.
    /// </summary>
    [Fact]
    public void SessionModeFormatter_FormatFocus_QualifyingDoesNotMentionRacePitWindows()
    {
        var focusText = SessionModeFormatter.FormatFocus(SessionMode.Qualifying);

        Assert.Contains("有效圈", focusText, StringComparison.Ordinal);
        Assert.Contains("交通", focusText, StringComparison.Ordinal);
        Assert.DoesNotContain("进站窗口", focusText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("undercut", focusText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("overcut", focusText, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies race overview focus includes race-relevant strategy cues.
    /// </summary>
    [Fact]
    public void SessionModeFormatter_FormatFocus_RaceMentionsRacePriorities()
    {
        var focusText = SessionModeFormatter.FormatFocus(SessionMode.Race);

        Assert.Contains("胎龄", focusText, StringComparison.Ordinal);
        Assert.Contains("油耗", focusText, StringComparison.Ordinal);
        Assert.Contains("前后车", focusText, StringComparison.Ordinal);
        Assert.Contains("进站窗口", focusText, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies the overview exposes a session focus binding.
    /// </summary>
    [Fact]
    public void OverviewView_BindsSessionFocusText()
    {
        Assert.NotNull(typeof(DashboardViewModel).GetProperty("OverviewSessionFocusText"));

        var document = XDocument.Load(FindRepositoryFile("F1Telemetry.App", "Views", "OverviewView.xaml"));
        var xaml = document.ToString(SaveOptions.DisableFormatting);

        Assert.Contains("OverviewSessionFocusText", xaml, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies AI prompts include session semantics.
    /// </summary>
    [Fact]
    public void PromptBuilder_BuildMessages_IncludesSessionSemantics()
    {
        var prompt = new PromptBuilder().BuildMessages(CreateContext(SessionMode.Race));

        Assert.Contains("Session mode: Race", prompt.UserMessage, StringComparison.Ordinal);
        Assert.Contains("赛制：正赛", prompt.UserMessage, StringComparison.Ordinal);
        Assert.Contains("当前赛制重点：关注胎龄、油耗、前后车、进站窗口", prompt.UserMessage, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies qualifying AI prompts avoid race strategy instructions.
    /// </summary>
    [Fact]
    public void PromptBuilder_BuildMessages_QualifyingDoesNotIncludeRaceStrategyInstructions()
    {
        var prompt = new PromptBuilder().BuildMessages(CreateContext(SessionMode.Qualifying));

        Assert.Contains("赛制：排位赛", prompt.UserMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("进站窗口", prompt.UserMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("undercut", prompt.UserMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("overcut", prompt.UserMessage, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies race AI prompts keep race strategy guidance.
    /// </summary>
    [Fact]
    public void PromptBuilder_BuildMessages_RaceIncludesRaceStrategyGuidance()
    {
        var prompt = new PromptBuilder().BuildMessages(CreateContext(SessionMode.Race));

        Assert.Contains("轮胎", prompt.UserMessage, StringComparison.Ordinal);
        Assert.Contains("燃油", prompt.UserMessage, StringComparison.Ordinal);
        Assert.Contains("交通", prompt.UserMessage, StringComparison.Ordinal);
        Assert.Contains("进站建议", prompt.UserMessage, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies qualifying suppresses race pit-window speech but keeps lap-invalid speech.
    /// </summary>
    [Fact]
    public void TtsMessageFactory_CreateForRaceEvent_FiltersRacePitEventsButKeepsHighPriorityEvents()
    {
        var factory = new TtsMessageFactory();
        var options = new TtsOptions { TtsEnabled = true, CooldownSeconds = 8 };

        Assert.Null(factory.CreateForRaceEvent(CreateRaceEvent(EventType.FrontCarPitted), options, SessionMode.Qualifying));
        Assert.NotNull(factory.CreateForRaceEvent(CreateRaceEvent(EventType.FrontCarPitted), options, SessionMode.Race));
        Assert.NotNull(factory.CreateForRaceEvent(CreateRaceEvent(EventType.PlayerLapInvalidated), options, SessionMode.Qualifying));
    }

    private static AIAnalysisContext CreateContext(SessionMode sessionMode)
    {
        return new AIAnalysisContext
        {
            SessionMode = sessionMode,
            SessionTypeText = SessionModeFormatter.FormatDisplayName(sessionMode),
            SessionFocusText = SessionModeFormatter.FormatFocus(sessionMode),
            LatestLap = new LapSummary { LapNumber = 14, LapTimeInMs = 91_000, FuelUsedLitres = 1.24f, IsValid = true },
            BestLap = new LapSummary { LapNumber = 10, LapTimeInMs = 90_300, FuelUsedLitres = 1.18f, IsValid = true },
            RecentLaps =
            [
                new LapSummary { LapNumber = 14, LapTimeInMs = 91_000, FuelUsedLitres = 1.24f, IsValid = true },
                new LapSummary { LapNumber = 13, LapTimeInMs = 91_500, FuelUsedLitres = 1.31f, IsValid = false }
            ],
            CurrentFuelInTank = 8.4f,
            CurrentFuelRemainingLaps = 5.1f,
            CurrentErsStoreEnergy = 2_250_000f,
            CurrentTyre = "红胎",
            CurrentTyreAgeLaps = 7,
            GapToFrontInMs = 1_250,
            GapToBehindInMs = 980,
            RecentEvents = ["后车已进站。"]
        };
    }

    private static RaceEvent CreateRaceEvent(EventType eventType)
    {
        return new RaceEvent
        {
            EventType = eventType,
            LapNumber = 8,
            VehicleIdx = 12,
            Severity = EventSeverity.Warning,
            Message = eventType == EventType.PlayerLapInvalidated
                ? "当前圈已无效。"
                : "前车已进站。"
        };
    }

    private static string FindRepositoryFile(params string[] pathParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(pathParts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find repository file: {Path.Combine(pathParts)}");
    }
}
