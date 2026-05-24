using F1Telemetry.App.ViewModels;

namespace F1Telemetry.App.Charts;

internal static class TyreCompoundSeriesBuilder
{
    public static IReadOnlyList<ChartSeriesModel> BuildContiguousRuns<T>(
        IReadOnlyList<T> orderedSource,
        Func<T, int> lapSelector,
        Func<T, TyreCompoundChartStyle> styleSelector,
        Func<T, double> valueSelector,
        Func<TyreCompoundChartStyle, string> nameSelector)
    {
        var runs = new List<TyreCompoundSeriesRun>();
        var currentPoints = new List<ChartPointModel>();
        TyreCompoundChartStyle? currentStyle = null;
        string? currentBaseName = null;
        var firstLap = 0;
        var lastLap = 0;

        foreach (var item in orderedSource)
        {
            var lapNumber = lapSelector(item);
            var value = valueSelector(item);
            if (!double.IsFinite(lapNumber) || !double.IsFinite(value))
            {
                continue;
            }

            var style = styleSelector(item);
            var baseName = nameSelector(style);
            if (currentStyle is null || !string.Equals(currentStyle.Label, style.Label, StringComparison.Ordinal))
            {
                AddRun(runs, currentBaseName, currentStyle, firstLap, lastLap, currentPoints);
                currentPoints = [];
                currentStyle = style;
                currentBaseName = baseName;
                firstLap = lapNumber;
            }

            lastLap = lapNumber;
            currentPoints.Add(new ChartPointModel { X = lapNumber, Y = value });
        }

        AddRun(runs, currentBaseName, currentStyle, firstLap, lastLap, currentPoints);
        return BuildSeries(runs);
    }

    private static IReadOnlyList<ChartSeriesModel> BuildSeries(IReadOnlyList<TyreCompoundSeriesRun> runs)
    {
        var duplicateNames = runs
            .GroupBy(run => run.BaseName, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);

        return runs
            .Select(run => new ChartSeriesModel
            {
                Name = duplicateNames.Contains(run.BaseName)
                    ? $"{run.BaseName} L{run.FirstLap}-{run.LastLap}"
                    : run.BaseName,
                StrokeBrush = run.Style.StrokeBrush,
                Points = run.Points
            })
            .ToArray();
    }

    private static void AddRun(
        ICollection<TyreCompoundSeriesRun> runs,
        string? baseName,
        TyreCompoundChartStyle? style,
        int firstLap,
        int lastLap,
        IReadOnlyList<ChartPointModel> points)
    {
        if (baseName is null || style is null || points.Count == 0)
        {
            return;
        }

        runs.Add(new TyreCompoundSeriesRun(baseName, style, firstLap, lastLap, points.ToArray()));
    }

    private sealed record TyreCompoundSeriesRun(
        string BaseName,
        TyreCompoundChartStyle Style,
        int FirstLap,
        int LastLap,
        IReadOnlyList<ChartPointModel> Points);
}
