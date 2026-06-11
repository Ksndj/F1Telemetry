namespace F1Telemetry.App.ViewModels;

/// <summary>
/// Adapts a paged collection to the available visual viewport.
/// </summary>
public interface IViewportPageSizer
{
    /// <summary>
    /// Sets the page size from the available viewport height and estimated item height.
    /// </summary>
    /// <param name="viewportHeight">The available viewport height.</param>
    /// <param name="estimatedItemHeight">The estimated item height.</param>
    /// <param name="minPageSize">The minimum page size.</param>
    /// <param name="maxPageSize">The maximum page size.</param>
    void SetPageSizeFromViewport(
        double viewportHeight,
        double estimatedItemHeight,
        int minPageSize = 1,
        int maxPageSize = 50);
}
