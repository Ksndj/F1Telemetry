using System.Windows;
using System.Windows.Controls;

namespace F1Telemetry.App.Views;

/// <summary>
/// Interaction logic for the V3 corner analysis page.
/// </summary>
public partial class CornerAnalysisView : UserControl
{
    private const double WideLayoutBreakpoint = 1300d;
    private const double NarrowLayoutBreakpoint = 1000d;

    /// <summary>
    /// Initializes a new corner analysis view.
    /// </summary>
    public CornerAnalysisView()
    {
        InitializeComponent();
    }

    private void CornerAnalysisView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyResponsiveLayout(e.NewSize.Width);
    }

    private void ApplyResponsiveLayout(double width)
    {
        var isNarrow = width < NarrowLayoutBreakpoint;
        var shouldStackRightDetails = width < WideLayoutBreakpoint;

        ApplyMainLayout(isNarrow, width >= WideLayoutBreakpoint);
        ApplyRightDetailsLayout(shouldStackRightDetails);
    }

    private void ApplyMainLayout(bool isNarrow, bool isWide)
    {
        if (isNarrow)
        {
            CornerAnalysisListColumn.Width = new GridLength(1d, GridUnitType.Star);
            CornerAnalysisMainGapColumn.Width = new GridLength(0d);
            CornerAnalysisDetailsColumn.Width = new GridLength(0d);
            CornerAnalysisMainGapRow.Height = new GridLength(12d);

            Grid.SetRow(CornerAnalysisListPanel, 0);
            Grid.SetColumn(CornerAnalysisListPanel, 0);
            Grid.SetColumnSpan(CornerAnalysisListPanel, 3);
            Grid.SetRow(CornerAnalysisRightDetails, 2);
            Grid.SetColumn(CornerAnalysisRightDetails, 0);
            Grid.SetColumnSpan(CornerAnalysisRightDetails, 3);
            return;
        }

        CornerAnalysisListColumn.Width = new GridLength(isWide ? 13d : 3d, GridUnitType.Star);
        CornerAnalysisMainGapColumn.Width = new GridLength(isWide ? 14d : 12d);
        CornerAnalysisDetailsColumn.Width = new GridLength(isWide ? 8d : 2d, GridUnitType.Star);
        CornerAnalysisMainGapRow.Height = new GridLength(0d);

        Grid.SetRow(CornerAnalysisListPanel, 0);
        Grid.SetColumn(CornerAnalysisListPanel, 0);
        Grid.SetColumnSpan(CornerAnalysisListPanel, 1);
        Grid.SetRow(CornerAnalysisRightDetails, 0);
        Grid.SetColumn(CornerAnalysisRightDetails, 2);
        Grid.SetColumnSpan(CornerAnalysisRightDetails, 1);
    }

    private void ApplyRightDetailsLayout(bool shouldStack)
    {
        if (shouldStack)
        {
            CornerAnalysisDetailColumn.Width = new GridLength(1d, GridUnitType.Star);
            CornerAnalysisRightGapColumn.Width = new GridLength(0d);
            CornerAnalysisTrackColumn.Width = new GridLength(0d);

            PlaceRightPanel(CornerAnalysisDetailPanel, row: 0, column: 0, columnSpan: 3);
            PlaceRightPanel(CornerAnalysisTrackMapPanel, row: 2, column: 0, columnSpan: 3);
            PlaceRightPanel(CornerAnalysisVisualEvidencePanel, row: 4, column: 0, columnSpan: 3);
            PlaceRightPanel(CornerAnalysisEngineerAdvicePanel, row: 6, column: 0, columnSpan: 3);
            return;
        }

        CornerAnalysisDetailColumn.Width = new GridLength(1d, GridUnitType.Star);
        CornerAnalysisRightGapColumn.Width = new GridLength(10d);
        CornerAnalysisTrackColumn.Width = new GridLength(1d, GridUnitType.Star);

        PlaceRightPanel(CornerAnalysisDetailPanel, row: 0, column: 0, columnSpan: 1);
        PlaceRightPanel(CornerAnalysisTrackMapPanel, row: 0, column: 2, columnSpan: 1);
        PlaceRightPanel(CornerAnalysisVisualEvidencePanel, row: 2, column: 0, columnSpan: 3);
        PlaceRightPanel(CornerAnalysisEngineerAdvicePanel, row: 4, column: 0, columnSpan: 3);
    }

    private static void PlaceRightPanel(UIElement panel, int row, int column, int columnSpan)
    {
        Grid.SetRow(panel, row);
        Grid.SetColumn(panel, column);
        Grid.SetColumnSpan(panel, columnSpan);
    }
}
