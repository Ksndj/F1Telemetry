using System.Windows.Controls;
using F1Telemetry.App.ViewModels;

namespace F1Telemetry.App.Views;

/// <summary>
/// Hosts the historical post-race review page.
/// </summary>
public partial class PostRaceReviewView : UserControl
{
    /// <summary>
    /// Initializes the post-race review page.
    /// </summary>
    public PostRaceReviewView()
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

        viewModel.PostRaceReview.HistoryBrowser.HistorySessionPages.SetPageSizeFromViewport(
            PostRaceHistorySessionListBox.ActualHeight,
            estimatedItemHeight: 118,
            minPageSize: 2,
            maxPageSize: 8);
        viewModel.PostRaceReview.EventTimelinePages.SetPageSizeFromViewport(
            PostRaceEventTimelineHost.ActualHeight - 70,
            estimatedItemHeight: 64,
            minPageSize: 3,
            maxPageSize: 12);
        viewModel.PostRaceReview.AiReportPages.SetPageSizeFromViewport(
            PostRaceAiReportHost.ActualHeight - 76,
            estimatedItemHeight: 92,
            minPageSize: 2,
            maxPageSize: 8);
    }
}
