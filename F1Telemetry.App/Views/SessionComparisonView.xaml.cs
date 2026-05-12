using System.Windows.Controls;
using F1Telemetry.App.ViewModels;

namespace F1Telemetry.App.Views;

/// <summary>
/// Hosts the historical multi-session comparison page.
/// </summary>
public partial class SessionComparisonView : UserControl
{
    /// <summary>
    /// Initializes the session comparison page.
    /// </summary>
    public SessionComparisonView()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateAdaptivePageSizes();
        SizeChanged += (_, _) => UpdateAdaptivePageSizes();
    }

    private void UpdateAdaptivePageSizes()
    {
        if (DataContext is not DashboardViewModel viewModel)
        {
            return;
        }

        viewModel.SessionComparison.CandidateSessionPages.SetPageSizeFromViewport(
            CandidateSessionListHost.ActualHeight,
            estimatedItemHeight: 128,
            minPageSize: 2,
            maxPageSize: 8);
    }
}
