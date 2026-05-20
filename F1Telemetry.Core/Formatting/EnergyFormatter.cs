using System.Globalization;

namespace F1Telemetry.Core.Formatting;

/// <summary>
/// Formats ERS energy values without exposing large raw joule numbers to users.
/// </summary>
public static class EnergyFormatter
{
    /// <summary>
    /// Gets the default F1 25 ERS store capacity used for percentage formatting.
    /// </summary>
    public const float DefaultErsStoreCapacityJoules = 4_000_000f;

    /// <summary>
    /// Formats raw joules as megajoules with one decimal place.
    /// </summary>
    /// <param name="joules">The energy value in joules.</param>
    public static string FormatMegaJoules(float joules)
    {
        var megaJoules = Math.Max(0f, joules) / 1_000_000f;
        return string.Create(CultureInfo.InvariantCulture, $"{megaJoules:0.0} MJ");
    }

    /// <summary>
    /// Formats an ERS store value as megajoules and, when possible, a percentage.
    /// </summary>
    /// <param name="joules">The current ERS store value in joules.</param>
    /// <param name="maxJoules">The maximum ERS store value in joules.</param>
    public static string FormatErs(float? joules, float? maxJoules = DefaultErsStoreCapacityJoules)
    {
        if (joules is null)
        {
            return "-";
        }

        var energyText = FormatMegaJoules(joules.Value);
        if (maxJoules is null || maxJoules.Value <= 0)
        {
            return energyText;
        }

        var percent = Math.Clamp(joules.Value / maxJoules.Value, 0f, 1f) * 100f;
        return string.Create(CultureInfo.InvariantCulture, $"{energyText} · {percent:0}%");
    }
}
