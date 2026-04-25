using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using F1Telemetry.App;
using F1Telemetry.App.ViewModels;
using F1Telemetry.App.Windowing;
using F1Telemetry.App.Views;
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

    /// <summary>
    /// Verifies that the overview page can load and owns its own scroll surface.
    /// </summary>
    [Fact]
    public void OverviewView_LoadsWithScrollViewer()
    {
        RunOnStaThread(() =>
        {
            var view = new OverviewView();
            try
            {
                view.UpdateLayout();

                Assert.IsType<ScrollViewer>(view.Content);
            }
            finally
            {
                view.Content = null;
            }
        });
    }

    /// <summary>
    /// Verifies that each detail page can load and owns its own scroll surface.
    /// </summary>
    [Theory]
    [InlineData(typeof(ChartsView))]
    [InlineData(typeof(LapHistoryView))]
    [InlineData(typeof(OpponentsView))]
    [InlineData(typeof(LogsView))]
    public void DetailViews_LoadWithScrollViewer(Type viewType)
    {
        RunOnStaThread(() =>
        {
            var view = Assert.IsAssignableFrom<UserControl>(Activator.CreateInstance(viewType));
            try
            {
                view.UpdateLayout();

                Assert.IsType<ScrollViewer>(view.Content);
            }
            finally
            {
                view.Content = null;
            }
        });
    }

    /// <summary>
    /// Verifies that the content host switches between one active shell page at a time.
    /// </summary>
    [Fact]
    public void MainWindow_ContentHostSwitchesBetweenDetailPagesPlaceholdersAndLegacy()
    {
        RunOnStaThread(() =>
        {
            var navigationItems = ShellNavigationItemViewModel.CreateDefaultItems();
            var viewModel = new ShellNavigationTestViewModel(navigationItems);
            var window = new MainWindow
            {
                DataContext = viewModel
            };

            try
            {
                window.Show();

                Assert.Equal(7, navigationItems.Count);
                AssertContentHostShows<OverviewView>(window, "OverviewContentTemplate");

                viewModel.SelectedShellNavigationItem = navigationItems[1];
                AssertContentHostShows<ChartsView>(window, "ChartsContentTemplate");

                viewModel.SelectedShellNavigationItem = navigationItems[2];
                AssertContentHostShows<LapHistoryView>(window, "LapHistoryContentTemplate");

                viewModel.SelectedShellNavigationItem = navigationItems[3];
                AssertContentHostShows<OpponentsView>(window, "OpponentsContentTemplate");

                viewModel.SelectedShellNavigationItem = navigationItems[4];
                AssertContentHostShows<LogsView>(window, "LogsContentTemplate");

                viewModel.SelectedShellNavigationItem = navigationItems[5];
                AssertContentHostUsesPlaceholder(window);

                viewModel.SelectedShellNavigationItem = navigationItems[6];
                AssertContentHostUsesPlaceholder(window);

                viewModel.SelectedShellNavigationItem = new ShellNavigationItemViewModel("laps", "Laps alias");
                AssertContentHostShows<LapHistoryView>(window, "LapHistoryContentTemplate");

                viewModel.SelectedShellNavigationItem = new ShellNavigationItemViewModel("logs", "Logs alias");
                AssertContentHostShows<LogsView>(window, "LogsContentTemplate");

                viewModel.SelectedShellNavigationItem = new ShellNavigationItemViewModel("legacy-dashboard", "Legacy dashboard");
                AssertContentHostShows<LegacyDashboardView>(window, "LegacyDashboardContentTemplate");

                Assert.Equal(7, ((ListBox)window.FindName("ShellNavigationList")).Items.Count);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// Verifies that the legacy dashboard view can still load while migration continues.
    /// </summary>
    [Fact]
    public void LegacyDashboardView_LoadsForMigrationFallback()
    {
        RunOnStaThread(() =>
        {
            var view = new LegacyDashboardView();
            try
            {
                view.UpdateLayout();

                Assert.NotNull(view.Content);
            }
            finally
            {
                view.Content = null;
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

    public sealed class ShellNavigationTestViewModel : INotifyPropertyChanged
    {
        public ShellNavigationTestViewModel(IReadOnlyList<ShellNavigationItemViewModel> shellNavigationItems)
        {
            ShellNavigationItems = shellNavigationItems;
            SelectedShellNavigationItem = shellNavigationItems[0];
        }

        public IReadOnlyList<ShellNavigationItemViewModel> ShellNavigationItems { get; }

        public event PropertyChangedEventHandler? PropertyChanged;

        public ShellNavigationItemViewModel SelectedShellNavigationItem
        {
            get => _selectedShellNavigationItem;
            set
            {
                if (ReferenceEquals(_selectedShellNavigationItem, value))
                {
                    return;
                }

                _selectedShellNavigationItem = value;
                IsOverviewSelected = string.Equals(value.Key, "overview", StringComparison.Ordinal);
                IsChartsSelected = string.Equals(value.Key, "charts", StringComparison.Ordinal);
                IsLapHistorySelected =
                    string.Equals(value.Key, "lap-history", StringComparison.Ordinal) ||
                    string.Equals(value.Key, "laps", StringComparison.Ordinal);
                IsOpponentsSelected = string.Equals(value.Key, "opponents", StringComparison.Ordinal);
                IsLogsSelected =
                    string.Equals(value.Key, "event-logs", StringComparison.Ordinal) ||
                    string.Equals(value.Key, "logs", StringComparison.Ordinal);
                IsLegacyDashboardSelected = string.Equals(value.Key, "legacy-dashboard", StringComparison.Ordinal);
                IsPlaceholderNavigationSelected =
                    !IsOverviewSelected &&
                    !IsChartsSelected &&
                    !IsLapHistorySelected &&
                    !IsOpponentsSelected &&
                    !IsLogsSelected &&
                    !IsLegacyDashboardSelected;
                SelectedShellNavigationTitle = value.Name;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedShellNavigationItem)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsOverviewSelected)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChartsSelected)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLapHistorySelected)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsOpponentsSelected)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLogsSelected)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLegacyDashboardSelected)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPlaceholderNavigationSelected)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedShellNavigationTitle)));
            }
        }

        public bool IsOverviewSelected { get; private set; }

        public bool IsChartsSelected { get; private set; }

        public bool IsLapHistorySelected { get; private set; }

        public bool IsOpponentsSelected { get; private set; }

        public bool IsLogsSelected { get; private set; }

        public bool IsPlaceholderNavigationSelected { get; private set; }

        public bool IsLegacyDashboardSelected { get; private set; }

        public string SelectedShellNavigationTitle { get; private set; } = string.Empty;

        private ShellNavigationItemViewModel _selectedShellNavigationItem = null!;
    }

    private static void AssertVisible(object element)
    {
        Assert.Equal(Visibility.Visible, Assert.IsAssignableFrom<FrameworkElement>(element).Visibility);
    }

    private static void AssertCollapsed(object element)
    {
        Assert.Equal(Visibility.Collapsed, Assert.IsAssignableFrom<FrameworkElement>(element).Visibility);
    }

    private static void AssertContentHostShows<TPage>(MainWindow window, string templateKey)
        where TPage : UserControl
    {
        var host = ApplyContentHostLayout(window);

        Assert.Same(window.FindResource(templateKey), host.ContentTemplate);
        Assert.NotNull(FindDescendant<TPage>(host));
        Assert.Equal(1, CountActiveShellPages(host));
    }

    private static void AssertContentHostUsesPlaceholder(MainWindow window)
    {
        var host = ApplyContentHostLayout(window);

        Assert.Same(window.FindResource("PlaceholderContentTemplate"), host.ContentTemplate);
        Assert.Equal(0, CountActiveShellPages(host));
    }

    private static ContentControl ApplyContentHostLayout(MainWindow window)
    {
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
        window.UpdateLayout();

        var host = Assert.IsType<ContentControl>(window.FindName("ContentHost"));
        host.ApplyTemplate();
        host.UpdateLayout();
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
        host.UpdateLayout();

        return host;
    }

    private static int CountActiveShellPages(DependencyObject root)
    {
        var count = IsShellPage(root) ? 1 : 0;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            count += CountActiveShellPages(VisualTreeHelper.GetChild(root, i));
        }

        return count;
    }

    private static bool IsShellPage(object value)
    {
        return value is OverviewView or ChartsView or LapHistoryView or OpponentsView or LogsView or LegacyDashboardView;
    }

    private static T? FindDescendant<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T typedChild)
            {
                return typedChild;
            }

            var match = FindDescendant<T>(child);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }
}
