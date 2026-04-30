using System.Globalization;
using System.Text;
using F1Telemetry.Analytics.Laps;

namespace F1Telemetry.AI.Services;

/// <summary>
/// Converts live telemetry samples and completed-lap trends into compact AI broadcast context.
/// </summary>
public sealed class TelemetryAnalysisSummaryBuilder
{
    /// <summary>
    /// Builds a short Chinese summary from the same telemetry streams that previously backed live charts.
    /// </summary>
    /// <param name="currentLapSamples">Current in-flight lap samples.</param>
    /// <param name="recentLaps">Recent completed lap summaries.</param>
    /// <returns>A compact trend summary, or an empty string when no useful data is available.</returns>
    public string Build(IReadOnlyList<LapSample> currentLapSamples, IReadOnlyList<LapSummary> recentLaps)
    {
        ArgumentNullException.ThrowIfNull(currentLapSamples);
        ArgumentNullException.ThrowIfNull(recentLaps);

        var sections = new List<string>(capacity: 3);
        AppendCurrentLapSummary(sections, currentLapSamples);
        AppendFuelTrendSummary(sections, recentLaps);
        AppendTyreWearTrendSummary(sections, recentLaps);

        return string.Join("；", sections);
    }

    private static void AppendCurrentLapSummary(ICollection<string> sections, IReadOnlyList<LapSample> currentLapSamples)
    {
        if (currentLapSamples.Count == 0)
        {
            return;
        }

        var builder = new StringBuilder();
        builder.AppendFormat(CultureInfo.InvariantCulture, "当前圈采样 {0} 个", currentLapSamples.Count);

        var maxSpeed = currentLapSamples
            .Where(sample => sample.SpeedKph is not null)
            .Select(sample => sample.SpeedKph!.Value)
            .DefaultIfEmpty(double.NaN)
            .Max();
        if (!double.IsNaN(maxSpeed))
        {
            builder.AppendFormat(CultureInfo.InvariantCulture, "，最高速度 {0:0} km/h", maxSpeed);
        }

        var maxThrottle = currentLapSamples
            .Where(sample => sample.Throttle is not null)
            .Select(sample => sample.Throttle!.Value * 100d)
            .DefaultIfEmpty(double.NaN)
            .Max();
        if (!double.IsNaN(maxThrottle))
        {
            builder.AppendFormat(CultureInfo.InvariantCulture, "，最大油门 {0:0}%", maxThrottle);
        }

        var maxBrake = currentLapSamples
            .Where(sample => sample.Brake is not null)
            .Select(sample => sample.Brake!.Value * 100d)
            .DefaultIfEmpty(double.NaN)
            .Max();
        if (!double.IsNaN(maxBrake))
        {
            builder.AppendFormat(CultureInfo.InvariantCulture, "，最大刹车 {0:0}%", maxBrake);
        }

        sections.Add(builder.ToString());
    }

    private static void AppendFuelTrendSummary(ICollection<string> sections, IReadOnlyList<LapSummary> recentLaps)
    {
        var fuelLaps = recentLaps
            .Where(lap => lap.FuelUsedLitres is not null)
            .OrderBy(lap => lap.LapNumber)
            .ToArray();
        if (fuelLaps.Length == 0)
        {
            return;
        }

        var minFuel = fuelLaps.Min(lap => lap.FuelUsedLitres!.Value);
        var maxFuel = fuelLaps.Max(lap => lap.FuelUsedLitres!.Value);
        sections.Add(string.Format(
            CultureInfo.InvariantCulture,
            "近 {0} 圈燃油 {1:0.00}-{2:0.00} L",
            fuelLaps.Length,
            minFuel,
            maxFuel));
    }

    private static void AppendTyreWearTrendSummary(ICollection<string> sections, IReadOnlyList<LapSummary> recentLaps)
    {
        var latestTyreWear = recentLaps
            .Where(lap => lap.TyreWearDeltaPerWheel is not null)
            .OrderByDescending(lap => lap.LapNumber)
            .Select(lap => lap.TyreWearDeltaPerWheel!)
            .FirstOrDefault();
        if (latestTyreWear is null)
        {
            return;
        }

        sections.Add(string.Format(
            CultureInfo.InvariantCulture,
            "最近胎磨增量 后左 {0:0.0}%、后右 {1:0.0}%、前左 {2:0.0}%、前右 {3:0.0}%",
            latestTyreWear.RearLeft,
            latestTyreWear.RearRight,
            latestTyreWear.FrontLeft,
            latestTyreWear.FrontRight));
    }
}
