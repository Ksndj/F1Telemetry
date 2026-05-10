using F1Telemetry.Analytics.Events;
using F1Telemetry.Core.Eventing;

namespace F1Telemetry.App.Services;

/// <summary>
/// Caches recent race-event messages for AI insight prompts.
/// </summary>
public sealed class RaceEventInsightBuffer : IDisposable
{
    private readonly object _gate = new();
    private readonly int _capacity;
    private readonly List<string> _messages = new();
    private IDisposable? _subscription;

    /// <summary>
    /// Initializes a new instance of the <see cref="RaceEventInsightBuffer"/> class.
    /// </summary>
    /// <param name="raceEventBus">The race-event bus to observe.</param>
    /// <param name="capacity">The maximum number of recent messages to retain.</param>
    public RaceEventInsightBuffer(IEventBus<RaceEvent> raceEventBus, int capacity = 8)
    {
        ArgumentNullException.ThrowIfNull(raceEventBus);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        _capacity = capacity;
        _subscription = raceEventBus.Subscribe(HandleRaceEvent);
    }

    /// <summary>
    /// Captures the currently cached messages as a stable snapshot.
    /// </summary>
    /// <returns>A copy of the retained race-event messages in oldest-to-newest order.</returns>
    public string[] CaptureMessages()
    {
        lock (_gate)
        {
            return _messages.ToArray();
        }
    }

    /// <summary>
    /// Clears all cached race-event messages.
    /// </summary>
    public void Reset()
    {
        lock (_gate)
        {
            _messages.Clear();
        }
    }

    /// <summary>
    /// Unsubscribes the buffer from the race-event bus.
    /// </summary>
    public void Dispose()
    {
        Interlocked.Exchange(ref _subscription, null)?.Dispose();
    }

    private void HandleRaceEvent(RaceEvent raceEvent)
    {
        if (raceEvent.EventType == EventType.DataQualityWarning)
        {
            return;
        }

        var message = raceEvent.Message?.Trim();
        if (string.IsNullOrWhiteSpace(message) || string.Equals(message, "-", StringComparison.Ordinal))
        {
            return;
        }

        lock (_gate)
        {
            _messages.Add(message);
            if (_messages.Count > _capacity)
            {
                _messages.RemoveRange(0, _messages.Count - _capacity);
            }
        }
    }
}
