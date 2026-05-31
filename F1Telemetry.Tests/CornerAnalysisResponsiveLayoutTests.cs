using System.IO;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies the responsive XAML contract for the corner analysis page.
/// </summary>
public sealed class CornerAnalysisResponsiveLayoutTests
{
    /// <summary>
    /// Verifies that the page owns vertical scrolling without allowing whole-page horizontal overflow.
    /// </summary>
    [Fact]
    public void CornerAnalysisView_DefinesPageLevelVerticalScroll()
    {
        var xaml = ReadCornerAnalysisViewXaml();

        Assert.Contains("x:Name=\"CornerAnalysisPageScrollViewer\"", xaml, StringComparison.Ordinal);
        Assert.Contains("HorizontalScrollBarVisibility=\"Disabled\"", xaml, StringComparison.Ordinal);
        Assert.Contains("VerticalScrollBarVisibility=\"Auto\"", xaml, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that the main layout has named breakpoints for wide, compressed, and narrow sizing.
    /// </summary>
    [Fact]
    public void CornerAnalysisView_DefinesNarrowSingleColumnBreakpoint()
    {
        var xaml = ReadCornerAnalysisViewXaml();
        var codeBehind = ReadCornerAnalysisViewCodeBehind();

        Assert.Contains("SizeChanged=\"CornerAnalysisView_SizeChanged\"", xaml, StringComparison.Ordinal);
        Assert.Contains("CornerAnalysisListColumn", xaml, StringComparison.Ordinal);
        Assert.Contains("CornerAnalysisDetailsColumn", xaml, StringComparison.Ordinal);
        Assert.Contains("CornerAnalysisMainGapRow", xaml, StringComparison.Ordinal);
        Assert.Contains("WideLayoutBreakpoint = 1300d", codeBehind, StringComparison.Ordinal);
        Assert.Contains("NarrowLayoutBreakpoint = 1000d", codeBehind, StringComparison.Ordinal);
        Assert.Contains("CornerAnalysisListColumn.Width = new GridLength(isWide ? 5d : 1d", codeBehind, StringComparison.Ordinal);
        Assert.Contains("CornerAnalysisDetailsColumn.Width = new GridLength(isWide ? 4d : 1d", codeBehind, StringComparison.Ordinal);
        Assert.Contains("Grid.SetRow(CornerAnalysisRightDetails, 2)", codeBehind, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that summary cards and filter controls can wrap instead of forcing fixed columns.
    /// </summary>
    [Fact]
    public void CornerAnalysisView_SummaryAndFilterBarsCanWrap()
    {
        var xaml = ReadCornerAnalysisViewXaml();

        Assert.Contains("x:Name=\"CornerAnalysisSummaryWrapPanel\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"CornerAnalysisFilterWrapPanel\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<ColumnDefinition Width=\"150\" />", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<ColumnDefinition Width=\"176\" />", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<Button Width=\"104\"", xaml, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that the corner list is constrained by the main grid and owns horizontal table overflow.
    /// </summary>
    [Fact]
    public void CornerAnalysisView_CornerTableUsesLocalHorizontalScroll()
    {
        var xaml = ReadCornerAnalysisViewXaml();

        Assert.Contains("x:Name=\"CornerAnalysisTableScrollViewer\"", xaml, StringComparison.Ordinal);
        Assert.Contains("HorizontalScrollBarVisibility=\"Auto\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"CornerAnalysisListPanel\"", xaml, StringComparison.Ordinal);
        Assert.Contains("MaxHeight=\"520\"", xaml, StringComparison.Ordinal);
        Assert.Contains("VerticalAlignment=\"Top\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("MinWidth=\"640\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxWidth=\"920\"", xaml, StringComparison.Ordinal);
        Assert.Contains("StringFormat=共 {0} 个弯角", xaml, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that the right detail region can stack and avoids fixed oversized widths.
    /// </summary>
    [Fact]
    public void CornerAnalysisView_RightDetailsCanStackWithoutFixedWidth()
    {
        var xaml = ReadCornerAnalysisViewXaml();
        var codeBehind = ReadCornerAnalysisViewCodeBehind();

        Assert.Contains("x:Name=\"CornerAnalysisRightDetails\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"CornerAnalysisTrackMapPanel\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"CornerAnalysisVisualEvidencePanel\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"CornerAnalysisEngineerAdvicePanel\"", xaml, StringComparison.Ordinal);
        Assert.Contains("PlaceRightPanel(CornerAnalysisTrackMapPanel, row: 2", codeBehind, StringComparison.Ordinal);
        Assert.Contains("PlaceRightPanel(CornerAnalysisVisualEvidencePanel, row: 4", codeBehind, StringComparison.Ordinal);
        Assert.DoesNotContain("CornerAnalysisRightScrollViewer", xaml, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that footer notes are normal scrollable page content and read-only picker text stays one-way.
    /// </summary>
    [Fact]
    public void CornerAnalysisView_FooterNotesAndReadOnlyBindingsRemainSafe()
    {
        var xaml = ReadCornerAnalysisViewXaml();

        Assert.Contains("AI 分析备注", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxHeight=\"132\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding CornerAnalysis.ReferencePickerText, Mode=OneWay}\"", xaml, StringComparison.Ordinal);
    }

    private static string ReadCornerAnalysisViewXaml()
    {
        return File.ReadAllText(Path.Combine(FindRepositoryRoot(), "F1Telemetry.App", "Views", "CornerAnalysisView.xaml"));
    }

    private static string ReadCornerAnalysisViewCodeBehind()
    {
        return File.ReadAllText(Path.Combine(FindRepositoryRoot(), "F1Telemetry.App", "Views", "CornerAnalysisView.xaml.cs"));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "F1Telemetry.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
