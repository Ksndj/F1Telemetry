using System.Windows;
using System.Windows.Controls;

namespace F1Telemetry.App.AttachedProperties;

/// <summary>
/// Provides a bindable attached password value for WPF password boxes.
/// </summary>
public static class PasswordBoxBinding
{
    /// <summary>
    /// Gets the bindable password value.
    /// </summary>
    public static string GetBoundPassword(DependencyObject dependencyObject)
    {
        return (string?)dependencyObject.GetValue(BoundPasswordProperty) ?? string.Empty;
    }

    /// <summary>
    /// Sets the bindable password value.
    /// </summary>
    public static void SetBoundPassword(DependencyObject dependencyObject, string value)
    {
        dependencyObject.SetValue(BoundPasswordProperty, value);
    }

    /// <summary>
    /// Gets a value indicating whether binding updates are currently being applied.
    /// </summary>
    public static bool GetIsUpdating(DependencyObject dependencyObject)
    {
        return (bool)dependencyObject.GetValue(IsUpdatingProperty);
    }

    /// <summary>
    /// Sets a value indicating whether binding updates are currently being applied.
    /// </summary>
    public static void SetIsUpdating(DependencyObject dependencyObject, bool value)
    {
        dependencyObject.SetValue(IsUpdatingProperty, value);
    }

    /// <summary>
    /// Gets or sets the bindable password attached property.
    /// </summary>
    public static readonly DependencyProperty BoundPasswordProperty =
        DependencyProperty.RegisterAttached(
            "BoundPassword",
            typeof(string),
            typeof(PasswordBoxBinding),
            new PropertyMetadata(null, OnBoundPasswordChanged));

    /// <summary>
    /// Gets or sets the internal update guard property.
    /// </summary>
    public static readonly DependencyProperty IsUpdatingProperty =
        DependencyProperty.RegisterAttached(
            "IsUpdating",
            typeof(bool),
            typeof(PasswordBoxBinding),
            new PropertyMetadata(false));

    private static void OnBoundPasswordChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not PasswordBox passwordBox)
        {
            return;
        }

        passwordBox.PasswordChanged -= HandlePasswordChanged;

        if (!GetIsUpdating(passwordBox))
        {
            passwordBox.Password = e.NewValue as string ?? string.Empty;
        }

        passwordBox.PasswordChanged += HandlePasswordChanged;
    }

    private static void HandlePasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not PasswordBox passwordBox)
        {
            return;
        }

        SetIsUpdating(passwordBox, true);
        SetBoundPassword(passwordBox, passwordBox.Password);
        SetIsUpdating(passwordBox, false);
    }
}
