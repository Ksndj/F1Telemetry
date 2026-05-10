using F1Telemetry.AI.Services;
using F1Telemetry.Analytics.Events;
using F1Telemetry.Core.Eventing;
using F1Telemetry.Core.Models;
using F1Telemetry.TTS.Models;
using F1Telemetry.TTS.Services;

namespace F1Telemetry.App.Services;

/// <summary>
/// Subscribes analytics race events to the TTS queue through the race-event speech mapper.
/// </summary>
public sealed class RaceEventSpeechSubscriber : IDisposable
{
    private readonly TtsMessageFactory _ttsMessageFactory;
    private readonly TtsQueue _ttsQueue;
    private readonly Func<SessionMode> _captureSessionMode;
    private readonly Func<TtsOptions> _captureTtsOptions;
    private readonly Action<string>? _logWarning;
    private IDisposable? _subscription;
    private int _disposed;

    /// <summary>
    /// Initializes a subscriber that turns publishable race events into queued TTS messages.
    /// </summary>
    /// <param name="raceEventBus">The race-event bus to subscribe to.</param>
    /// <param name="ttsMessageFactory">The mapper that applies session and TTS speech rules.</param>
    /// <param name="ttsQueue">The queue that receives accepted speech messages.</param>
    /// <param name="captureSessionMode">Captures the current high-level session mode.</param>
    /// <param name="captureTtsOptions">Captures the current TTS options snapshot.</param>
    /// <param name="logWarning">Optional warning logger used when event speech handling fails.</param>
    public RaceEventSpeechSubscriber(
        IEventBus<RaceEvent> raceEventBus,
        TtsMessageFactory ttsMessageFactory,
        TtsQueue ttsQueue,
        Func<SessionMode> captureSessionMode,
        Func<TtsOptions> captureTtsOptions,
        Action<string>? logWarning = null)
    {
        ArgumentNullException.ThrowIfNull(raceEventBus);

        _ttsMessageFactory = ttsMessageFactory ?? throw new ArgumentNullException(nameof(ttsMessageFactory));
        _ttsQueue = ttsQueue ?? throw new ArgumentNullException(nameof(ttsQueue));
        _captureSessionMode = captureSessionMode ?? throw new ArgumentNullException(nameof(captureSessionMode));
        _captureTtsOptions = captureTtsOptions ?? throw new ArgumentNullException(nameof(captureTtsOptions));
        _logWarning = logWarning;
        _subscription = raceEventBus.Subscribe(HandleRaceEvent);
    }

    /// <summary>
    /// Disposes the event-bus subscription.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        Interlocked.Exchange(ref _subscription, null)?.Dispose();
    }

    private void HandleRaceEvent(RaceEvent raceEvent)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        try
        {
            var message = _ttsMessageFactory.CreateForRaceEvent(
                raceEvent,
                _captureTtsOptions(),
                _captureSessionMode());

            if (message is not null)
            {
                _ttsQueue.TryEnqueue(message);
            }
        }
        catch (Exception ex)
        {
            LogWarning($"TTS EventBus subscriber failed: {ex.GetType().Name}");
        }
    }

    private void LogWarning(string message)
    {
        try
        {
            _logWarning?.Invoke(message);
        }
        catch
        {
        }
    }
}
