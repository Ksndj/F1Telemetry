using F1Telemetry.Core.Formatting;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies user-facing ERS energy formatting.
/// </summary>
public sealed class EnergyFormatterTests
{
    /// <summary>
    /// Verifies raw joules are displayed as short megajoule values.
    /// </summary>
    [Theory]
    [InlineData(998_136f, "1.0 MJ")]
    [InlineData(1_200_000f, "1.2 MJ")]
    public void FormatMegaJoules_UsesShortMegajouleText(float joules, string expected)
    {
        var text = EnergyFormatter.FormatMegaJoules(joules);

        Assert.Equal(expected, text);
        Assert.DoesNotContain("焦耳", text, StringComparison.Ordinal);
        Assert.DoesNotContain(((int)joules).ToString(), text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies ERS formatting includes percentage when the maximum store is known.
    /// </summary>
    [Fact]
    public void FormatErs_WithMaximum_IncludesPercentage()
    {
        var text = EnergyFormatter.FormatErs(1_200_000f, 4_000_000f);

        Assert.Equal("1.2 MJ · 30%", text);
        Assert.DoesNotContain("1200000", text, StringComparison.Ordinal);
    }
}
