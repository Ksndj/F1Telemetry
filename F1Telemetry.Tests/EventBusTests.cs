using F1Telemetry.Core.Eventing;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies the in-memory EventBus contract used by the V2 event pipeline.
/// </summary>
public sealed class EventBusTests
{
    /// <summary>
    /// Verifies a subscriber receives a published event.
    /// </summary>
    [Fact]
    public void Publish_NotifiesSubscriber()
    {
        var eventBus = new InMemoryEventBus<string>();
        string? received = null;

        eventBus.Subscribe(value => received = value);
        eventBus.Publish("low fuel");

        Assert.Equal("low fuel", received);
    }

    /// <summary>
    /// Verifies a disposed subscription no longer receives events.
    /// </summary>
    [Fact]
    public void Subscribe_DisposedSubscriptionStopsReceivingEvents()
    {
        var eventBus = new InMemoryEventBus<string>();
        var received = new List<string>();
        var subscription = eventBus.Subscribe(received.Add);

        subscription.Dispose();
        eventBus.Publish("ignored");

        Assert.Empty(received);
    }

    /// <summary>
    /// Verifies subscribers are called in subscription order.
    /// </summary>
    [Fact]
    public void Publish_NotifiesSubscribersInSubscriptionOrder()
    {
        var eventBus = new InMemoryEventBus<string>();
        var calls = new List<string>();

        eventBus.Subscribe(_ => calls.Add("first"));
        eventBus.Subscribe(_ => calls.Add("second"));
        eventBus.Publish("event");

        Assert.Equal(new[] { "first", "second" }, calls);
    }

    /// <summary>
    /// Verifies disposing the same subscription multiple times is safe.
    /// </summary>
    [Fact]
    public void Subscription_DisposeIsIdempotent()
    {
        var eventBus = new InMemoryEventBus<string>();
        var subscription = eventBus.Subscribe(_ => { });

        subscription.Dispose();
        var exception = Record.Exception(subscription.Dispose);

        Assert.Null(exception);
    }

    /// <summary>
    /// Verifies subscriber failures do not prevent later subscribers from receiving the same event.
    /// </summary>
    [Fact]
    public void Publish_WhenSubscriberThrows_NotifiesLaterSubscribersAndAggregatesExceptions()
    {
        var eventBus = new InMemoryEventBus<string>();
        var received = new List<string>();
        var failure = new InvalidOperationException("subscriber failed");

        eventBus.Subscribe(_ => throw failure);
        eventBus.Subscribe(received.Add);

        var exception = Assert.Throws<AggregateException>(() => eventBus.Publish("yellow flag"));

        Assert.Same(failure, Assert.Single(exception.InnerExceptions));
        Assert.Equal(new[] { "yellow flag" }, received);
    }
}
