using F1Telemetry.AI.Models;
using F1Telemetry.AI.Services;
using F1Telemetry.Analytics.Events;
using F1Telemetry.Analytics.Laps;
using F1Telemetry.Core.Models;
using F1Telemetry.TTS;
using F1Telemetry.TTS.Models;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies the mapping from analytics and AI outputs into queue-ready TTS messages.
/// </summary>
public sealed class TtsMessageFactoryTests
{
    /// <summary>
    /// Verifies that a race event becomes a queue message with the normalized deduplication format.
    /// </summary>
    [Fact]
    public void CreateForRaceEvent_BuildsNormalizedQueueMessage()
    {
        var factory = new TtsMessageFactory();

        var message = factory.CreateForRaceEvent(
            new RaceEvent
            {
                EventType = EventType.FrontCarPitted,
                LapNumber = 8,
                VehicleIdx = 12,
                Severity = EventSeverity.Warning,
                Message = "前车进站了。"
            },
            new TtsOptions
            {
                TtsEnabled = true,
                CooldownSeconds = 8
            },
            SessionMode.Race);

        Assert.NotNull(message);
        Assert.Equal("TTS", message!.Source);
        Assert.Equal("front_pit", message.Type);
        Assert.Equal("event:front_pit:car12:lap8", message.DedupKey);
        Assert.Equal(TtsPriority.Normal, message.Priority);
        Assert.Equal(TimeSpan.FromSeconds(8), message.Cooldown);
    }

    /// <summary>
    /// Verifies that AI lap analysis uses one message per lap and enforces the minimum AI cooldown.
    /// </summary>
    [Fact]
    public void CreateForAiResult_BuildsSingleLapMessageWithMinimumCooldown()
    {
        var factory = new TtsMessageFactory();

        var message = factory.CreateForAiResult(
            new LapSummary
            {
                LapNumber = 12
            },
            new AIAnalysisResult
            {
                IsSuccess = true,
                TtsText = "这一圈节奏不错，继续保持。"
            },
            new TtsOptions
            {
                TtsEnabled = true,
                CooldownSeconds = 8
            });

        Assert.NotNull(message);
        Assert.Equal("AI", message!.Source);
        Assert.Equal("lap", message.Type);
        Assert.Equal("ai:lap:12", message.DedupKey);
        Assert.Equal(TtsPriority.Low, message.Priority);
        Assert.Equal(TimeSpan.FromSeconds(20), message.Cooldown);
    }

    /// <summary>
    /// Verifies that long AI speech is trimmed down to a short conclusion.
    /// </summary>
    [Fact]
    public void CreateForAiResult_TruncatesLongAiSpeechText()
    {
        var factory = new TtsMessageFactory();

        var message = factory.CreateForAiResult(
            new LapSummary
            {
                LapNumber = 13
            },
            new AIAnalysisResult
            {
                IsSuccess = true,
                TtsText = "这一圈整体节奏稳定，但接下来还需要继续观察燃油、轮胎、交通和前后车风险，不要展开长段分析，也不要把细节完整播报出来。"
            },
            new TtsOptions
            {
                TtsEnabled = true,
                CooldownSeconds = 8
            });

        Assert.NotNull(message);
        Assert.True(message!.Text.Length <= 48);
        Assert.EndsWith("...", message.Text, StringComparison.Ordinal);
        Assert.Equal(TtsPriority.Low, message.Priority);
    }

    /// <summary>
    /// Verifies that data-quality warnings stay in logs and never become spoken TTS.
    /// </summary>
    [Fact]
    public void CreateForRaceEvent_DataQualityWarning_ReturnsNull()
    {
        var factory = new TtsMessageFactory();

        var message = factory.CreateForRaceEvent(
            new RaceEvent
            {
                EventType = EventType.DataQualityWarning,
                Severity = EventSeverity.Information,
                Message = "Gap timing unavailable."
            },
            new TtsOptions { TtsEnabled = true },
            SessionMode.Race);

        Assert.Null(message);
    }

    /// <summary>
    /// Verifies key race event categories map to the expected race TTS priorities.
    /// </summary>
    [Theory]
    [InlineData(EventType.SafetyCar, TtsPriority.High)]
    [InlineData(EventType.YellowFlag, TtsPriority.High)]
    [InlineData(EventType.RedFlag, TtsPriority.High)]
    [InlineData(EventType.LowFuel, TtsPriority.High)]
    [InlineData(EventType.HighTyreWear, TtsPriority.High)]
    [InlineData(EventType.AttackWindow, TtsPriority.High)]
    [InlineData(EventType.DefenseWindow, TtsPriority.High)]
    [InlineData(EventType.LowErs, TtsPriority.Normal)]
    public void CreateForRaceEvent_MapsRacePriorities(EventType eventType, TtsPriority expectedPriority)
    {
        var factory = new TtsMessageFactory();

        var message = factory.CreateForRaceEvent(
            new RaceEvent
            {
                EventType = eventType,
                LapNumber = 12,
                VehicleIdx = 0,
                Severity = EventSeverity.Warning,
                Message = "Race event"
            },
            new TtsOptions
            {
                TtsEnabled = true,
                CooldownSeconds = 8
            },
            SessionMode.Race);

        Assert.NotNull(message);
        Assert.Equal(expectedPriority, message!.Priority);
    }

    /// <summary>
    /// Verifies same-type race risks are cooled down before reaching the TTS queue.
    /// </summary>
    [Fact]
    public void CreateForRaceEvent_SameTypeRaceRiskCooldown_SuppressesRepeat()
    {
        var factory = new TtsMessageFactory();

        var first = factory.CreateForRaceEvent(
            new RaceEvent
            {
                EventType = EventType.AttackWindow,
                LapNumber = 12,
                Severity = EventSeverity.Warning,
                Message = "Attack window"
            },
            new TtsOptions { TtsEnabled = true, CooldownSeconds = 8 },
            SessionMode.Race);
        var second = factory.CreateForRaceEvent(
            new RaceEvent
            {
                EventType = EventType.AttackWindow,
                LapNumber = 13,
                Severity = EventSeverity.Warning,
                Message = "Attack window again"
            },
            new TtsOptions { TtsEnabled = true, CooldownSeconds = 8 },
            SessionMode.Race);

        Assert.NotNull(first);
        Assert.Null(second);
        Assert.Equal(TimeSpan.FromSeconds(30), first!.Cooldown);
    }

    /// <summary>
    /// Verifies AI speech is limited to one message per lap before reaching the queue.
    /// </summary>
    [Fact]
    public void CreateForAiResult_SameLap_ReturnsOnlyOneMessage()
    {
        var factory = new TtsMessageFactory();
        var lap = new LapSummary { LapNumber = 12 };
        var result = new AIAnalysisResult
        {
            IsSuccess = true,
            TtsText = "Keep the rhythm."
        };
        var options = new TtsOptions { TtsEnabled = true, CooldownSeconds = 8 };

        var first = factory.CreateForAiResult(lap, result, options);
        var second = factory.CreateForAiResult(lap, result, options);

        Assert.NotNull(first);
        Assert.Null(second);
    }

    /// <summary>
    /// Verifies race pit-window and gap-window speech is suppressed outside race-like sessions.
    /// </summary>
    [Theory]
    [InlineData(EventType.FrontCarPitted)]
    [InlineData(EventType.RearCarPitted)]
    [InlineData(EventType.AttackWindow)]
    [InlineData(EventType.DefenseWindow)]
    public void CreateForRaceEvent_NonRaceStrategyEvent_ReturnsNull(EventType eventType)
    {
        var factory = new TtsMessageFactory();

        var message = factory.CreateForRaceEvent(
            new RaceEvent
            {
                EventType = eventType,
                LapNumber = 8,
                Severity = EventSeverity.Warning,
                Message = "Strategy event"
            },
            new TtsOptions { TtsEnabled = true },
            SessionMode.Qualifying);

        Assert.Null(message);
    }
}
