using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using F1Telemetry.App.Windowing;

namespace F1Telemetry.App;

/// <summary>
/// Hosts the real-time telemetry dashboard and applies lightweight window-state transitions.
/// </summary>
public partial class MainWindow : Window
{
    private Storyboard? _windowStateToastStoryboard;
    private bool _shutdownStarted;
    private bool _shutdownCompleted;

    /// <summary>
    /// Initializes the main window shell.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyWindowStateVisuals(WindowState, animate: false);
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
        if (_shutdownCompleted)
        {
            Application.Current?.Shutdown();
        }
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
}
