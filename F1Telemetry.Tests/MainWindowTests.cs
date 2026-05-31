using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Xml.Linq;
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
    private const int ExpectedShellNavigationItemCount = 10;

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
                Assert.True(window.MinWidth >= 1000d, "MainWindow should prevent layouts below the supported shell width.");
                Assert.True(window.MinHeight >= 650d, "MainWindow should prevent layouts below the supported shell height.");
                Assert.True(double.IsPositiveInfinity(window.MaxWidth), "MainWindow should not cap maximum width.");
                Assert.True(double.IsPositiveInfinity(window.MaxHeight), "MainWindow should not cap maximum height.");
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
        Assert.Equal(new Thickness(0), maximizedProfile.ContentMargin);
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
                Assert.NotNull(window.FindName("ContentHostScrollViewer"));

                var navigationList = Assert.IsType<ListBox>(window.FindName("ShellNavigationList"));
                Assert.Equal(ExpectedShellNavigationItemCount, navigationList.Items.Count);
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

        Assert.Equal(ExpectedShellNavigationItemCount, navigationItems.Count);
        Assert.All(navigationItems, item => Assert.False(string.IsNullOrWhiteSpace(item.IconGlyph)));
        Assert.Contains(navigationItems, item => item.Key == "corner-analysis" && item.Name == "弯角分析");
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
                Assert.Equal(ScrollBarVisibility.Auto, ScrollViewer.GetVerticalScrollBarVisibility(navigationList));
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
    /// Verifies that scroll-backed detail pages can load and own their scroll surface.
    /// </summary>
    [Theory]
    [InlineData(typeof(ChartsView))]
    [InlineData(typeof(LapHistoryView))]
    [InlineData(typeof(OpponentsView))]
    [InlineData(typeof(LogsView))]
    [InlineData(typeof(AiTtsView))]
    [InlineData(typeof(SettingsView))]
    public void ScrollBackedDetailViews_LoadWithScrollViewer(Type viewType)
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
    /// Verifies history-heavy views expose paging controls and deletion commands.
    /// </summary>
    [Fact]
    public void HistoryViews_DefinePagingAndDeleteEntryPoints()
    {
        var root = FindRepositoryRoot();
        var lapHistoryXaml = File.ReadAllText(Path.Combine(root, "F1Telemetry.App", "Views", "LapHistoryView.xaml"));
        var postRaceReviewXaml = File.ReadAllText(Path.Combine(root, "F1Telemetry.App", "Views", "PostRaceReviewView.xaml"));
        var sessionComparisonXaml = File.ReadAllText(Path.Combine(root, "F1Telemetry.App", "Views", "SessionComparisonView.xaml"));

        AssertPagingBindings(lapHistoryXaml, "HistoryBrowser.HistorySessionPages");
        AssertPagingBindings(lapHistoryXaml, "HistoryBrowser.HistoryLapPages");
        Assert.Contains("HistoryBrowser.DeleteSessionCommand", lapHistoryXaml, StringComparison.Ordinal);
        AssertHistoryLapHeader(lapHistoryXaml);

        AssertPagingBindings(postRaceReviewXaml, "PostRaceReview.HistoryBrowser.HistorySessionPages");
        AssertPagingBindings(postRaceReviewXaml, "PostRaceReview.EventTimelinePages");
        AssertPagingBindings(postRaceReviewXaml, "PostRaceReview.AiReportPages");
        Assert.Contains("PostRaceReview.HistoryBrowser.DeleteSessionCommand", postRaceReviewXaml, StringComparison.Ordinal);
        AssertHorizontalScrollBarVisibilityDisabled(postRaceReviewXaml);

        AssertPagingBindings(sessionComparisonXaml, "SessionComparison.CandidateSessionPages");
        Assert.Contains("SessionComparison.DeleteSessionCommand", sessionComparisonXaml, StringComparison.Ordinal);
        AssertHorizontalScrollBarVisibilityDisabled(sessionComparisonXaml);
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

                Assert.Equal(ExpectedShellNavigationItemCount, navigationItems.Count);
                AssertContentHostShows<OverviewView>(window, "OverviewContentTemplate");

                viewModel.SelectedShellNavigationItem = navigationItems[1];
                AssertContentHostShows<ChartsView>(window, "ChartsContentTemplate");

                viewModel.SelectedShellNavigationItem = navigationItems[2];
                AssertContentHostShows<LapHistoryView>(window, "LapHistoryContentTemplate");

                viewModel.SelectedShellNavigationItem = navigationItems[3];
                AssertContentHostShows<PostRaceReviewView>(window, "PostRaceReviewContentTemplate");

                viewModel.SelectedShellNavigationItem = navigationItems[4];
                AssertContentHostShows<SessionComparisonView>(window, "SessionComparisonContentTemplate");

                viewModel.SelectedShellNavigationItem = navigationItems[5];
                AssertContentHostShows<CornerAnalysisView>(window, "CornerAnalysisContentTemplate");

                viewModel.SelectedShellNavigationItem = navigationItems[6];
                AssertContentHostShows<OpponentsView>(window, "OpponentsContentTemplate");

                viewModel.SelectedShellNavigationItem = navigationItems[7];
                AssertContentHostShows<LogsView>(window, "LogsContentTemplate");

                viewModel.SelectedShellNavigationItem = navigationItems[8];
                AssertContentHostShows<AiTtsView>(window, "AiTtsContentTemplate");

                viewModel.SelectedShellNavigationItem = navigationItems[9];
                AssertContentHostShows<SettingsView>(window, "SettingsContentTemplate");

                viewModel.SelectedShellNavigationItem = new ShellNavigationItemViewModel("laps", "Laps alias");
                AssertContentHostShows<LapHistoryView>(window, "LapHistoryContentTemplate");

                viewModel.SelectedShellNavigationItem = new ShellNavigationItemViewModel("logs", "Logs alias");
                AssertContentHostShows<LogsView>(window, "LogsContentTemplate");

                viewModel.SelectedShellNavigationItem = new ShellNavigationItemViewModel("legacy-dashboard", "Legacy dashboard");
                AssertContentHostShows<LegacyDashboardView>(window, "LegacyDashboardContentTemplate");

                viewModel.SelectedShellNavigationItem = new ShellNavigationItemViewModel("future-page", "Future page");
                AssertContentHostUsesPlaceholder(window);

                Assert.Equal(ExpectedShellNavigationItemCount, ((ListBox)window.FindName("ShellNavigationList")).Items.Count);
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
    /// Verifies the top shell status area uses the compact telemetry dashboard structure.
    /// </summary>
    [Fact]
    public void MainWindow_UsesCompactTelemetryStatusBarLayout()
    {
        var root = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "F1Telemetry.App", "MainWindow.xaml"));

        Assert.Contains("ShellTelemetryCardStyle", xaml, StringComparison.Ordinal);
        Assert.Contains("ShellTelemetryLabelStyle", xaml, StringComparison.Ordinal);
        Assert.Contains("ShellTelemetryValueStyle", xaml, StringComparison.Ordinal);
        Assert.Contains("<Setter Property=\"MinHeight\" Value=\"42\" />", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"TopStatusWrapPanel\"", xaml, StringComparison.Ordinal);
        Assert.Contains("连接状态", xaml, StringComparison.Ordinal);
        Assert.Contains("赛道", xaml, StringComparison.Ordinal);
        Assert.Contains("赛制", xaml, StringComparison.Ordinal);
        Assert.Contains("比赛进度", xaml, StringComparison.Ordinal);
        Assert.Contains("天气", xaml, StringComparison.Ordinal);
        Assert.Contains("UDP PPS", xaml, StringComparison.Ordinal);
        Assert.Contains("监听端口", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SessionTypeStatusChip\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ToolTip=\"{Binding SessionTypeTooltipText}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"WeatherStatusChip\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ToolTip=\"{Binding WeatherTooltipText}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("MaxWidth=\"230\"", xaml, StringComparison.Ordinal);
        Assert.Contains("TextTrimming=\"CharacterEllipsis\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"UdpPortStatusChip\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Width=\"104\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Width=\"96\"", xaml, StringComparison.Ordinal);
        Assert.Contains("StartListeningCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("StopListeningCommand", xaml, StringComparison.Ordinal);

        var sessionTypeChip = ExtractNamedBorderBlock(xaml, "SessionTypeStatusChip");
        Assert.Contains("ToolTip=\"{Binding SessionTypeTooltipText}\"", sessionTypeChip, StringComparison.Ordinal);
        Assert.Contains("Text=\"赛制\"", sessionTypeChip, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"连接状态\"", sessionTypeChip, StringComparison.Ordinal);

        var udpPortChip = ExtractNamedBorderBlock(xaml, "UdpPortStatusChip");
        Assert.DoesNotContain("<WrapPanel", udpPortChip, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxWidth=", udpPortChip, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that shell content hosting has a single vertical scroll strategy.
    /// </summary>
    [Fact]
    public void MainWindow_ContentHostUsesVerticalScrollContainer()
    {
        RunOnStaThread(() =>
        {
            var window = new MainWindow();
            try
            {
                window.UpdateLayout();

                var scrollViewer = Assert.IsType<ScrollViewer>(window.FindName("ContentHostScrollViewer"));
                Assert.Equal(ScrollBarVisibility.Auto, scrollViewer.VerticalScrollBarVisibility);
                Assert.Equal(ScrollBarVisibility.Disabled, scrollViewer.HorizontalScrollBarVisibility);
                Assert.IsType<ContentControl>(window.FindName("ContentHost"));
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// Verifies that the shell forwards wheel input to the page scroll host.
    /// </summary>
    [Fact]
    public void MainWindow_ContentHostRoutesMouseWheelToPageScroll()
    {
        var root = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "F1Telemetry.App", "MainWindow.xaml"));
        var codeBehind = File.ReadAllText(Path.Combine(root, "F1Telemetry.App", "MainWindow.xaml.cs"));

        Assert.Contains("PreviewMouseWheel=\"ContentHostScrollViewer_PreviewMouseWheel\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta)", codeBehind, StringComparison.Ordinal);
        Assert.Contains("CanScrollVertically", codeBehind, StringComparison.Ordinal);
        Assert.Contains("FindScrollableChild", codeBehind, StringComparison.Ordinal);
        Assert.Contains("e.Handled = true", codeBehind, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that narrow shell widths trigger the icon-only sidebar path.
    /// </summary>
    [Fact]
    public void MainWindow_DefinesResponsiveSidebarAutoCollapsePath()
    {
        var root = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "F1Telemetry.App", "MainWindow.xaml"));
        var codeBehind = File.ReadAllText(Path.Combine(root, "F1Telemetry.App", "MainWindow.xaml.cs"));
        var viewModel = File.ReadAllText(Path.Combine(root, "F1Telemetry.App", "ViewModels", "DashboardViewModel.cs"));

        Assert.Contains("SizeChanged=\"Window_SizeChanged\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ApplyShellViewportWidth(e.NewSize.Width)", codeBehind, StringComparison.Ordinal);
        Assert.Contains("CompactSidebarAutoCollapseWidth = 1180d", viewModel, StringComparison.Ordinal);
        Assert.Contains("ExpandedSidebarAutoRestoreWidth = 1280d", viewModel, StringComparison.Ordinal);
        Assert.Contains("_sidebarCollapsedByViewport", viewModel, StringComparison.Ordinal);
        Assert.Contains("IsSidebarExpanded = false", viewModel, StringComparison.Ordinal);
        Assert.Contains("IsSidebarExpanded = true", viewModel, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that application resources provide the unified rounded scrollbar theme.
    /// </summary>
    [Fact]
    public void AppResources_DefineRoundedGlobalScrollBarStyle()
    {
        var root = FindRepositoryRoot();
        var appXaml = File.ReadAllText(Path.Combine(root, "F1Telemetry.App", "App.xaml"));
        var scrollbarXaml = File.ReadAllText(Path.Combine(root, "F1Telemetry.App", "Styles", "ScrollBarStyles.xaml"));

        Assert.Contains("Styles/ScrollBarStyles.xaml", appXaml, StringComparison.Ordinal);
        Assert.Contains("TargetType=\"{x:Type ScrollBar}\"", scrollbarXaml, StringComparison.Ordinal);
        Assert.Contains("AppVerticalScrollBarTemplate", scrollbarXaml, StringComparison.Ordinal);
        Assert.Contains("AppHorizontalScrollBarTemplate", scrollbarXaml, StringComparison.Ordinal);
        Assert.Contains("Orientation\" Value=\"Horizontal\"", scrollbarXaml, StringComparison.Ordinal);
        Assert.Contains("Orientation\" Value=\"Vertical\"", scrollbarXaml, StringComparison.Ordinal);
        Assert.Contains("CornerRadius=\"6\"", scrollbarXaml, StringComparison.Ordinal);
        Assert.Contains("AppScrollBarTrackBrush", scrollbarXaml, StringComparison.Ordinal);
        Assert.Contains("AppScrollBarThumbBrush", scrollbarXaml, StringComparison.Ordinal);
        Assert.Contains("AppScrollBarThumbHoverBrush", scrollbarXaml, StringComparison.Ordinal);
        Assert.Contains("AppScrollBarThumbPressedBrush", scrollbarXaml, StringComparison.Ordinal);
        Assert.Contains("#4F7FA6", scrollbarXaml, StringComparison.Ordinal);
        Assert.Contains("#66A7D8", scrollbarXaml, StringComparison.Ordinal);
        Assert.Contains("#7EC7F2", scrollbarXaml, StringComparison.Ordinal);
        Assert.Contains("MinHeight\" Value=\"36\"", scrollbarXaml, StringComparison.Ordinal);
        Assert.Contains("MinWidth\" Value=\"36\"", scrollbarXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("#FFFFFF", scrollbarXaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SystemColors", scrollbarXaml, StringComparison.OrdinalIgnoreCase);
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

    private static void AssertPagingBindings(string xaml, string bindingPath)
    {
        Assert.Contains($"{bindingPath}.Items", xaml, StringComparison.Ordinal);
        Assert.Contains($"{bindingPath}.PreviousPageCommand", xaml, StringComparison.Ordinal);
        Assert.Contains($"{bindingPath}.PageText", xaml, StringComparison.Ordinal);
        Assert.Contains($"{bindingPath}.NextPageCommand", xaml, StringComparison.Ordinal);
    }

    private static void AssertHistoryLapHeader(string xaml)
    {
        foreach (var header in new[] { "圈号", "圈速", "分段", "均速", "燃油", "ERS", "磨损", "状态", "轮胎", "进站" })
        {
            Assert.Contains($"Text=\"{header}\"", xaml, StringComparison.Ordinal);
        }
    }

    private static void AssertHorizontalScrollBarVisibilityDisabled(string xaml)
    {
        var document = XDocument.Parse(xaml);
        var horizontalScrollAttributes = document.Descendants()
            .SelectMany(element => element.Attributes())
            .Where(attribute => attribute.Name.LocalName.EndsWith("HorizontalScrollBarVisibility", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(horizontalScrollAttributes);
        Assert.All(horizontalScrollAttributes, attribute => Assert.Equal("Disabled", attribute.Value));
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
                IsPostRaceReviewSelected = string.Equals(value.Key, "post-race-review", StringComparison.Ordinal);
                IsSessionComparisonSelected = string.Equals(value.Key, "session-comparison", StringComparison.Ordinal);
                IsCornerAnalysisSelected = string.Equals(value.Key, "corner-analysis", StringComparison.Ordinal);
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
                    !IsPostRaceReviewSelected &&
                    !IsSessionComparisonSelected &&
                    !IsCornerAnalysisSelected &&
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
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPostRaceReviewSelected)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSessionComparisonSelected)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCornerAnalysisSelected)));
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

        public bool IsPostRaceReviewSelected { get; private set; }

        public bool IsSessionComparisonSelected { get; private set; }

        public bool IsCornerAnalysisSelected { get; private set; }

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
            navigationList.ScrollIntoView(item);
            navigationList.UpdateLayout();
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
        return value is OverviewView or ChartsView or LapHistoryView or PostRaceReviewView or SessionComparisonView or CornerAnalysisView or OpponentsView or LogsView or AiTtsView or SettingsView or LegacyDashboardView;
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

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Directory.Build.props")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root.");
    }

    private static string ExtractNamedBorderBlock(string xaml, string name)
    {
        var start = xaml.IndexOf($"x:Name=\"{name}\"", StringComparison.Ordinal);
        Assert.True(start >= 0, $"Expected to find {name}.");

        var end = xaml.IndexOf("</Border>", start, StringComparison.Ordinal);
        Assert.True(end > start, $"Expected to find the closing Border for {name}.");

        return xaml[start..end];
    }
}
