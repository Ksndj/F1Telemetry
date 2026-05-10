using F1Telemetry.Analytics.Events;
using F1Telemetry.App.Services;
using F1Telemetry.Core.Eventing;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies recent race-event caching for AI insight prompts.
/// </summary>
public sealed class RaceEventInsightBufferTests
{
    /// <summary>
    /// Verifies a published race-event message is cached.
    /// </summary>
    [Fact]
    public void CaptureMessages_AfterRaceEventPublished_ReturnsMessage()
    {
        var eventBus = new InMemoryEventBus<RaceEvent>();
        using var buffer = new RaceEventInsightBuffer(eventBus);

        eventBus.Publish(CreateRaceEvent("  前车进站  "));

        Assert.Equal(new[] { "前车进站" }, buffer.CaptureMessages());
    }

    /// <summary>
    /// Verifies new race-advice events are retained for recent AI insight context.
    /// </summary>
    [Fact]
    public void CaptureMessages_AfterM6AdviceRaceEventPublished_ReturnsMessage()
    {
        var eventBus = new InMemoryEventBus<RaceEvent>();
        using var buffer = new RaceEventInsightBuffer(eventBus);

        eventBus.Publish(CreateRaceEvent("前车旧胎风险，适合持续施压", EventType.FrontOldTyreRisk));

        Assert.Equal(new[] { "前车旧胎风险，适合持续施压" }, buffer.CaptureMessages());
    }

    /// <summary>
    /// Verifies data-quality warnings and blank placeholder messages are ignored.
    /// </summary>
    [Fact]
    public void CaptureMessages_IgnoresDataQualityWarningAndEmptyMessages()
    {
        var eventBus = new InMemoryEventBus<RaceEvent>();
        using var buffer = new RaceEventInsightBuffer(eventBus);

        eventBus.Publish(CreateRaceEvent("缺少实时排名数据", EventType.DataQualityWarning));
        eventBus.Publish(CreateRaceEvent(string.Empty));
        eventBus.Publish(CreateRaceEvent("   "));
        eventBus.Publish(CreateRaceEvent("-"));

        Assert.Empty(buffer.CaptureMessages());
    }

    /// <summary>
    /// Verifies the default buffer retains only the most recent eight messages.
    /// </summary>
    [Fact]
    public void CaptureMessages_DefaultCapacityKeepsMostRecentEight()
    {
        var eventBus = new InMemoryEventBus<RaceEvent>();
        using var buffer = new RaceEventInsightBuffer(eventBus);

        for (var i = 1; i <= 10; i++)
        {
            eventBus.Publish(CreateRaceEvent($"事件 {i}"));
        }

        Assert.Equal(new[]
        {
            "事件 3",
            "事件 4",
            "事件 5",
            "事件 6",
            "事件 7",
            "事件 8",
            "事件 9",
            "事件 10"
        }, buffer.CaptureMessages());
    }

    /// <summary>
    /// Verifies reset clears all retained messages.
    /// </summary>
    [Fact]
    public void Reset_ClearsMessages()
    {
        var eventBus = new InMemoryEventBus<RaceEvent>();
        using var buffer = new RaceEventInsightBuffer(eventBus);

        eventBus.Publish(CreateRaceEvent("安全车"));

        buffer.Reset();

        Assert.Empty(buffer.CaptureMessages());
    }

    /// <summary>
    /// Verifies concurrent publishing and capturing is safe and returns copied snapshots.
    /// </summary>
    [Fact]
    public async Task CaptureMessages_WhilePublishingConcurrently_ReturnsStableSnapshots()
    {
        var eventBus = new InMemoryEventBus<RaceEvent>();
        using var buffer = new RaceEventInsightBuffer(eventBus);
        var snapshots = new List<string[]>();

        var publisher = Task.Run(() =>
        {
            for (var i = 0; i < 64; i++)
            {
                eventBus.Publish(CreateRaceEvent($"事件 {i}"));
            }
        });

        var capturer = Task.Run(() =>
        {
            for (var i = 0; i < 64; i++)
            {
                snapshots.Add(buffer.CaptureMessages());
            }
        });

        await Task.WhenAll(publisher, capturer);

        var snapshot = buffer.CaptureMessages();
        eventBus.Publish(CreateRaceEvent("后续事件"));

        Assert.All(snapshots, captured => Assert.InRange(captured.Length, 0, 8));
        Assert.InRange(snapshot.Length, 0, 8);
        Assert.DoesNotContain("后续事件", snapshot);
    }

    /// <summary>
    /// Verifies disposing the buffer stops future race-event handling.
    /// </summary>
    [Fact]
    public void Dispose_StopsFutureHandling()
    {
        var eventBus = new InMemoryEventBus<RaceEvent>();
        var buffer = new RaceEventInsightBuffer(eventBus);

        eventBus.Publish(CreateRaceEvent("黄旗"));
        buffer.Dispose();
        buffer.Dispose();
        eventBus.Publish(CreateRaceEvent("红旗"));

        Assert.Equal(new[] { "黄旗" }, buffer.CaptureMessages());
    }

    private static RaceEvent CreateRaceEvent(string message, EventType eventType = EventType.LowFuel)
    {
        return new RaceEvent
        {
            EventType = eventType,
            Timestamp = new DateTimeOffset(2026, 5, 10, 10, 0, 0, TimeSpan.Zero),
            Message = message,
            Severity = EventSeverity.Information,
            DedupKey = message
        };
    }
}
