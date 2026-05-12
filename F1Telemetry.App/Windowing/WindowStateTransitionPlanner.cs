using System.Windows;

namespace F1Telemetry.App.Windowing;

/// <summary>
/// Describes the visual transition settings used when the main window changes state.
/// </summary>
public sealed record WindowStateTransitionProfile(
    string FeedbackMessage,
    Thickness ContentMargin,
    double InitialScale,
    double InitialOpacity,
    bool ShowFeedback);

/// <summary>
/// Provides lightweight transition presets for main-window maximize, restore, and minimize changes.
/// </summary>
public static class WindowStateTransitionPlanner
{
    /// <summary>
    /// Creates the transition profile for the specified window state.
    /// </summary>
    /// <param name="windowState">The current WPF window state.</param>
    /// <returns>The visual profile used by the main window transition handler.</returns>
    public static WindowStateTransitionProfile Create(WindowState windowState)
    {
        return windowState switch
        {
            WindowState.Maximized => new WindowStateTransitionProfile(
                FeedbackMessage: "窗口已最大化",
                ContentMargin: new Thickness(0),
                InitialScale: 0.988,
                InitialOpacity: 0.96,
                ShowFeedback: true),
            WindowState.Minimized => new WindowStateTransitionProfile(
                FeedbackMessage: "窗口已最小化",
                ContentMargin: new Thickness(0),
                InitialScale: 1.0,
                InitialOpacity: 1.0,
                ShowFeedback: false),
            _ => new WindowStateTransitionProfile(
                FeedbackMessage: "窗口已还原",
                ContentMargin: new Thickness(0),
                InitialScale: 0.994,
                InitialOpacity: 0.98,
                ShowFeedback: true)
        };
    }
}
