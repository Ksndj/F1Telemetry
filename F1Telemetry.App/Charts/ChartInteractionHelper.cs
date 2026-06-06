using System.Windows;
using System.Windows.Input;
using ScottPlot.WPF;

namespace F1Telemetry.App.Charts;

/// <summary>
/// Applies fixed-chart interaction rules to telemetry chart controls.
/// </summary>
public static class ChartInteractionHelper
{
    /// <summary>
    /// Disables built-in zoom, pan, double-click, and context-menu interactions.
    /// </summary>
    /// <param name="chart">The ScottPlot WPF chart to lock.</param>
    public static void DisableFixedChartInteractions(WpfPlot chart)
    {
        ArgumentNullException.ThrowIfNull(chart);

        chart.UserInputProcessor.Disable();
        chart.Menu?.Clear();
    }

    /// <summary>
    /// Handles mouse-wheel events so fixed charts do not zoom or scroll their parent page.
    /// </summary>
    /// <param name="element">The WPF element that owns the chart input surface.</param>
    public static void AttachNoWheelZoomBehavior(UIElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        element.PreviewMouseWheel -= OnPreviewMouseWheel;
        element.PreviewMouseWheel += OnPreviewMouseWheel;
        element.MouseWheel -= OnPreviewMouseWheel;
        element.MouseWheel += OnPreviewMouseWheel;
    }

    /// <summary>
    /// Handles wheel input at the host window before parent scroll viewers can scroll.
    /// </summary>
    /// <param name="window">The window that hosts the chart control.</param>
    /// <param name="handler">The chart-specific wheel handler.</param>
    public static void AttachNoWheelZoomBehavior(Window window, MouseWheelEventHandler handler)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(handler);

        window.RemoveHandler(UIElement.PreviewMouseWheelEvent, handler);
        window.AddHandler(UIElement.PreviewMouseWheelEvent, handler, true);
    }

    /// <summary>
    /// Removes a previously attached host-window wheel handler.
    /// </summary>
    /// <param name="window">The window that hosts the chart control.</param>
    /// <param name="handler">The chart-specific wheel handler.</param>
    public static void DetachNoWheelZoomBehavior(Window window, MouseWheelEventHandler handler)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(handler);

        window.RemoveHandler(UIElement.PreviewMouseWheelEvent, handler);
    }

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;
    }
}
