using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace F1Telemetry.App.Views.Shell;

/// <summary>
/// Displays transient shell feedback for window state changes.
/// </summary>
public partial class ToastOverlay : UserControl
{
    private Storyboard? _windowStateToastStoryboard;

    /// <summary>
    /// Initializes the toast overlay view.
    /// </summary>
    public ToastOverlay()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Shows the toast with the supplied message.
    /// </summary>
    /// <param name="message">The toast text.</param>
    public void ShowMessage(string message)
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
