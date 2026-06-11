using System.Windows;
using System.Windows.Controls;
using F1Telemetry.App.ViewModels;

namespace F1Telemetry.App.Views.Shell;

/// <summary>
/// Selects the shell content template for the active navigation item.
/// </summary>
public sealed class NavigationTemplateSelector : DataTemplateSelector
{
    /// <summary>
    /// Gets or sets the default overview page template.
    /// </summary>
    public DataTemplate? OverviewTemplate { get; set; }

    /// <summary>
    /// Gets or sets the charts page template.
    /// </summary>
    public DataTemplate? ChartsTemplate { get; set; }

    /// <summary>
    /// Gets or sets the lap history page template.
    /// </summary>
    public DataTemplate? LapHistoryTemplate { get; set; }

    /// <summary>
    /// Gets or sets the post-race review page template.
    /// </summary>
    public DataTemplate? PostRaceReviewTemplate { get; set; }

    /// <summary>
    /// Gets or sets the session comparison page template.
    /// </summary>
    public DataTemplate? SessionComparisonTemplate { get; set; }

    /// <summary>
    /// Gets or sets the corner analysis page template.
    /// </summary>
    public DataTemplate? CornerAnalysisTemplate { get; set; }

    /// <summary>
    /// Gets or sets the opponents page template.
    /// </summary>
    public DataTemplate? OpponentsTemplate { get; set; }

    /// <summary>
    /// Gets or sets the event logs page template.
    /// </summary>
    public DataTemplate? LogsTemplate { get; set; }

    /// <summary>
    /// Gets or sets the AI/TTS settings page template.
    /// </summary>
    public DataTemplate? AiTtsTemplate { get; set; }

    /// <summary>
    /// Gets or sets the settings page template.
    /// </summary>
    public DataTemplate? SettingsTemplate { get; set; }

    /// <summary>
    /// Gets or sets the legacy dashboard page template.
    /// </summary>
    public DataTemplate? LegacyDashboardTemplate { get; set; }

    /// <summary>
    /// Gets or sets the placeholder page template.
    /// </summary>
    public DataTemplate? PlaceholderTemplate { get; set; }

    /// <inheritdoc />
    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        var key = item switch
        {
            DashboardViewModel dashboard => dashboard.SelectedShellNavigationItem?.Key,
            ShellNavigationItemViewModel navigationItem => navigationItem.Key,
            _ => null,
        };

        return key switch
        {
            "overview" => OverviewTemplate,
            "charts" => ChartsTemplate,
            "lap-history" or "laps" => LapHistoryTemplate,
            "post-race-review" => PostRaceReviewTemplate,
            "session-comparison" => SessionComparisonTemplate,
            "corner-analysis" => CornerAnalysisTemplate,
            "opponents" => OpponentsTemplate,
            "event-logs" or "logs" => LogsTemplate,
            "ai-tts" => AiTtsTemplate,
            "settings" => SettingsTemplate,
            "legacy-dashboard" => LegacyDashboardTemplate,
            null => OverviewTemplate,
            _ => PlaceholderTemplate,
        } ?? base.SelectTemplate(item, container);
    }
}
