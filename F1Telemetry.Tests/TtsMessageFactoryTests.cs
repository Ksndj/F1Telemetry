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
        Assert.Equal(TimeSpan.FromSeconds(10), message.Cooldown);
    }
}
