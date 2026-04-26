namespace F1Telemetry.App.Windowing;

/// <summary>
/// Coordinates application resource shutdown before the main window is allowed to close.
/// </summary>
public interface IApplicationShutdownCoordinator
{
    /// <summary>
    /// Releases application resources before the WPF shell exits.
    /// </summary>
    Task ShutdownAsync();
}
