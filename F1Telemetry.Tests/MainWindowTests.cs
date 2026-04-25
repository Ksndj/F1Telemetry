using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using F1Telemetry.App;
using F1Telemetry.App.ViewModels;
using F1Telemetry.App.Windowing;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies main-window chrome defaults and state-transition presentation settings.
/// </summary>
public sealed class MainWindowTests
{
    /// <summary>
    /// Verifies that the main window enables standard resize and taskbar behavior.
    /// </summary>
    [Fact]
    public void MainWindow_UsesResizableChromeAndTaskbarVisibility()
    {
        RunOnStaThread(() =>
        {
            var window = new MainWindow();
            try
            {
                Assert.Equal(WindowState.Normal, window.WindowState);
                Assert.Equal(ResizeMode.CanResizeWithGrip, window.ResizeMode);
                Assert.True(window.ShowInTaskbar);
                Assert.Equal(1920d, window.MaxWidth);
                Assert.Equal(1080d, window.MaxHeight);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// Verifies that maximize and restore transitions expose the expected feedback and layout tuning.
    /// </summary>
    [Fact]
    public void Create_MaximizeAndRestoreProfiles_ReturnExpectedFeedback()
    {
        var maximizedProfile = WindowStateTransitionPlanner.Create(WindowState.Maximized);
        var normalProfile = WindowStateTransitionPlanner.Create(WindowState.Normal);

        Assert.True(maximizedProfile.ShowFeedback);
        Assert.Equal("窗口已最大化", maximizedProfile.FeedbackMessage);
        Assert.Equal(new Thickness(8), maximizedProfile.ContentMargin);
        Assert.True(maximizedProfile.InitialScale < 1d);

        Assert.True(normalProfile.ShowFeedback);
        Assert.Equal("窗口已还原", normalProfile.FeedbackMessage);
        Assert.Equal(new Thickness(0), normalProfile.ContentMargin);
        Assert.True(normalProfile.InitialScale < 1d);
    }

    /// <summary>
    /// Verifies that the shell exposes the primary regions and default sidebar selection.
    /// </summary>
    [Fact]
    public void MainWindow_DefinesShellRegionsAndDefaultNavigationSelection()
    {
        RunOnStaThread(() =>
        {
            var navigationItems = ShellNavigationItemViewModel.CreateDefaultItems();
            var window = new MainWindow
            {
                DataContext = new ShellNavigationTestViewModel(navigationItems)
            };

            try
            {
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
                window.UpdateLayout();

                Assert.NotNull(window.FindName("Sidebar"));
                Assert.NotNull(window.FindName("TopStatusBar"));
                Assert.NotNull(window.FindName("ContentHost"));

                var navigationList = Assert.IsType<ListBox>(window.FindName("ShellNavigationList"));
                Assert.Equal(7, navigationList.Items.Count);
                Assert.Same(navigationItems[0], navigationList.SelectedItem);
                Assert.Equal("实时概览", navigationItems[0].Name);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? capturedException = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                capturedException = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (capturedException is not null)
        {
            ExceptionDispatchInfo.Capture(capturedException).Throw();
        }
    }

    public sealed class ShellNavigationTestViewModel
    {
        public ShellNavigationTestViewModel(IReadOnlyList<ShellNavigationItemViewModel> shellNavigationItems)
        {
            ShellNavigationItems = shellNavigationItems;
            SelectedShellNavigationItem = shellNavigationItems[0];
        }

        public IReadOnlyList<ShellNavigationItemViewModel> ShellNavigationItems { get; }

        public ShellNavigationItemViewModel SelectedShellNavigationItem { get; set; }
    }
}
