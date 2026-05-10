namespace F1Telemetry.Core.Eventing;

/// <summary>
/// Provides a thread-safe in-memory event bus for lightweight synchronous fan-out.
/// </summary>
/// <typeparam name="TEvent">The event type carried by the bus.</typeparam>
public sealed class InMemoryEventBus<TEvent> : IEventBus<TEvent>
{
    private readonly object _gate = new();
    private readonly List<Action<TEvent>> _subscribers = new();

    /// <inheritdoc />
    public IDisposable Subscribe(Action<TEvent> subscriber)
    {
        ArgumentNullException.ThrowIfNull(subscriber);

        lock (_gate)
        {
            _subscribers.Add(subscriber);
        }

        return new Subscription(this, subscriber);
    }

    /// <inheritdoc />
    public void Publish(TEvent eventData)
    {
        Action<TEvent>[] snapshot;
        lock (_gate)
        {
            snapshot = _subscribers.ToArray();
        }

        List<Exception>? exceptions = null;
        foreach (var subscriber in snapshot)
        {
            try
            {
                subscriber(eventData);
            }
            catch (Exception ex)
            {
                exceptions ??= new List<Exception>();
                exceptions.Add(ex);
            }
        }

        if (exceptions is not null)
        {
            throw new AggregateException(exceptions);
        }
    }

    private void Unsubscribe(Action<TEvent> subscriber)
    {
        lock (_gate)
        {
            _subscribers.Remove(subscriber);
        }
    }

    private sealed class Subscription : IDisposable
    {
        private readonly InMemoryEventBus<TEvent> _eventBus;
        private Action<TEvent>? _subscriber;

        public Subscription(InMemoryEventBus<TEvent> eventBus, Action<TEvent> subscriber)
        {
            _eventBus = eventBus;
            _subscriber = subscriber;
        }

        public void Dispose()
        {
            var subscriber = Interlocked.Exchange(ref _subscriber, null);
            if (subscriber is not null)
            {
                _eventBus.Unsubscribe(subscriber);
            }
        }
    }
}
