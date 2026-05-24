using System.Windows.Media;
using F1Telemetry.App.Formatting;

namespace F1Telemetry.App.Charts;

/// <summary>
/// Provides chart labels and official compound colors for tyre-related series.
/// </summary>
public static class TyreCompoundChartPalette
{
    private static readonly TyreCompoundChartStyle Soft = new("红胎", CreateBrush("#E10600"), 0);
    private static readonly TyreCompoundChartStyle Medium = new("黄胎", CreateBrush("#FFD12E"), 1);
    private static readonly TyreCompoundChartStyle Hard = new("白胎", CreateBrush("#F2F2F2"), 2);
    private static readonly TyreCompoundChartStyle Intermediate = new("半雨胎", CreateBrush("#43B02A"), 3);
    private static readonly TyreCompoundChartStyle Wet = new("全雨胎", CreateBrush("#009FE3"), 4);
    private static readonly TyreCompoundChartStyle Unknown = new("未知胎", CreateBrush("#9AB0C9"), 99);

    /// <summary>
    /// Resolves a chart style from stored raw visual and actual tyre compound identifiers.
    /// </summary>
    /// <param name="visualCompound">The raw visual tyre compound identifier.</param>
    /// <param name="actualCompound">The raw actual tyre compound identifier.</param>
    public static TyreCompoundChartStyle FromCodes(int? visualCompound, int? actualCompound)
    {
        var visual = ToByte(visualCompound);
        if (visual is not null)
        {
            return FromFormattedLabel(TyreCompoundFormatter.Format(visual, null, hasTelemetryAccess: true));
        }

        var actual = ToByte(actualCompound);
        return actual is null
            ? Unknown
            : FromFormattedLabel(TyreCompoundFormatter.Format(null, actual, hasTelemetryAccess: true));
    }

    /// <summary>
    /// Resolves a chart style from raw lap tyre text such as "V16 / A19".
    /// </summary>
    /// <param name="rawCompoundText">The stored tyre compound text.</param>
    public static TyreCompoundChartStyle FromRawCompoundText(string? rawCompoundText)
    {
        return FromFormattedLabel(TyreCompoundFormatter.FormatRawCompoundText(rawCompoundText));
    }

    /// <summary>
    /// Resolves a chart style from a formatted tyre compound label.
    /// </summary>
    /// <param name="label">The formatted tyre compound label.</param>
    public static TyreCompoundChartStyle FromFormattedLabel(string? label)
    {
        return label?.Trim() switch
        {
            "红胎" or "Soft" => Soft,
            "黄胎" or "Medium" => Medium,
            "白胎" or "Hard" => Hard,
            "半雨胎" or "Intermediate" => Intermediate,
            "全雨胎" or "Wet" => Wet,
            _ => Unknown
        };
    }

    private static byte? ToByte(int? value)
    {
        return value is >= byte.MinValue and <= byte.MaxValue ? (byte)value.Value : null;
    }

    private static Brush CreateBrush(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}

/// <summary>
/// Represents the display label, stroke brush, and order for one tyre compound chart style.
/// </summary>
public sealed record TyreCompoundChartStyle(string Label, Brush StrokeBrush, int SortOrder);
