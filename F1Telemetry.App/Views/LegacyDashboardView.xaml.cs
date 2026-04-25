using System.Windows.Controls;

namespace F1Telemetry.App.Views;

/// <summary>
/// Hosts the pre-M2 full dashboard while the shell pages are split across milestones.
/// </summary>
public partial class LegacyDashboardView : UserControl
{
    /// <summary>
    /// Initializes the legacy dashboard view.
    /// </summary>
    public LegacyDashboardView()
    {
        InitializeComponent();
    }
}
