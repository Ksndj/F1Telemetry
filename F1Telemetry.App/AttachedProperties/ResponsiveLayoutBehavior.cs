using System.Globalization;
using System.Windows;

namespace F1Telemetry.App.AttachedProperties;

/// <summary>
/// Switches visual states from element width breakpoints.
/// </summary>
public static class ResponsiveLayoutBehavior
{
    private static readonly DependencyProperty IsHookedProperty =
        DependencyProperty.RegisterAttached(
            "IsHooked",
            typeof(bool),
            typeof(ResponsiveLayoutBehavior),
            new PropertyMetadata(false));

    /// <summary>
    /// Identifies comma-separated width breakpoints that divide narrow, medium, and wide states.
    /// </summary>
    public static readonly DependencyProperty BreakpointsProperty =
        DependencyProperty.RegisterAttached(
            "Breakpoints",
            typeof(string),
            typeof(ResponsiveLayoutBehavior),
            new PropertyMetadata(string.Empty, OnResponsivePropertyChanged));

    /// <summary>
    /// Identifies the current responsive state name.
    /// </summary>
    public static readonly DependencyProperty CurrentStateProperty =
        DependencyProperty.RegisterAttached(
            "CurrentState",
            typeof(string),
            typeof(ResponsiveLayoutBehavior),
            new FrameworkPropertyMetadata("Wide", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    /// <summary>
    /// Gets comma-separated width breakpoints that divide narrow, medium, and wide states.
    /// </summary>
    /// <param name="element">The target element.</param>
    /// <returns>The configured breakpoint text.</returns>
    public static string GetBreakpoints(DependencyObject element)
    {
        return (string)element.GetValue(BreakpointsProperty);
    }

    /// <summary>
    /// Sets comma-separated width breakpoints that divide narrow, medium, and wide states.
    /// </summary>
    /// <param name="element">The target element.</param>
    /// <param name="value">The breakpoint text.</param>
    public static void SetBreakpoints(DependencyObject element, string value)
    {
        element.SetValue(BreakpointsProperty, value);
    }

    /// <summary>
    /// Gets the current responsive state name.
    /// </summary>
    /// <param name="element">The target element.</param>
    /// <returns>The current state name.</returns>
    public static string GetCurrentState(DependencyObject element)
    {
        return (string)element.GetValue(CurrentStateProperty);
    }

    /// <summary>
    /// Sets the current responsive state name.
    /// </summary>
    /// <param name="element">The target element.</param>
    /// <param name="value">The state name.</param>
    public static void SetCurrentState(DependencyObject element, string value)
    {
        element.SetValue(CurrentStateProperty, value);
    }

    private static void OnResponsivePropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not FrameworkElement element)
        {
            return;
        }

        EnsureHooked(element);
        UpdateVisualState(element);
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
            UpdateVisualState(element);
        }
    }

    private static void Element_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            UpdateVisualState(element);
        }
    }

    private static void UpdateVisualState(FrameworkElement element)
    {
        var breakpoints = ParseBreakpoints(GetBreakpoints(element));
        if (breakpoints.Length < 2 || !double.IsFinite(element.ActualWidth) || element.ActualWidth <= 0)
        {
            return;
        }

        var state = element.ActualWidth < breakpoints[0]
            ? "Narrow"
            : element.ActualWidth < breakpoints[1]
                ? "Medium"
                : "Wide";
        SetCurrentState(element, state);
        VisualStateManager.GoToElementState(element, state, useTransitions: false);
    }

    private static double[] ParseBreakpoints(string value)
    {
        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var breakpoint)
                ? breakpoint
                : double.NaN)
            .Where(double.IsFinite)
            .OrderBy(breakpoint => breakpoint)
            .ToArray();
    }
}
