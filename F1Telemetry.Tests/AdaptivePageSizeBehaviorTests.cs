using System.Windows;
using System.Windows.Controls;
using F1Telemetry.App.AttachedProperties;
using F1Telemetry.App.ViewModels;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies viewport-driven page sizing for WPF list hosts.
/// </summary>
public sealed class AdaptivePageSizeBehaviorTests
{
    /// <summary>
    /// Verifies that host height, chrome height, item height, and bounds reach the pager.
    /// </summary>
    [Fact]
    public void AdaptivePageSizeBehavior_ForwardsViewportSizingArguments()
    {
        WpfApplicationHelper.RunOnStaThread(() =>
        {
            var pager = new RecordingPageSizer();
            var host = new Border
            {
                Width = 240,
                Height = 320,
            };

            host.Measure(new Size(240, 320));
            host.Arrange(new Rect(0, 0, 240, 320));
            host.UpdateLayout();

            AdaptivePageSizeBehavior.SetEstimatedItemHeight(host, 50);
            AdaptivePageSizeBehavior.SetChromeHeight(host, 60);
            AdaptivePageSizeBehavior.SetMinPageSize(host, 2);
            AdaptivePageSizeBehavior.SetMaxPageSize(host, 8);
            AdaptivePageSizeBehavior.SetPagedCollection(host, pager);

            Assert.Equal(260, pager.ViewportHeight);
            Assert.Equal(50, pager.EstimatedItemHeight);
            Assert.Equal(2, pager.MinPageSize);
            Assert.Equal(8, pager.MaxPageSize);
        });
    }

    private sealed class RecordingPageSizer : IViewportPageSizer
    {
        public double ViewportHeight { get; private set; }

        public double EstimatedItemHeight { get; private set; }

        public int MinPageSize { get; private set; }

        public int MaxPageSize { get; private set; }

        public void SetPageSizeFromViewport(
            double viewportHeight,
            double estimatedItemHeight,
            int minPageSize = 1,
            int maxPageSize = 50)
        {
            ViewportHeight = viewportHeight;
            EstimatedItemHeight = estimatedItemHeight;
            MinPageSize = minPageSize;
            MaxPageSize = maxPageSize;
        }
    }
}
