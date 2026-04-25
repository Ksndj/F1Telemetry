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
using F1Telemetry.Core.Abstractions;
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
    /// Verifies that shell navigation items expose stable icon glyphs for the collapsed sidebar.
    /// </summary>
    [Fact]
    public void ShellNavigationItems_ExposeStableIconGlyphs()
    {
        var navigationItems = ShellNavigationItemViewModel.CreateDefaultItems();

        Assert.Equal(7, navigationItems.Count);
        Assert.All(navigationItems, item => Assert.False(string.IsNullOrWhiteSpace(item.IconGlyph)));
    }

    /// <summary>
    /// Verifies that the sidebar defaults to expanded and collapses to a tooltip-backed icon rail.
    /// </summary>
    [Fact]
    public void MainWindow_SidebarDefaultsExpandedAndCollapsesToIconRail()
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
                ApplyContentHostLayout(window);

                var sidebarColumn = Assert.IsType<ColumnDefinition>(window.FindName("SidebarColumnDefinition"));
                var navigationList = Assert.IsType<ListBox>(window.FindName("ShellNavigationList"));
                var productName = Assert.IsType<TextBlock>(window.FindName("SidebarProductName"));

                Assert.True(viewModel.IsSidebarExpanded);
                Assert.Equal(220d, sidebarColumn.Width.Value);
                Assert.Equal(GridUnitType.Pixel, sidebarColumn.Width.GridUnitType);
                Assert.Equal("F1 Telemetry", productName.Text);
                Assert.DoesNotContain(VersionInfo.CurrentVersion, productName.Text, StringComparison.Ordinal);
                Assert.Equal(ScrollBarVisibility.Disabled, ScrollViewer.GetHorizontalScrollBarVisibility(navigationList));
                AssertNavigationTextVisibility(navigationList, "实时概览", Visibility.Visible);
                AssertNavigationItemsHaveTooltips(navigationList);

                viewModel.ToggleSidebarCommand.Execute(null);
                ApplyContentHostLayout(window);

                Assert.False(viewModel.IsSidebarExpanded);
                Assert.Equal(80d, sidebarColumn.Width.Value);
                Assert.Equal(GridUnitType.Pixel, sidebarColumn.Width.GridUnitType);
                AssertNavigationTextVisibility(navigationList, "实时概览", Visibility.Collapsed);
                AssertNavigationItemsHaveTooltips(navigationList);
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
    [InlineData(typeof(AiTtsView))]
    [InlineData(typeof(SettingsView))]
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
                AssertContentHostShows<AiTtsView>(window, "AiTtsContentTemplate");

                viewModel.SelectedShellNavigationItem = navigationItems[6];
                AssertContentHostShows<SettingsView>(window, "SettingsContentTemplate");

                viewModel.SelectedShellNavigationItem = new ShellNavigationItemViewModel("laps", "Laps alias");
                AssertContentHostShows<LapHistoryView>(window, "LapHistoryContentTemplate");

                viewModel.SelectedShellNavigationItem = new ShellNavigationItemViewModel("logs", "Logs alias");
                AssertContentHostShows<LogsView>(window, "LogsContentTemplate");

                viewModel.SelectedShellNavigationItem = new ShellNavigationItemViewModel("legacy-dashboard", "Legacy dashboard");
                AssertContentHostShows<LegacyDashboardView>(window, "LegacyDashboardContentTemplate");

                viewModel.SelectedShellNavigationItem = new ShellNavigationItemViewModel("future-page", "Future page");
                AssertContentHostUsesPlaceholder(window);

                Assert.Equal(7, ((ListBox)window.FindName("ShellNavigationList")).Items.Count);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// Verifies that the shell title is fed by the dynamic application version text.
    /// </summary>
    [Fact]
    public void MainWindow_TitleUsesDynamicApplicationVersion()
    {
        RunOnStaThread(() =>
        {
            var viewModel = new ShellNavigationTestViewModel(ShellNavigationItemViewModel.CreateDefaultItems());
            var window = new MainWindow
            {
                DataContext = viewModel
            };

            try
            {
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
                window.UpdateLayout();

                var titleText = Assert.IsType<TextBlock>(window.FindName("ShellTitleText"));
                Assert.Equal(viewModel.AppTitleText, window.Title);
                Assert.Equal(viewModel.AppTitleText, titleText.Text);
                Assert.Contains(VersionInfo.CurrentVersion, titleText.Text, StringComparison.Ordinal);
                Assert.NotEqual("F1 25 遥测软件 V1", titleText.Text);
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
            ToggleSidebarCommand = new RelayCommand(ToggleSidebar);
            SelectedShellNavigationItem = shellNavigationItems[0];
        }

        public IReadOnlyList<ShellNavigationItemViewModel> ShellNavigationItems { get; }

        public event PropertyChangedEventHandler? PropertyChanged;

        public RelayCommand ToggleSidebarCommand { get; }

        public bool IsSidebarExpanded { get; private set; } = true;

        public GridLength SidebarColumnWidth => new(IsSidebarExpanded ? 220d : 80d);

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
                IsAiTtsSelected = string.Equals(value.Key, "ai-tts", StringComparison.Ordinal);
                IsSettingsSelected = string.Equals(value.Key, "settings", StringComparison.Ordinal);
                IsLegacyDashboardSelected = string.Equals(value.Key, "legacy-dashboard", StringComparison.Ordinal);
                IsPlaceholderNavigationSelected =
                    !IsOverviewSelected &&
                    !IsChartsSelected &&
                    !IsLapHistorySelected &&
                    !IsOpponentsSelected &&
                    !IsLogsSelected &&
                    !IsAiTtsSelected &&
                    !IsSettingsSelected &&
                    !IsLegacyDashboardSelected;
                SelectedShellNavigationTitle = value.Name;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedShellNavigationItem)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsOverviewSelected)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChartsSelected)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLapHistorySelected)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsOpponentsSelected)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLogsSelected)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsAiTtsSelected)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSettingsSelected)));
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

        public bool IsAiTtsSelected { get; private set; }

        public bool IsSettingsSelected { get; private set; }

        public bool IsPlaceholderNavigationSelected { get; private set; }

        public bool IsLegacyDashboardSelected { get; private set; }

        public string AppTitleText => $"F1 Telemetry {VersionInfo.CurrentVersion}";

        public string SelectedShellNavigationTitle { get; private set; } = string.Empty;

        private ShellNavigationItemViewModel _selectedShellNavigationItem = null!;

        private void ToggleSidebar()
        {
            IsSidebarExpanded = !IsSidebarExpanded;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSidebarExpanded)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SidebarColumnWidth)));
        }
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

    private static void AssertNavigationTextVisibility(ListBox navigationList, string text, Visibility expectedVisibility)
    {
        navigationList.UpdateLayout();
        var container = Assert.IsAssignableFrom<ListBoxItem>(
            navigationList.ItemContainerGenerator.ContainerFromIndex(0));
        var textBlock = FindDescendant<TextBlock>(container, block => string.Equals(block.Text, text, StringComparison.Ordinal));

        Assert.NotNull(textBlock);
        Assert.Equal(expectedVisibility, textBlock.Visibility);
    }

    private static void AssertNavigationItemsHaveTooltips(ListBox navigationList)
    {
        navigationList.UpdateLayout();
        for (var i = 0; i < navigationList.Items.Count; i++)
        {
            var item = Assert.IsType<ShellNavigationItemViewModel>(navigationList.Items[i]);
            var container = Assert.IsAssignableFrom<ListBoxItem>(
                navigationList.ItemContainerGenerator.ContainerFromIndex(i));
            var elementWithTooltip = FindDescendant<FrameworkElement>(
                container,
                element => string.Equals(element.ToolTip as string, item.Name, StringComparison.Ordinal));

            Assert.NotNull(elementWithTooltip);
        }
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
        return value is OverviewView or ChartsView or LapHistoryView or OpponentsView or LogsView or AiTtsView or SettingsView or LegacyDashboardView;
    }

    private static T? FindDescendant<T>(DependencyObject root)
        where T : DependencyObject
    {
        return FindDescendant<T>(root, _ => true);
    }

    private static T? FindDescendant<T>(DependencyObject root, Predicate<T> predicate)
        where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T typedChild && predicate(typedChild))
            {
                return typedChild;
            }

            var match = FindDescendant<T>(child, predicate);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }
}
