using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using F1Telemetry.App.ViewModels;
using F1Telemetry.App.Windowing;

namespace F1Telemetry.App;

/// <summary>
/// Hosts the real-time telemetry dashboard and applies lightweight window-state transitions.
/// </summary>
public partial class MainWindow : Window
{
    private const int WmHotkey = 0x0312;
    private const int VoiceAiHotkeyId = 0x4641;
    private Storyboard? _windowStateToastStoryboard;
    private HwndSource? _hotkeySource;
    private DashboardViewModel? _hotkeyDashboard;
    private bool _isVoiceAiHotkeyRegistered;
    private bool _shutdownStarted;
    private bool _shutdownCompleted;

    /// <summary>
    /// Initializes the main window shell.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += Window_DataContextChanged;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyWindowStateVisuals(WindowState, animate: false);
        InitializeVoiceAiHotkeyHook();
        RefreshVoiceAiHotkeyRegistration();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (DataContext is DashboardViewModel dashboard && dashboard.TryHandleVoiceAiHotkey(key))
        {
            e.Handled = true;
        }
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        _ = Dispatcher.BeginInvoke(
            DispatcherPriority.Render,
            new Action(() => ApplyWindowStateVisuals(WindowState, animate: WindowState != WindowState.Minimized)));
    }

    private async void Window_Closing(object sender, CancelEventArgs e)
    {
        if (_shutdownCompleted)
        {
            return;
        }

        if (DataContext is not IApplicationShutdownCoordinator shutdownCoordinator)
        {
            _shutdownCompleted = true;
            return;
        }

        e.Cancel = true;
        if (_shutdownStarted)
        {
            return;
        }

        _shutdownStarted = true;
        try
        {
            await shutdownCoordinator.ShutdownAsync();
        }
        catch
        {
        }
        finally
        {
            _shutdownCompleted = true;
            CloseAfterShutdown();
        }
    }

    private void Window_Closed(object sender, EventArgs e)
    {
        ReleaseVoiceAiHotkeyHook();
        if (_shutdownCompleted)
        {
            Application.Current?.Shutdown();
        }
    }

    private void Window_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_hotkeyDashboard is not null)
        {
            _hotkeyDashboard.PropertyChanged -= Dashboard_PropertyChanged;
        }

        _hotkeyDashboard = e.NewValue as DashboardViewModel;
        if (_hotkeyDashboard is not null)
        {
            _hotkeyDashboard.PropertyChanged += Dashboard_PropertyChanged;
        }

        RefreshVoiceAiHotkeyRegistration();
    }

    private void Dashboard_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(DashboardViewModel.VoiceAiEnabled), StringComparison.Ordinal) ||
            string.Equals(e.PropertyName, nameof(DashboardViewModel.VoiceAiHotkey), StringComparison.Ordinal))
        {
            RefreshVoiceAiHotkeyRegistration();
        }
    }

    private void InitializeVoiceAiHotkeyHook()
    {
        if (_hotkeySource is not null)
        {
            return;
        }

        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        _hotkeySource = HwndSource.FromHwnd(handle);
        _hotkeySource?.AddHook(WndProc);
    }

    private void ReleaseVoiceAiHotkeyHook()
    {
        UnregisterVoiceAiHotkey();
        if (_hotkeyDashboard is not null)
        {
            _hotkeyDashboard.PropertyChanged -= Dashboard_PropertyChanged;
            _hotkeyDashboard = null;
        }

        _hotkeySource?.RemoveHook(WndProc);
        _hotkeySource = null;
    }

    private void RefreshVoiceAiHotkeyRegistration()
    {
        UnregisterVoiceAiHotkey();
        if (_hotkeySource is null ||
            _hotkeyDashboard is null ||
            !_hotkeyDashboard.VoiceAiEnabled ||
            !TryParseVoiceAiHotkey(_hotkeyDashboard.VoiceAiHotkey, out var key) ||
            !IsGlobalVoiceAiHotkeyCandidate(key))
        {
            return;
        }

        var virtualKey = KeyInterop.VirtualKeyFromKey(key);
        if (virtualKey == 0)
        {
            return;
        }

        _isVoiceAiHotkeyRegistered = RegisterHotKey(
            _hotkeySource.Handle,
            VoiceAiHotkeyId,
            fsModifiers: 0,
            vk: (uint)virtualKey);
    }

    private void UnregisterVoiceAiHotkey()
    {
        if (!_isVoiceAiHotkeyRegistered || _hotkeySource is null)
        {
            _isVoiceAiHotkeyRegistered = false;
            return;
        }

        UnregisterHotKey(_hotkeySource.Handle, VoiceAiHotkeyId);
        _isVoiceAiHotkeyRegistered = false;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey &&
            wParam.ToInt32() == VoiceAiHotkeyId &&
            _hotkeyDashboard is not null &&
            TryParseVoiceAiHotkey(_hotkeyDashboard.VoiceAiHotkey, out var key))
        {
            handled = _hotkeyDashboard.TryHandleVoiceAiHotkey(key);
        }

        return IntPtr.Zero;
    }

    private static bool TryParseVoiceAiHotkey(string? hotkey, out Key key)
    {
        return Enum.TryParse(hotkey, ignoreCase: true, out key) && key != Key.None;
    }

    private static bool IsGlobalVoiceAiHotkeyCandidate(Key key)
    {
        return key >= Key.F13 && key <= Key.F24;
    }

    private void CloseAfterShutdown()
    {
        _ = Dispatcher.BeginInvoke(
            DispatcherPriority.Send,
            new Action(() =>
            {
                try
                {
                    Close();
                }
                catch (InvalidOperationException)
                {
                }
            }));
    }

    private void ApplyWindowStateVisuals(WindowState windowState, bool animate)
    {
        var profile = WindowStateTransitionPlanner.Create(windowState);
        WindowRoot.Margin = profile.ContentMargin;

        if (animate)
        {
            AnimateWindowRoot(profile);
        }
        else
        {
            WindowRootScaleTransform.ScaleX = 1d;
            WindowRootScaleTransform.ScaleY = 1d;
            WindowRoot.Opacity = 1d;
        }

        if (profile.ShowFeedback)
        {
            ShowWindowStateToast(profile.FeedbackMessage);
        }
    }

    private void AnimateWindowRoot(WindowStateTransitionProfile profile)
    {
        var duration = TimeSpan.FromMilliseconds(180);
        var easing = new CubicEase
        {
            EasingMode = EasingMode.EaseOut
        };

        WindowRootScaleTransform.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            new DoubleAnimation(profile.InitialScale, 1d, duration)
            {
                EasingFunction = easing
            });
        WindowRootScaleTransform.BeginAnimation(
            ScaleTransform.ScaleYProperty,
            new DoubleAnimation(profile.InitialScale, 1d, duration)
            {
                EasingFunction = easing
            });
        WindowRoot.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(profile.InitialOpacity, 1d, duration)
            {
                EasingFunction = easing
            });
    }

    private void ShowWindowStateToast(string message)
    {
        _windowStateToastStoryboard?.Stop(WindowStateToast);

        WindowStateToastText.Text = message;
        WindowStateToast.Visibility = Visibility.Visible;
        WindowStateToast.Opacity = 0d;
        WindowStateToastScaleTransform.ScaleX = 0.96d;
        WindowStateToastScaleTransform.ScaleY = 0.96d;
        WindowStateToastTranslateTransform.Y = -6d;

        var storyboard = new Storyboard();
        storyboard.Children.Add(CreateDoubleAnimation(WindowStateToast, OpacityProperty, 0d, 1d, 0));
        storyboard.Children.Add(CreateDoubleAnimation(WindowStateToast, OpacityProperty, 1d, 0d, 1250));
        storyboard.Children.Add(CreateDoubleAnimation(WindowStateToastScaleTransform, ScaleTransform.ScaleXProperty, 0.96d, 1d, 0));
        storyboard.Children.Add(CreateDoubleAnimation(WindowStateToastScaleTransform, ScaleTransform.ScaleYProperty, 0.96d, 1d, 0));
        storyboard.Children.Add(CreateDoubleAnimation(WindowStateToastTranslateTransform, TranslateTransform.YProperty, -6d, 0d, 0));
        storyboard.Completed += (_, _) => WindowStateToast.Visibility = Visibility.Collapsed;
        _windowStateToastStoryboard = storyboard;
        storyboard.Begin(WindowStateToast, true);
    }

    private static Timeline CreateDoubleAnimation(
        DependencyObject target,
        DependencyProperty property,
        double from,
        double to,
        int beginMilliseconds)
    {
        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            BeginTime = TimeSpan.FromMilliseconds(beginMilliseconds),
            Duration = TimeSpan.FromMilliseconds(beginMilliseconds == 0 ? 180 : 220),
            EasingFunction = new CubicEase
            {
                EasingMode = EasingMode.EaseOut
            }
        };

        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, new PropertyPath(property));
        return animation;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
