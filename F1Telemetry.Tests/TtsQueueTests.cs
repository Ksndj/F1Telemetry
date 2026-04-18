using System.Collections.Concurrent;
using F1Telemetry.Core.Interfaces;
using F1Telemetry.TTS;
using F1Telemetry.TTS.Models;
using F1Telemetry.TTS.Services;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies queue-level deduplication, cooldown, priority ordering, and overflow behavior for TTS.
/// </summary>
public sealed class TtsQueueTests
{
    /// <summary>
    /// Verifies that the queue rejects a duplicate dedup key while the first message is still queued or speaking.
    /// </summary>
    [Fact]
    public async Task TryEnqueue_DuplicateDedupKeyWhileQueued_RejectsSecondMessageAndLogsDeduplication()
    {
        var speaker = new BlockingSpeaker();
        using var queue = new TtsQueue(speaker, new TtsOptions { TtsEnabled = true });

        var firstAccepted = queue.TryEnqueue(new TtsMessage
        {
            Text = "Low fuel warning",
            Source = "TTS",
            Type = "low_fuel",
            DedupKey = "event:low_fuel:lap12",
            Priority = TtsPriority.Normal,
            Cooldown = TimeSpan.FromSeconds(8)
        });
        await speaker.WaitForInvocationAsync();

        var secondAccepted = queue.TryEnqueue(new TtsMessage
        {
            Text = "Low fuel warning again",
            Source = "TTS",
            Type = "low_fuel",
            DedupKey = "event:low_fuel:lap12",
            Priority = TtsPriority.High,
            Cooldown = TimeSpan.FromSeconds(8)
        });

        var rejectionRecords = await DrainUntilAsync(
            queue,
            records => records.Any(record => record.Outcome == TtsPlaybackOutcome.Deduplicated));

        Assert.True(firstAccepted);
        Assert.False(secondAccepted);
        Assert.Contains(rejectionRecords, record => record.Outcome == TtsPlaybackOutcome.PlaybackStarted);
        Assert.Contains(
            rejectionRecords,
            record => record.Outcome == TtsPlaybackOutcome.Deduplicated &&
                      record.DedupKey == "event:low_fuel:lap12");

        speaker.Release();
        await DrainUntilAsync(queue, records => records.Any(record => record.Outcome == TtsPlaybackOutcome.PlaybackCompleted));
    }

    /// <summary>
    /// Verifies that a message inside the cooldown window is rejected only after a successful playback.
    /// </summary>
    [Fact]
    public async Task TryEnqueue_MessageInsideCooldownAfterSuccessfulPlayback_IsRejectedAndLogsCooldown()
    {
        var speaker = new RecordingSpeaker();
        using var queue = new TtsQueue(speaker, new TtsOptions { TtsEnabled = true, CooldownSeconds = 8 });

        Assert.True(queue.TryEnqueue(new TtsMessage
        {
            Text = "Tyres overheating",
            Source = "TTS",
            Type = "high_tyre_wear",
            DedupKey = "event:high_tyre_wear:car0:lap9",
            Priority = TtsPriority.Normal,
            Cooldown = TimeSpan.FromMinutes(1)
        }));

        await DrainUntilAsync(queue, records => records.Any(record => record.Outcome == TtsPlaybackOutcome.PlaybackCompleted));

        var accepted = queue.TryEnqueue(new TtsMessage
        {
            Text = "Tyres overheating repeat",
            Source = "TTS",
            Type = "high_tyre_wear",
            DedupKey = "event:high_tyre_wear:car0:lap9",
            Priority = TtsPriority.Normal,
            Cooldown = TimeSpan.FromMinutes(1)
        });

        var cooldownRecords = await DrainUntilAsync(
            queue,
            records => records.Any(record => record.Outcome == TtsPlaybackOutcome.CooledDown));

        Assert.False(accepted);
        Assert.Contains(cooldownRecords, record => record.Outcome == TtsPlaybackOutcome.CooledDown);
    }

    /// <summary>
    /// Verifies that a failed playback does not activate the cooldown window.
    /// </summary>
    [Fact]
    public async Task TryEnqueue_FailedPlayback_DoesNotUpdateCooldown()
    {
        var speaker = new FailOnceSpeaker();
        using var queue = new TtsQueue(speaker, new TtsOptions { TtsEnabled = true, CooldownSeconds = 8 });

        Assert.True(queue.TryEnqueue(new TtsMessage
        {
            Text = "Traffic update",
            Source = "AI",
            Type = "lap",
            DedupKey = "ai:lap:12",
            Priority = TtsPriority.Low,
            Cooldown = TimeSpan.FromMinutes(1)
        }));

        var failureRecords = await DrainUntilAsync(
            queue,
            records => records.Any(record => record.Outcome == TtsPlaybackOutcome.Failed));

        var secondAccepted = queue.TryEnqueue(new TtsMessage
        {
            Text = "Traffic update retry",
            Source = "AI",
            Type = "lap",
            DedupKey = "ai:lap:12",
            Priority = TtsPriority.Low,
            Cooldown = TimeSpan.FromMinutes(1)
        });

        var completionRecords = await DrainUntilAsync(
            queue,
            records => records.Any(record => record.Outcome == TtsPlaybackOutcome.PlaybackCompleted));

        Assert.True(secondAccepted);
        Assert.Contains(failureRecords, record => record.Outcome == TtsPlaybackOutcome.Failed);
        Assert.Contains(completionRecords, record => record.Outcome == TtsPlaybackOutcome.PlaybackCompleted);
    }

    /// <summary>
    /// Verifies that high priority messages jump ahead of queued low priority messages without interrupting the current playback.
    /// </summary>
    [Fact]
    public async Task TryEnqueue_HighPriority_JumpsAheadOfQueuedLowPriorityWithoutInterruptingCurrentPlayback()
    {
        var speaker = new BlockingSpeaker();
        using var queue = new TtsQueue(speaker, new TtsOptions { TtsEnabled = true });

        Assert.True(queue.TryEnqueue(new TtsMessage
        {
            Text = "Current low priority message",
            Source = "TTS",
            Type = "event",
            DedupKey = "event:event:current",
            Priority = TtsPriority.Low
        }));
        await speaker.WaitForInvocationAsync();

        Assert.True(queue.TryEnqueue(new TtsMessage
        {
            Text = "Queued low priority message",
            Source = "TTS",
            Type = "event",
            DedupKey = "event:event:queued-low",
            Priority = TtsPriority.Low
        }));
        Assert.True(queue.TryEnqueue(new TtsMessage
        {
            Text = "High priority message",
            Source = "TTS",
            Type = "event",
            DedupKey = "event:event:queued-high",
            Priority = TtsPriority.High
        }));

        speaker.Release();
        await WaitUntilAsync(() => speaker.Messages.Count >= 3);

        Assert.Equal(
            ["Current low priority message", "High priority message", "Queued low priority message"],
            speaker.Messages.ToArray());
    }

    /// <summary>
    /// Verifies that a full queue drops low-priority messages and lets high-priority messages evict queued low-priority work.
    /// </summary>
    [Fact]
    public async Task TryEnqueue_QueueFull_DropsLowPriorityAndLetsHighPriorityEvictQueuedLowPriority()
    {
        var speaker = new BlockingSpeaker();
        using var queue = new TtsQueue(speaker, new TtsOptions { TtsEnabled = true });

        Assert.True(queue.TryEnqueue(new TtsMessage
        {
            Text = "Current low priority message",
            Source = "TTS",
            Type = "event",
            DedupKey = "event:event:current",
            Priority = TtsPriority.Low
        }));
        await speaker.WaitForInvocationAsync();

        for (var index = 0; index < 50; index++)
        {
            Assert.True(queue.TryEnqueue(new TtsMessage
            {
                Text = $"Queued low priority {index}",
                Source = "TTS",
                Type = "event",
                DedupKey = $"event:event:low{index}",
                Priority = TtsPriority.Low
            }));
        }

        var extraLowAccepted = queue.TryEnqueue(new TtsMessage
        {
            Text = "Extra low priority",
            Source = "TTS",
            Type = "event",
            DedupKey = "event:event:extra-low",
            Priority = TtsPriority.Low
        });

        Assert.False(extraLowAccepted);

        var highAccepted = queue.TryEnqueue(new TtsMessage
        {
            Text = "High priority takeover",
            Source = "TTS",
            Type = "event",
            DedupKey = "event:event:high",
            Priority = TtsPriority.High
        });

        Assert.True(highAccepted);

        var dropRecords = await DrainUntilAsync(
            queue,
            records => records.Count(record => record.Outcome == TtsPlaybackOutcome.Dropped) >= 2);

        speaker.Release();
        await WaitUntilAsync(() => speaker.Messages.Count >= 2);

        var spokenMessages = speaker.Messages.ToArray();
        Assert.Equal("Current low priority message", spokenMessages[0]);
        Assert.Equal("High priority takeover", spokenMessages[1]);
        Assert.Contains(dropRecords, record => record.Message.Contains("已丢弃低优先级播报", StringComparison.Ordinal));
        Assert.Contains(dropRecords, record => record.Message.Contains("已让出低优先级播报", StringComparison.Ordinal));
    }

    private static async Task<IReadOnlyList<TtsPlaybackRecord>> DrainUntilAsync(
        TtsQueue queue,
        Func<IReadOnlyList<TtsPlaybackRecord>, bool> predicate)
    {
        var collected = new List<TtsPlaybackRecord>();
        var timeoutAt = DateTime.UtcNow.AddSeconds(5);

        while (DateTime.UtcNow < timeoutAt)
        {
            collected.AddRange(queue.DrainPendingRecords());
            if (predicate(collected))
            {
                return collected;
            }

            await Task.Delay(25);
        }

        throw new TimeoutException("The expected queue records were not observed in time.");
    }

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        var timeoutAt = DateTime.UtcNow.AddSeconds(5);
        while (!predicate())
        {
            if (DateTime.UtcNow >= timeoutAt)
            {
                throw new TimeoutException("The expected queue state was not reached in time.");
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

    private sealed class FailOnceSpeaker : ITtsService
    {
        private int _callCount;

        public void Configure(string? voiceName, int volume, int rate)
        {
        }

        public Task SpeakAsync(string text, CancellationToken cancellationToken = default)
        {
            var callCount = Interlocked.Increment(ref _callCount);
            if (callCount == 1)
            {
                throw new InvalidOperationException("Simulated TTS failure.");
            }

            return Task.CompletedTask;
        }
    }

    private sealed class BlockingSpeaker : ITtsService
    {
        private readonly ConcurrentQueue<string> _messages = new();
        private readonly TaskCompletionSource<bool> _invoked = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _callCount;

        public IReadOnlyCollection<string> Messages => _messages.ToArray();

        public void Configure(string? voiceName, int volume, int rate)
        {
        }

        public async Task SpeakAsync(string text, CancellationToken cancellationToken = default)
        {
            _messages.Enqueue(text);
            var callIndex = Interlocked.Increment(ref _callCount);

            if (callIndex == 1)
            {
                _invoked.TrySetResult(true);
                await _gate.Task.WaitAsync(cancellationToken);
            }
        }

        public Task WaitForInvocationAsync()
        {
            return _invoked.Task;
        }

        public void Release()
        {
            _gate.TrySetResult(true);
        }
    }
}
