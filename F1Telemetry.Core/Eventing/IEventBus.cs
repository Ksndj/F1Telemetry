namespace F1Telemetry.Core.Eventing;

/// <summary>
/// Publishes events to synchronous subscribers.
/// </summary>
/// <typeparam name="TEvent">The event type carried by the bus.</typeparam>
public interface IEventBus<TEvent>
{
    /// <summary>
    /// Registers a subscriber and returns a subscription handle.
    /// </summary>
    /// <param name="subscriber">The subscriber callback to invoke for published events.</param>
    /// <returns>A handle that removes the subscriber when disposed.</returns>
    IDisposable Subscribe(Action<TEvent> subscriber);

    /// <summary>
    /// Publishes an event to all current subscribers.
    /// </summary>
    /// <param name="eventData">The event payload.</param>
    void Publish(TEvent eventData);
}
