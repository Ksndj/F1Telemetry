using System.Reflection;
using F1Telemetry.Analytics.Laps;
using F1Telemetry.App.Formatting;
using F1Telemetry.App.ViewModels;
using F1Telemetry.Core.Models;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies Chinese display mappings for raw F1 25 telemetry codes.
/// </summary>
public sealed class DisplaySemanticFormatterTests
{
    /// <summary>
    /// Verifies that known track identifiers are displayed as Chinese short names.
    /// </summary>
    [Fact]
    public void TrackNameFormatter_Format_KnownTrackId_ReturnsChineseShortName()
    {
        Assert.Equal("澳洲", TrackNameFormatter.Format(0));
        Assert.Equal("上海", TrackNameFormatter.Format(2));
        Assert.Equal("比利时", TrackNameFormatter.Format(10));
    }

    /// <summary>
    /// Verifies that unknown track identifiers keep a readable fallback with the raw ID.
    /// </summary>
    [Fact]
    public void TrackNameFormatter_Format_UnknownTrackId_ReturnsFallbackWithId()
    {
        Assert.Equal("未知赛道（ID 99）", TrackNameFormatter.Format(99));
        Assert.Equal("未知赛道", TrackNameFormatter.Format(null));
    }

    /// <summary>
    /// Verifies that raw session type values are grouped into Chinese race formats.
    /// </summary>
    [Theory]
    [InlineData(1, "练习赛")]
    [InlineData(5, "排位赛")]
    [InlineData(10, "冲刺排位")]
    [InlineData(16, "冲刺赛")]
    [InlineData(15, "正赛")]
    [InlineData(18, "时间试跑 / 计时赛")]
    [InlineData(255, "未知赛制")]
    public void SessionTypeFormatter_Format_ReturnsChineseSessionType(byte sessionType, string expected)
    {
        Assert.Equal(expected, SessionTypeFormatter.Format(sessionType));
    }

    /// <summary>
    /// Verifies that visual tyre compound is used before actual compound.
    /// The dry visual mappings are calibrated from the 2026-04-26 practice raw log observations.
    /// </summary>
    [Theory]
    [InlineData(16, 8, "红胎")]
    [InlineData(17, 8, "黄胎")]
    [InlineData(18, 8, "白胎")]
    [InlineData(7, 16, "半雨胎")]
    [InlineData(8, 16, "全雨胎")]
    public void TyreCompoundFormatter_Format_UsesVisualCompoundFirst(byte visual, byte actual, string expected)
    {
        Assert.Equal(expected, TyreCompoundFormatter.Format(visual, actual, hasTelemetryAccess: true));
    }

    /// <summary>
    /// Verifies that actual wet compounds remain safe to display when visual compound is absent.
    /// </summary>
    [Theory]
    [InlineData(7, "半雨胎")]
    [InlineData(8, "全雨胎")]
    public void TyreCompoundFormatter_Format_ActualWetCompoundWithoutVisual_ReturnsWetTyre(byte actual, string expected)
    {
        Assert.Equal(expected, TyreCompoundFormatter.Format(null, actual, hasTelemetryAccess: true));
    }

    /// <summary>
    /// Verifies that actual dry compounds are not guessed as soft, medium, or hard.
    /// </summary>
    [Theory]
    [InlineData(16)]
    [InlineData(19)]
    [InlineData(22)]
    public void TyreCompoundFormatter_Format_ActualDryCompoundWithoutVisual_ReturnsUnknownWithCode(byte actual)
    {
        Assert.Equal($"未知胎（编码 {actual}）", TyreCompoundFormatter.Format(null, actual, hasTelemetryAccess: true));
    }

    /// <summary>
    /// Verifies that restricted telemetry never leaks tyre compound codes through formal text.
    /// </summary>
    [Fact]
    public void TyreCompoundFormatter_Format_RestrictedTelemetryWithCodes_ReturnsRestrictedText()
    {
        Assert.Equal("遥测受限", TyreCompoundFormatter.Format(16, 16, hasTelemetryAccess: false));
        Assert.Equal("遥测受限", TyreCompoundFormatter.Format(null, 7, hasTelemetryAccess: false));
    }

    /// <summary>
    /// Verifies that legacy lap tyre codes are localized without exposing raw L-prefixed text.
    /// </summary>
    [Fact]
    public void TyreCompoundFormatter_FormatRawCompoundText_LegacyLapCode_ReturnsUnknownCodeFallback()
    {
        var text = TyreCompoundFormatter.FormatRawCompoundText("L16");

        Assert.Equal("未知胎（编码 16）", text);
        Assert.DoesNotContain("L16", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that tyre formatter fallbacks remain visible and non-empty.
    /// </summary>
    [Fact]
    public void TyreCompoundFormatter_Format_UnknownAndRestrictedValues_ReturnReadableFallbacks()
    {
        Assert.Equal("未知胎（编码 99）", TyreCompoundFormatter.Format(99, null, hasTelemetryAccess: true));
        Assert.Equal("未知胎", TyreCompoundFormatter.Format(null, null, hasTelemetryAccess: true));
        Assert.Equal("遥测受限", TyreCompoundFormatter.Format(null, null, hasTelemetryAccess: false));
    }

    /// <summary>
    /// Verifies that pit status display uses readable Chinese labels.
    /// </summary>
    [Theory]
    [InlineData(0, 0, "赛道")]
    [InlineData(0, 2, "已进站 2 次")]
    [InlineData(1, 0, "进站中")]
    [InlineData(2, 0, "维修区")]
    [InlineData(9, 0, "未知")]
    public void PitStatusFormatter_Format_ReturnsReadablePitStatus(byte pitStatus, byte numPitStops, string expected)
    {
        Assert.Equal(expected, PitStatusFormatter.Format(pitStatus, numPitStops));
    }

    /// <summary>
    /// Verifies that opponent gap display avoids unclear same-position text.
    /// </summary>
    [Fact]
    public void OpponentStatusFormatter_FormatGapToPlayer_ReturnsReadableRelativeGap()
    {
        var player = CreateCar(position: 2, deltaToLeaderInMs: 10_000);
        var front = CreateCar(position: 1, deltaToLeaderInMs: 8_000);
        var behind = CreateCar(position: 3, deltaToLeaderInMs: 13_000);
        var sameLap = CreateCar(position: 3, deltaToLeaderInMs: 10_000);

        Assert.Equal("前 2.000s", OpponentStatusFormatter.FormatGapToPlayer(front, player));
        Assert.Equal("后 3.000s", OpponentStatusFormatter.FormatGapToPlayer(behind, player));
        Assert.Equal("同圈", OpponentStatusFormatter.FormatGapToPlayer(sameLap, player));
        Assert.Equal("不可用", OpponentStatusFormatter.FormatGapToPlayer(CreateCar(position: null, deltaToLeaderInMs: null), player));
    }

    /// <summary>
    /// Verifies that dashboard formal track text no longer exposes raw track ID wording.
    /// </summary>
    [Fact]
    public void DashboardViewModel_BuildTrackText_DoesNotExposeRawTrackId()
    {
        var displayText = InvokeDashboardTrackText(10);

        Assert.Equal("比利时", displayText);
        Assert.DoesNotContain("赛道 ID", displayText, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that the dashboard exposes a bindable session type display property.
    /// </summary>
    [Fact]
    public void DashboardViewModel_ExposesSessionTypeText()
    {
        Assert.NotNull(typeof(DashboardViewModel).GetProperty("SessionTypeText"));
    }

    /// <summary>
    /// Verifies that opponent rows use readable tyre and telemetry-restricted text.
    /// </summary>
    [Fact]
    public void CarStateItemViewModel_FromSnapshot_DoesNotExposeRawTyreCodes()
    {
        var player = CreateCar(position: 2, deltaToLeaderInMs: 10_000);
        var opponent = CreateCar(position: 3, deltaToLeaderInMs: 13_000) with
        {
            VisualTyreCompound = 16,
            ActualTyreCompound = 19,
            PitStatus = 0,
            NumPitStops = 0
        };

        var item = CarStateItemViewModel.FromSnapshot(opponent, player);

        Assert.Equal("红胎", item.TyreText);
        Assert.DoesNotContain("V16", item.TyreText, StringComparison.Ordinal);
        Assert.DoesNotContain("A19", item.TyreText, StringComparison.Ordinal);
        Assert.DoesNotContain("L16", item.TyreText, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that telemetry-restricted opponents display a readable restriction label.
    /// </summary>
    [Fact]
    public void CarStateItemViewModel_FromRestrictedSnapshot_ShowsTelemetryRestricted()
    {
        var restrictedOpponent = CreateCar(position: 3, deltaToLeaderInMs: null) with
        {
            IsTelemetryRestricted = true,
            VisualTyreCompound = null,
            ActualTyreCompound = null
        };

        var item = CarStateItemViewModel.FromSnapshot(restrictedOpponent, playerCar: null);

        Assert.Equal("遥测受限", item.TyreText);
    }

    /// <summary>
    /// Verifies that player overview tyre text uses a Chinese compound name as its formal UI text.
    /// </summary>
    [Fact]
    public void DashboardViewModel_BuildTyreText_DoesNotExposeRawTyreCodes()
    {
        var player = CreateCar(position: 1, deltaToLeaderInMs: 0) with
        {
            IsPlayer = true,
            VisualTyreCompound = 16,
            ActualTyreCompound = 16
        };

        var text = InvokeDashboardTyreText(player);

        Assert.Equal("红胎", text);
        Assert.DoesNotContain("V16", text, StringComparison.Ordinal);
        Assert.DoesNotContain("A16", text, StringComparison.Ordinal);
        Assert.DoesNotContain("L16", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that lap history rows localize raw tyre summary text before display.
    /// </summary>
    [Fact]
    public void LapSummaryItemViewModel_FromSummary_DoesNotExposeRawTyreCodes()
    {
        var summary = new LapSummary
        {
            LapNumber = 5,
            StartTyre = "V16 / A19",
            EndTyre = "V17 / A20",
            ClosedAt = DateTimeOffset.UtcNow
        };

        var item = LapSummaryItemViewModel.FromSummary(summary);

        Assert.Equal("红胎 -> 黄胎", item.TyreWindowText);
        Assert.DoesNotContain("V16", item.TyreWindowText, StringComparison.Ordinal);
        Assert.DoesNotContain("A19", item.TyreWindowText, StringComparison.Ordinal);
    }

    private static CarSnapshot CreateCar(byte? position, ushort? deltaToLeaderInMs)
    {
        return new CarSnapshot
        {
            CarIndex = position ?? 0,
            DriverName = "Driver",
            Position = position,
            DeltaToRaceLeaderInMs = deltaToLeaderInMs
        };
    }

    private static string InvokeDashboardTrackText(sbyte? trackId)
    {
        var method = typeof(DashboardViewModel).GetMethod("BuildTrackText", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return Assert.IsType<string>(method!.Invoke(null, new object?[] { trackId }));
    }

    private static string InvokeDashboardTyreText(CarSnapshot playerCar)
    {
        var method = typeof(DashboardViewModel).GetMethod("BuildTyreText", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return Assert.IsType<string>(method!.Invoke(null, new object?[] { playerCar }));
    }
}
