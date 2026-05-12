namespace F1Telemetry.App.Services;

/// <summary>
/// Confirms whether a stored history session should be permanently deleted.
/// </summary>
public interface IHistorySessionDeletionConfirmationService
{
    /// <summary>
    /// Confirms a permanent history session deletion.
    /// </summary>
    /// <param name="request">The deletion confirmation request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<bool> ConfirmDeleteAsync(
        HistorySessionDeletionConfirmationRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Describes the stored history session being deleted.
/// </summary>
/// <param name="SessionSummary">The display summary.</param>
/// <param name="SessionUid">The game session UID.</param>
public sealed record HistorySessionDeletionConfirmationRequest(
    string SessionSummary,
    string SessionUid);
