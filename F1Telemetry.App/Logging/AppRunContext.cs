namespace F1Telemetry.App.Logging;

/// <summary>
/// Carries one application-run correlation identifier for runtime logs.
/// </summary>
public sealed class AppRunContext
{
    /// <summary>
    /// Initializes a new application-run context.
    /// </summary>
    public AppRunContext()
        : this(Guid.NewGuid().ToString("N"), DateTimeOffset.Now)
    {
    }

    /// <summary>
    /// Initializes a new application-run context with explicit values for tests.
    /// </summary>
    public AppRunContext(string runId, DateTimeOffset runStartedAt)
    {
        RunId = string.IsNullOrWhiteSpace(runId) ? Guid.NewGuid().ToString("N") : runId.Trim();
        RunStartedAt = runStartedAt;
    }

    /// <summary>
    /// Gets the correlation id shared by logs from one app run.
    /// </summary>
    public string RunId { get; }

    /// <summary>
    /// Gets the local timestamp when this run started.
    /// </summary>
    public DateTimeOffset RunStartedAt { get; }

    /// <summary>
    /// Gets elapsed milliseconds since the run started.
    /// </summary>
    public long GetElapsedMilliseconds(DateTimeOffset timestamp)
    {
        return Math.Max(0, (long)(timestamp - RunStartedAt).TotalMilliseconds);
    }
}
