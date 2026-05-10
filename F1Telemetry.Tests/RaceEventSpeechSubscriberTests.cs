using System.Collections.Concurrent;
using F1Telemetry.AI.Services;
using F1Telemetry.Analytics.Events;
using F1Telemetry.App.Services;
using F1Telemetry.Core.Eventing;
using F1Telemetry.Core.Interfaces;
using F1Telemetry.Core.Models;
using F1Telemetry.TTS.Models;
using F1Telemetry.TTS.Services;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies App-layer speech subscription from race events to the TTS queue.
/// </summary>
public sealed class RaceEventSpeechSubscriberTests
{
    /// <summary>
    /// Verifies a speakable race event published on the event bus reaches the TTS speaker.
    /// </summary>
    [Fact]
    public async Task Publish_SpeakableRaceEvent_ReachesTtsSpeaker()
    {
        var eventBus = new InMemoryEventBus<RaceEvent>();
        var speaker = new RecordingSpeaker();
        using var queue = new TtsQueue(speaker, new TtsOptions { TtsEnabled = true });
        using var subscriber = CreateSubscriber(eventBus, queue, SessionMode.Race);

        eventBus.Publish(CreateRaceEvent(EventType.FrontCarPitted, "Front car boxed."));

        await WaitUntilAsync(() => speaker.Messages.Contains("Front car boxed."));
        Assert.Contains("Front car boxed.", speaker.Messages.ToArray());
    }

    /// <summary>
    /// Verifies a new race-advice event published in race mode reaches the TTS speaker.
    /// </summary>
    [Fact]
    public async Task Publish_M6RaceAdviceEventInRace_ReachesTtsSpeaker()
    {
        var eventBus = new InMemoryEventBus<RaceEvent>();
        var speaker = new RecordingSpeaker();
        using var queue = new TtsQueue(speaker, new TtsOptions { TtsEnabled = true });
        using var subscriber = CreateSubscriber(eventBus, queue, SessionMode.Race);

        eventBus.Publish(CreateRaceEvent(EventType.FrontOldTyreRisk, "前车旧胎，保持压力。"));

        await WaitUntilAsync(() => speaker.Messages.Contains("前车旧胎，保持压力。"));
        Assert.Contains("前车旧胎，保持压力。", speaker.Messages.ToArray());
    }

    /// <summary>
    /// Verifies qualifying sessions keep race pit-window speech out of the TTS queue.
    /// </summary>
    [Theory]
    [InlineData(EventType.FrontCarPitted)]
    [InlineData(EventType.RearCarPitted)]
    [InlineData(EventType.AttackWindow)]
    [InlineData(EventType.DefenseWindow)]
    public void Publish_QualifyingMode_FiltersRacePitWindowEvents(EventType eventType)
    {
        var eventBus = new InMemoryEventBus<RaceEvent>();
        var warnings = new List<string>();
        var queue = CreateDisposedQueue();
        using var subscriber = CreateSubscriber(eventBus, queue, SessionMode.Qualifying, warnings.Add);

        eventBus.Publish(CreateRaceEvent(eventType, "Race strategy event."));

        Assert.Empty(warnings);
    }

    /// <summary>
    /// Verifies qualifying mode filters new race-only advice before it reaches the TTS queue.
    /// </summary>
    [Fact]
    public void Publish_QualifyingMode_FiltersM6RaceOnlyAdvice()
    {
        var eventBus = new InMemoryEventBus<RaceEvent>();
        var warnings = new List<string>();
        var queue = CreateDisposedQueue();
        using var subscriber = CreateSubscriber(eventBus, queue, SessionMode.Qualifying, warnings.Add);

        eventBus.Publish(CreateRaceEvent(EventType.RacePitWindow, "正赛进站窗口。"));

        Assert.Empty(warnings);
    }

    /// <summary>
    /// Verifies data-quality events are not converted into TTS messages.
    /// </summary>
    [Fact]
    public void Publish_DataQualityWarning_DoesNotReachTts()
    {
        var eventBus = new InMemoryEventBus<RaceEvent>();
        var warnings = new List<string>();
        var queue = CreateDisposedQueue();
        using var subscriber = CreateSubscriber(eventBus, queue, SessionMode.Race, warnings.Add);

        eventBus.Publish(CreateRaceEvent(EventType.DataQualityWarning, "Gap data was unavailable."));

        Assert.Empty(warnings);
    }

    /// <summary>
    /// Verifies TTS enqueue failures are isolated from the event bus and later subscribers still run.
    /// </summary>
    [Fact]
    public void Publish_WhenTtsEnqueueThrows_DoesNotThrowAndNotifiesLaterSubscriber()
    {
        var eventBus = new InMemoryEventBus<RaceEvent>();
        var warnings = new List<string>();
        var laterSubscriberEvents = new List<RaceEvent>();
        var queue = CreateDisposedQueue();
        using var subscriber = CreateSubscriber(eventBus, queue, SessionMode.Race, warnings.Add);
        eventBus.Subscribe(laterSubscriberEvents.Add);
        var raceEvent = CreateRaceEvent(EventType.FrontCarPitted, "Front car boxed.");

        var exception = Record.Exception(() => eventBus.Publish(raceEvent));

        Assert.Null(exception);
        Assert.Same(raceEvent, Assert.Single(laterSubscriberEvents));
        Assert.Contains(warnings, warning =>
            warning.Contains("TTS", StringComparison.Ordinal) &&
            warning.Contains("EventBus", StringComparison.Ordinal));
    }

    /// <summary>
    /// Verifies event mapping failures are isolated from the event bus and later subscribers still run.
    /// </summary>
    [Fact]
    public void Publish_WhenEventMappingThrows_DoesNotThrowAndNotifiesLaterSubscriber()
    {
        var eventBus = new InMemoryEventBus<RaceEvent>();
        var warnings = new List<string>();
        var laterSubscriberEvents = new List<RaceEvent>();
        using var queue = new TtsQueue(new RecordingSpeaker(), new TtsOptions { TtsEnabled = true });
        using var subscriber = new RaceEventSpeechSubscriber(
            eventBus,
            new TtsMessageFactory(),
            queue,
            () => SessionMode.Race,
            () => throw new InvalidOperationException("options capture failed"),
            warnings.Add);
        eventBus.Subscribe(laterSubscriberEvents.Add);
        var raceEvent = CreateRaceEvent(EventType.LowFuel, "低油警告。");

        var exception = Record.Exception(() => eventBus.Publish(raceEvent));

        Assert.Null(exception);
        Assert.Same(raceEvent, Assert.Single(laterSubscriberEvents));
        Assert.Contains(warnings, warning =>
            warning.Contains("TTS", StringComparison.Ordinal) &&
            warning.Contains("EventBus", StringComparison.Ordinal));
    }

    /// <summary>
    /// Verifies disposing the subscriber removes its event-bus handler.
    /// </summary>
    [Fact]
    public void Dispose_StopsFutureHandling()
    {
        var eventBus = new InMemoryEventBus<RaceEvent>();
        var warnings = new List<string>();
        var queue = CreateDisposedQueue();
        var subscriber = CreateSubscriber(eventBus, queue, SessionMode.Race, warnings.Add);

        subscriber.Dispose();
        subscriber.Dispose();
        eventBus.Publish(CreateRaceEvent(EventType.FrontCarPitted, "Front car boxed."));

        Assert.Empty(warnings);
    }

    private static RaceEventSpeechSubscriber CreateSubscriber(
        IEventBus<RaceEvent> eventBus,
        TtsQueue queue,
        SessionMode sessionMode,
        Action<string>? logWarning = null)
    {
        return new RaceEventSpeechSubscriber(
            eventBus,
            new TtsMessageFactory(),
            queue,
            () => sessionMode,
            () => new TtsOptions { TtsEnabled = true },
            logWarning);
    }

    private static TtsQueue CreateDisposedQueue()
    {
        var queue = new TtsQueue(new RecordingSpeaker(), new TtsOptions { TtsEnabled = true });
        queue.Dispose();
        return queue;
    }

    private static RaceEvent CreateRaceEvent(EventType eventType, string message)
    {
        return new RaceEvent
        {
            EventType = eventType,
            Timestamp = new DateTimeOffset(2026, 5, 10, 10, 0, 0, TimeSpan.Zero),
            LapNumber = 8,
            VehicleIdx = 12,
            Severity = eventType == EventType.DataQualityWarning
                ? EventSeverity.Information
                : EventSeverity.Warning,
            Message = message,
            DedupKey = $"test:{eventType}"
        };
    }

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        var timeoutAt = DateTime.UtcNow.AddSeconds(5);
        while (!predicate())
        {
            if (DateTime.UtcNow >= timeoutAt)
            {
                throw new TimeoutException("The expected subscriber state was not reached in time.");
            }

            await Task.Delay(25);
        }
    }

    private sealed class RecordingSpeaker : ITtsService
    {
        public ConcurrentQueue<string> Messages { get; } = new();

        public void Configure(string? voiceName, int volume, int rate)
        {
        }

        public Task SpeakAsync(string text, CancellationToken cancellationToken = default)
        {
            Messages.Enqueue(text);
            return Task.CompletedTask;
        }
    }
}
