using F1Telemetry.Analytics.Events;
using F1Telemetry.Core.Models;

namespace F1Telemetry.Analytics.Interfaces;

/// <summary>
/// Detects reusable race events from the aggregate real-time session state.
/// </summary>
public interface IEventDetectionService
{
    /// <summary>
    /// Observes the latest aggregate session state.
    /// </summary>
    /// <param name="sessionState">The latest session state snapshot.</param>
    void Observe(SessionState sessionState);

    /// <summary>
    /// Returns newly detected events since the last drain call.
    /// </summary>
    IReadOnlyList<RaceEvent> DrainPendingEvents();
}
