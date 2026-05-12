using F1Telemetry.App.ViewModels;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies bindable pagination behavior for historical list projections.
/// </summary>
public sealed class PagedCollectionViewModelTests
{
    /// <summary>
    /// Verifies that changing page size exposes only the current page items.
    /// </summary>
    [Fact]
    public void SetPageSize_ExposesCurrentPageItems()
    {
        var pager = new PagedCollectionViewModel<int>();

        pager.SetItems(Enumerable.Range(1, 5));
        pager.SetPageSize(2);

        Assert.Equal(new[] { 1, 2 }, pager.Items);
        Assert.Equal("第 1 / 3 页", pager.PageText);
        Assert.True(pager.CanMoveNext);
        Assert.False(pager.CanMovePrevious);
    }

    /// <summary>
    /// Verifies previous and next commands clamp at page boundaries.
    /// </summary>
    [Fact]
    public void PreviousAndNextCommands_DoNotMovePastBoundaries()
    {
        var pager = new PagedCollectionViewModel<int>();
        pager.SetItems(Enumerable.Range(1, 3));
        pager.SetPageSize(2);

        pager.PreviousPageCommand.Execute(null);
        Assert.Equal(new[] { 1, 2 }, pager.Items);

        pager.NextPageCommand.Execute(null);
        pager.NextPageCommand.Execute(null);

        Assert.Equal(new[] { 3 }, pager.Items);
        Assert.Equal(1, pager.PageIndex);
        Assert.False(pager.CanMoveNext);
    }

    /// <summary>
    /// Verifies viewport-driven page size changes keep the current page valid.
    /// </summary>
    [Fact]
    public void SetPageSizeFromViewport_WhenViewportChanges_KeepsPageValid()
    {
        var pager = new PagedCollectionViewModel<int>();
        pager.SetItems(Enumerable.Range(1, 10));
        pager.SetPageSize(3);
        pager.NextPageCommand.Execute(null);
        pager.NextPageCommand.Execute(null);
        pager.NextPageCommand.Execute(null);

        pager.SetPageSizeFromViewport(500, 100, minPageSize: 2, maxPageSize: 5);

        Assert.Equal(1, pager.PageIndex);
        Assert.Equal(5, pager.PageSize);
        Assert.Equal(new[] { 6, 7, 8, 9, 10 }, pager.Items);
    }

    /// <summary>
    /// Verifies replacing data resets to the first page unless asked to preserve a valid page.
    /// </summary>
    [Fact]
    public void SetItems_WhenDataRefreshes_ResetsOrPreservesPage()
    {
        var pager = new PagedCollectionViewModel<int>();
        pager.SetItems(Enumerable.Range(1, 6));
        pager.SetPageSize(2);
        pager.NextPageCommand.Execute(null);

        pager.SetItems([10, 11, 12], resetPage: false);

        Assert.Equal(1, pager.PageIndex);
        Assert.Equal(new[] { 12 }, pager.Items);

        pager.SetItems([20, 21, 22]);

        Assert.Equal(0, pager.PageIndex);
        Assert.Equal(new[] { 20, 21 }, pager.Items);
    }
}
