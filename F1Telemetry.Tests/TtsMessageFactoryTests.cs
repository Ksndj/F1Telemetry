using F1Telemetry.AI.Models;
using F1Telemetry.AI.Services;
using F1Telemetry.Analytics.Events;
using F1Telemetry.Analytics.Laps;
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
    /// Verifies that a warning race event becomes a high-priority queue message with the normalized deduplication format.
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
            });

        Assert.NotNull(message);
        Assert.Equal("TTS", message!.Source);
        Assert.Equal("front_pit", message.Type);
        Assert.Equal("event:front_pit:car12:lap8", message.DedupKey);
        Assert.Equal(TtsPriority.High, message.Priority);
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
}
