using System.Windows;
using F1Telemetry.App.ViewModels;

namespace F1Telemetry.App.AttachedProperties;

/// <summary>
/// Updates a paged collection page size from the host element viewport height.
/// </summary>
public static class AdaptivePageSizeBehavior
{
    private static readonly DependencyProperty IsHookedProperty =
        DependencyProperty.RegisterAttached(
            "IsHooked",
            typeof(bool),
            typeof(AdaptivePageSizeBehavior),
            new PropertyMetadata(false));

    /// <summary>
    /// Identifies the paged collection that should receive viewport page-size updates.
    /// </summary>
    public static readonly DependencyProperty PagedCollectionProperty =
        DependencyProperty.RegisterAttached(
            "PagedCollection",
            typeof(IViewportPageSizer),
            typeof(AdaptivePageSizeBehavior),
            new PropertyMetadata(null, OnSizingPropertyChanged));

    /// <summary>
    /// Identifies the estimated height for a single rendered item.
    /// </summary>
    public static readonly DependencyProperty EstimatedItemHeightProperty =
        DependencyProperty.RegisterAttached(
            "EstimatedItemHeight",
            typeof(double),
            typeof(AdaptivePageSizeBehavior),
            new PropertyMetadata(1d, OnSizingPropertyChanged));

    /// <summary>
    /// Identifies non-list chrome height to subtract from the host viewport.
    /// </summary>
    public static readonly DependencyProperty ChromeHeightProperty =
        DependencyProperty.RegisterAttached(
            "ChromeHeight",
            typeof(double),
            typeof(AdaptivePageSizeBehavior),
            new PropertyMetadata(0d, OnSizingPropertyChanged));

    /// <summary>
    /// Identifies the minimum adaptive page size.
    /// </summary>
    public static readonly DependencyProperty MinPageSizeProperty =
        DependencyProperty.RegisterAttached(
            "MinPageSize",
            typeof(int),
            typeof(AdaptivePageSizeBehavior),
            new PropertyMetadata(1, OnSizingPropertyChanged));

    /// <summary>
    /// Identifies the maximum adaptive page size.
    /// </summary>
    public static readonly DependencyProperty MaxPageSizeProperty =
        DependencyProperty.RegisterAttached(
            "MaxPageSize",
            typeof(int),
            typeof(AdaptivePageSizeBehavior),
            new PropertyMetadata(50, OnSizingPropertyChanged));

    /// <summary>
    /// Gets the paged collection that receives viewport page-size updates.
    /// </summary>
    /// <param name="element">The target element.</param>
    /// <returns>The configured paged collection.</returns>
    public static IViewportPageSizer? GetPagedCollection(DependencyObject element)
    {
        return (IViewportPageSizer?)element.GetValue(PagedCollectionProperty);
    }

    /// <summary>
    /// Sets the paged collection that receives viewport page-size updates.
    /// </summary>
    /// <param name="element">The target element.</param>
    /// <param name="value">The paged collection.</param>
    public static void SetPagedCollection(DependencyObject element, IViewportPageSizer? value)
    {
        element.SetValue(PagedCollectionProperty, value);
    }

    /// <summary>
    /// Gets the estimated height for a single rendered item.
    /// </summary>
    /// <param name="element">The target element.</param>
    /// <returns>The configured item height.</returns>
    public static double GetEstimatedItemHeight(DependencyObject element)
    {
        return (double)element.GetValue(EstimatedItemHeightProperty);
    }

    /// <summary>
    /// Sets the estimated height for a single rendered item.
    /// </summary>
    /// <param name="element">The target element.</param>
    /// <param name="value">The item height.</param>
    public static void SetEstimatedItemHeight(DependencyObject element, double value)
    {
        element.SetValue(EstimatedItemHeightProperty, value);
    }

    /// <summary>
    /// Gets non-list chrome height to subtract from the host viewport.
    /// </summary>
    /// <param name="element">The target element.</param>
    /// <returns>The configured chrome height.</returns>
    public static double GetChromeHeight(DependencyObject element)
    {
        return (double)element.GetValue(ChromeHeightProperty);
    }

    /// <summary>
    /// Sets non-list chrome height to subtract from the host viewport.
    /// </summary>
    /// <param name="element">The target element.</param>
    /// <param name="value">The chrome height.</param>
    public static void SetChromeHeight(DependencyObject element, double value)
    {
        element.SetValue(ChromeHeightProperty, value);
    }

    /// <summary>
    /// Gets the minimum adaptive page size.
    /// </summary>
    /// <param name="element">The target element.</param>
    /// <returns>The configured minimum page size.</returns>
    public static int GetMinPageSize(DependencyObject element)
    {
        return (int)element.GetValue(MinPageSizeProperty);
    }

    /// <summary>
    /// Sets the minimum adaptive page size.
    /// </summary>
    /// <param name="element">The target element.</param>
    /// <param name="value">The minimum page size.</param>
    public static void SetMinPageSize(DependencyObject element, int value)
    {
        element.SetValue(MinPageSizeProperty, value);
    }

    /// <summary>
    /// Gets the maximum adaptive page size.
    /// </summary>
    /// <param name="element">The target element.</param>
    /// <returns>The configured maximum page size.</returns>
    public static int GetMaxPageSize(DependencyObject element)
    {
        return (int)element.GetValue(MaxPageSizeProperty);
    }

    /// <summary>
    /// Sets the maximum adaptive page size.
    /// </summary>
    /// <param name="element">The target element.</param>
    /// <param name="value">The maximum page size.</param>
    public static void SetMaxPageSize(DependencyObject element, int value)
    {
        element.SetValue(MaxPageSizeProperty, value);
    }

    private static void OnSizingPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not FrameworkElement element)
        {
            return;
        }

        EnsureHooked(element);
        UpdatePageSize(element);
    }

    private static void EnsureHooked(FrameworkElement element)
    {
        if ((bool)element.GetValue(IsHookedProperty))
        {
            return;
        }

        element.SetValue(IsHookedProperty, true);
        element.Loaded += Element_Loaded;
        element.SizeChanged += Element_SizeChanged;
    }

    private static void Element_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            UpdatePageSize(element);
        }
    }

    private static void Element_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            UpdatePageSize(element);
        }
    }

    private static void UpdatePageSize(FrameworkElement element)
    {
        var pagedCollection = GetPagedCollection(element);
        if (pagedCollection is null)
        {
            return;
        }

        var viewportHeight = element.ActualHeight - GetChromeHeight(element);
        pagedCollection.SetPageSizeFromViewport(
            viewportHeight,
            GetEstimatedItemHeight(element),
            GetMinPageSize(element),
            GetMaxPageSize(element));
    }
}
