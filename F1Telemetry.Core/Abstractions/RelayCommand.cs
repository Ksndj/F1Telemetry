using System.Windows.Input;

namespace F1Telemetry.Core.Abstractions;

/// <summary>
/// Wraps a parameterless delegate as a WPF command.
/// </summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    /// <summary>
    /// Initializes a relay command.
    /// </summary>
    /// <param name="execute">The command action.</param>
    /// <param name="canExecute">The optional command guard.</param>
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <inheritdoc />
    public event EventHandler? CanExecuteChanged;

    /// <inheritdoc />
    public bool CanExecute(object? parameter)
    {
        return _canExecute?.Invoke() ?? true;
    }

    /// <inheritdoc />
    public void Execute(object? parameter)
    {
        _execute();
    }

    /// <summary>
    /// Raises the command state changed notification.
    /// </summary>
    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// Wraps a typed delegate as a WPF command.
/// </summary>
/// <typeparam name="T">The command parameter type.</typeparam>
public sealed class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;
    private readonly Func<T?, bool>? _canExecute;

    /// <summary>
    /// Initializes a typed relay command.
    /// </summary>
    /// <param name="execute">The command action.</param>
    /// <param name="canExecute">The optional command guard.</param>
    public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <inheritdoc />
    public event EventHandler? CanExecuteChanged;

    /// <inheritdoc />
    public bool CanExecute(object? parameter)
    {
        return _canExecute?.Invoke(CoerceParameter(parameter)) ?? true;
    }

    /// <inheritdoc />
    public void Execute(object? parameter)
    {
        _execute(CoerceParameter(parameter));
    }

    /// <summary>
    /// Raises the command state changed notification.
    /// </summary>
    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    private static T? CoerceParameter(object? parameter)
    {
        if (parameter is null)
        {
            return default;
        }

        return parameter is T value ? value : default;
    }
}
