namespace F1Telemetry.App.ViewModels;

/// <summary>
/// Represents a selectable item in the main window shell navigation.
/// </summary>
public sealed class ShellNavigationItemViewModel
{
    /// <summary>
    /// Initializes a new shell navigation item.
    /// </summary>
    /// <param name="key">Stable navigation key for future page routing.</param>
    /// <param name="name">Display name shown in the sidebar.</param>
    public ShellNavigationItemViewModel(string key, string name)
    {
        Key = string.IsNullOrWhiteSpace(key) ? throw new ArgumentException("Navigation key is required.", nameof(key)) : key;
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Navigation name is required.", nameof(name)) : name;
    }

    /// <summary>
    /// Gets the stable navigation key for future page routing.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Gets the display name shown in the sidebar.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Creates the default V1.0.2-M1 shell navigation set.
    /// </summary>
    public static IReadOnlyList<ShellNavigationItemViewModel> CreateDefaultItems()
    {
        return
        [
            new("overview", "实时概览"),
            new("charts", "图表"),
            new("lap-history", "单圈历史"),
            new("opponents", "对手"),
            new("event-logs", "事件日志"),
            new("ai-tts", "AI / TTS"),
            new("settings", "设置")
        ];
    }
}
