using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace F1Telemetry.App.Controls;

/// <summary>
/// Arranges dashboard status cards as equal-width 4/2/1 columns based on the available width.
/// </summary>
public sealed class ResponsiveStatusCardsPanel : Panel
{
    /// <summary>
    /// Identifies the <see cref="WideColumns" /> dependency property.
    /// </summary>
    public static readonly DependencyProperty WideColumnsProperty =
        DependencyProperty.Register(
            nameof(WideColumns),
            typeof(int),
            typeof(ResponsiveStatusCardsPanel),
            new FrameworkPropertyMetadata(4, FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>
    /// Identifies the <see cref="MediumColumns" /> dependency property.
    /// </summary>
    public static readonly DependencyProperty MediumColumnsProperty =
        DependencyProperty.Register(
            nameof(MediumColumns),
            typeof(int),
            typeof(ResponsiveStatusCardsPanel),
            new FrameworkPropertyMetadata(2, FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>
    /// Identifies the <see cref="NarrowColumns" /> dependency property.
    /// </summary>
    public static readonly DependencyProperty NarrowColumnsProperty =
        DependencyProperty.Register(
            nameof(NarrowColumns),
            typeof(int),
            typeof(ResponsiveStatusCardsPanel),
            new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>
    /// Identifies the <see cref="WideMinWidth" /> dependency property.
    /// </summary>
    public static readonly DependencyProperty WideMinWidthProperty =
        DependencyProperty.Register(
            nameof(WideMinWidth),
            typeof(double),
            typeof(ResponsiveStatusCardsPanel),
            new FrameworkPropertyMetadata(1120d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>
    /// Identifies the <see cref="MediumMinWidth" /> dependency property.
    /// </summary>
    public static readonly DependencyProperty MediumMinWidthProperty =
        DependencyProperty.Register(
            nameof(MediumMinWidth),
            typeof(double),
            typeof(ResponsiveStatusCardsPanel),
            new FrameworkPropertyMetadata(720d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>
    /// Gets or sets the number of columns used for wide layouts.
    /// </summary>
    public int WideColumns
    {
        get => (int)GetValue(WideColumnsProperty);
        set => SetValue(WideColumnsProperty, value);
    }

    /// <summary>
    /// Gets or sets the number of columns used for medium layouts.
    /// </summary>
    public int MediumColumns
    {
        get => (int)GetValue(MediumColumnsProperty);
        set => SetValue(MediumColumnsProperty, value);
    }

    /// <summary>
    /// Gets or sets the number of columns used for narrow layouts.
    /// </summary>
    public int NarrowColumns
    {
        get => (int)GetValue(NarrowColumnsProperty);
        set => SetValue(NarrowColumnsProperty, value);
    }

    /// <summary>
    /// Gets or sets the minimum width required for the wide layout.
    /// </summary>
    public double WideMinWidth
    {
        get => (double)GetValue(WideMinWidthProperty);
        set => SetValue(WideMinWidthProperty, value);
    }

    /// <summary>
    /// Gets or sets the minimum width required for the medium layout.
    /// </summary>
    public double MediumMinWidth
    {
        get => (double)GetValue(MediumMinWidthProperty);
        set => SetValue(MediumMinWidthProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var availableWidth = double.IsInfinity(availableSize.Width)
            ? 0d
            : Math.Max(0d, availableSize.Width);
        var columns = ResolveColumnCount(availableWidth);
        var cellWidth = availableWidth > 0d
            ? availableWidth / columns
            : double.PositiveInfinity;
        var rowHeights = new double[GetRowCount(columns)];

        for (var index = 0; index < InternalChildren.Count; index++)
        {
            var child = InternalChildren[index];
            child.Measure(new Size(cellWidth, double.PositiveInfinity));
            var row = index / columns;
            rowHeights[row] = Math.Max(rowHeights[row], child.DesiredSize.Height);
        }

        var desiredHeight = rowHeights.Sum();
        var desiredWidth = availableWidth > 0d
            ? availableWidth
            : InternalChildren.Cast<UIElement>().Select(child => child.DesiredSize.Width).DefaultIfEmpty(0d).Max();
        return new Size(desiredWidth, desiredHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var finalWidth = Math.Max(0d, finalSize.Width);
        var columns = ResolveColumnCount(finalWidth);
        var cellWidth = columns > 0 ? finalWidth / columns : finalWidth;
        var rowHeights = new double[GetRowCount(columns)];

        for (var index = 0; index < InternalChildren.Count; index++)
        {
            var row = index / columns;
            rowHeights[row] = Math.Max(rowHeights[row], InternalChildren[index].DesiredSize.Height);
        }

        var y = 0d;
        for (var row = 0; row < rowHeights.Length; row++)
        {
            var rowHeight = rowHeights[row];
            for (var column = 0; column < columns; column++)
            {
                var index = row * columns + column;
                if (index >= InternalChildren.Count)
                {
                    break;
                }

                InternalChildren[index].Arrange(new Rect(column * cellWidth, y, cellWidth, rowHeight));
            }

            y += rowHeight;
        }

        return finalSize;
    }

    private int ResolveColumnCount(double availableWidth)
    {
        if (availableWidth >= WideMinWidth)
        {
            return Math.Max(1, WideColumns);
        }

        if (availableWidth >= MediumMinWidth)
        {
            return Math.Max(1, MediumColumns);
        }

        return Math.Max(1, NarrowColumns);
    }

    private int GetRowCount(int columns)
    {
        return Math.Max(1, (int)Math.Ceiling(InternalChildren.Count / (double)Math.Max(1, columns)));
    }
}
