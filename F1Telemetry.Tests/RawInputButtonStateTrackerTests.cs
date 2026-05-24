using F1Telemetry.App.Services;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies Raw Input HID button state snapshots produce stable button edges.
/// </summary>
public sealed class RawInputButtonStateTrackerTests
{
    /// <summary>
    /// Verifies a first report with pressed button usages emits the first real press.
    /// </summary>
    [Fact]
    public void Observe_FirstReportWithPressedButton_EmitsPressEdge()
    {
        var tracker = new RawInputButtonStateTracker();

        var edges = tracker.Observe("device-1", reportId: 0, [4]);

        var edge = Assert.Single(edges);
        Assert.Equal(4, edge.ButtonIndex);
        Assert.True(edge.IsPressed);
    }

    /// <summary>
    /// Verifies real button usage changes emit one press and one release edge.
    /// </summary>
    [Fact]
    public void Observe_ButtonUsageChange_EmitsPressAndRelease()
    {
        var tracker = new RawInputButtonStateTracker();
        tracker.Observe("device-1", reportId: 0, []);

        var pressEdges = tracker.Observe("device-1", reportId: 0, [4]);
        var releaseEdges = tracker.Observe("device-1", reportId: 0, []);

        var press = Assert.Single(pressEdges);
        Assert.Equal(4, press.ButtonIndex);
        Assert.True(press.IsPressed);
        Assert.Equal(1, press.PressedChangeCount);
        Assert.Equal(1, press.ChangedButtonCount);

        var release = Assert.Single(releaseEdges);
        Assert.Equal(4, release.ButtonIndex);
        Assert.False(release.IsPressed);
        Assert.Equal(0, release.PressedChangeCount);
        Assert.Equal(1, release.ChangedButtonCount);
    }

    /// <summary>
    /// Verifies repeated button snapshots do not emit duplicate press edges.
    /// </summary>
    [Fact]
    public void Observe_RepeatedPressedSnapshot_DoesNotEmitDuplicateEdge()
    {
        var tracker = new RawInputButtonStateTracker();
        tracker.Observe("device-1", reportId: 0, []);
        tracker.Observe("device-1", reportId: 0, [4]);

        var edges = tracker.Observe("device-1", reportId: 0, [4]);

        Assert.Empty(edges);
    }

    /// <summary>
    /// Verifies non-button report changes modeled as no button usages remain silent.
    /// </summary>
    [Fact]
    public void Observe_NoButtonUsages_DoesNotEmitAxisOrStatusNoise()
    {
        var tracker = new RawInputButtonStateTracker();
        tracker.Observe("device-1", reportId: 0, []);

        var edges = tracker.Observe("device-1", reportId: 0, []);

        Assert.Empty(edges);
    }

    /// <summary>
    /// Verifies multiple button changes preserve changed counts so binding can reject them.
    /// </summary>
    [Fact]
    public void Observe_MultipleButtonPresses_ReportsChangedCounts()
    {
        var tracker = new RawInputButtonStateTracker();
        tracker.Observe("device-1", reportId: 0, []);

        var edges = tracker.Observe("device-1", reportId: 0, [4, 7]);

        Assert.Equal(2, edges.Count);
        Assert.All(edges, edge =>
        {
            Assert.True(edge.IsPressed);
            Assert.Equal(2, edge.PressedChangeCount);
            Assert.Equal(2, edge.ChangedButtonCount);
        });
    }

    /// <summary>
    /// Verifies separate report identifiers do not release each other's pressed buttons.
    /// </summary>
    [Fact]
    public void Observe_DifferentReports_KeepIndependentButtonState()
    {
        var tracker = new RawInputButtonStateTracker();
        tracker.Observe("device-1", reportId: 1, [4]);

        var edges = tracker.Observe("device-1", reportId: 2, []);

        Assert.Empty(edges);
    }
}
