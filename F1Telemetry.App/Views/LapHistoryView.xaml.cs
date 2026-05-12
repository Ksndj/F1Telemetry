using System.Windows.Controls;
using F1Telemetry.App.ViewModels;

namespace F1Telemetry.App.Views;

/// <summary>
/// Hosts detailed recent-lap history.
/// </summary>
public partial class LapHistoryView : UserControl
{
    /// <summary>
    /// Initializes the lap history page.
    /// </summary>
    public LapHistoryView()
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

        viewModel.HistoryBrowser.HistorySessionPages.SetPageSizeFromViewport(
            HistorySessionListBox.ActualHeight,
            estimatedItemHeight: 150,
            minPageSize: 2,
            maxPageSize: 8);
        viewModel.HistoryBrowser.HistoryLapPages.SetPageSizeFromViewport(
            HistoryLapListHost.ActualHeight - 78,
            estimatedItemHeight: 58,
            minPageSize: 4,
            maxPageSize: 20);
    }
}
