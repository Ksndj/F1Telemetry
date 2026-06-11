using System.Windows;
using System.Windows.Controls;
using F1Telemetry.App.AttachedProperties;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies breakpoint-driven responsive state selection.
/// </summary>
public sealed class ResponsiveLayoutBehaviorTests
{
    /// <summary>
    /// Verifies that the behavior maps element width into narrow, medium, and wide states.
    /// </summary>
    [Fact]
    public void ResponsiveLayoutBehavior_MapsWidthToNamedStates()
    {
        WpfApplicationHelper.RunOnStaThread(() =>
        {
            var host = new Border { Height = 100 };
            ResponsiveLayoutBehavior.SetBreakpoints(host, "1000,1300");

            Arrange(host, 900);
            Assert.Equal("Narrow", ResponsiveLayoutBehavior.GetCurrentState(host));

            Arrange(host, 1100);
            Assert.Equal("Medium", ResponsiveLayoutBehavior.GetCurrentState(host));

            Arrange(host, 1400);
            Assert.Equal("Wide", ResponsiveLayoutBehavior.GetCurrentState(host));
        });
    }

    private static void Arrange(FrameworkElement element, double width)
    {
        element.Width = width;
        element.Measure(new Size(width, 100));
        element.Arrange(new Rect(0, 0, width, 100));
        element.UpdateLayout();
    }
}
