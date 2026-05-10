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
    /// <param name="iconGlyph">Icon glyph shown in the collapsed sidebar.</param>
    public ShellNavigationItemViewModel(string key, string name, string iconGlyph = "\uE10F")
    {
        Key = string.IsNullOrWhiteSpace(key) ? throw new ArgumentException("Navigation key is required.", nameof(key)) : key;
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Navigation name is required.", nameof(name)) : name;
        IconGlyph = string.IsNullOrWhiteSpace(iconGlyph) ? throw new ArgumentException("Navigation icon is required.", nameof(iconGlyph)) : iconGlyph;
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
    /// Gets the Segoe MDL2 glyph shown when the sidebar is collapsed.
    /// </summary>
    public string IconGlyph { get; }

    /// <summary>
    /// Creates the default shell navigation set.
    /// </summary>
    public static IReadOnlyList<ShellNavigationItemViewModel> CreateDefaultItems()
    {
        return
        [
            new("overview", "实时概览", "\uE80F"),
            new("charts", "分析播报", "\uE9D2"),
            new("lap-history", "单圈历史", "\uE81C"),
            new("post-race-review", "赛后复盘", "\uE9D9"),
            new("opponents", "对手", "\uE716"),
            new("event-logs", "事件日志", "\uE787"),
            new("ai-tts", "AI / TTS", "\uE8BD"),
            new("settings", "设置", "\uE713")
        ];
    }
}
