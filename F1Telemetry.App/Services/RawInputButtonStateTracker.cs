namespace F1Telemetry.App.Services;

/// <summary>
/// Tracks pressed HID button usages per Raw Input device and emits stable button edges.
/// </summary>
public sealed class RawInputButtonStateTracker
{
    private readonly Dictionary<string, HashSet<int>> _pressedButtonsByReport = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Observes the currently pressed button indexes for one device report and returns changed edges.
    /// </summary>
    public IReadOnlyList<RawInputButtonEdge> Observe(string deviceId, int reportId, IEnumerable<int> pressedButtonIndexes)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return [];
        }

        var stateKey = BuildStateKey(deviceId, reportId);
        var current = pressedButtonIndexes
            .Where(buttonIndex => buttonIndex > 0)
            .ToHashSet();
        if (!_pressedButtonsByReport.TryGetValue(stateKey, out var previous))
        {
            _pressedButtonsByReport[stateKey] = current;
            return current
                .Order()
                .Select(
                    buttonIndex => new RawInputButtonEdge
                    {
                        ButtonIndex = buttonIndex,
                        IsPressed = true,
                        PressedChangeCount = current.Count,
                        ChangedButtonCount = current.Count
                    })
                .ToArray();
        }

        var pressed = current.Except(previous).Order().ToArray();
        var released = previous.Except(current).Order().ToArray();
        var changedButtonCount = pressed.Length + released.Length;
        _pressedButtonsByReport[stateKey] = current;
        if (changedButtonCount == 0)
        {
            return [];
        }

        var edges = new List<RawInputButtonEdge>(changedButtonCount);
        foreach (var buttonIndex in pressed)
        {
            edges.Add(
                new RawInputButtonEdge
                {
                    ButtonIndex = buttonIndex,
                    IsPressed = true,
                    PressedChangeCount = pressed.Length,
                    ChangedButtonCount = changedButtonCount
                });
        }

        foreach (var buttonIndex in released)
        {
            edges.Add(
                new RawInputButtonEdge
                {
                    ButtonIndex = buttonIndex,
                    IsPressed = false,
                    PressedChangeCount = pressed.Length,
                    ChangedButtonCount = changedButtonCount
                });
        }

        return edges;
    }

    /// <summary>
    /// Clears all remembered button states.
    /// </summary>
    public void Clear()
    {
        _pressedButtonsByReport.Clear();
    }

    private static string BuildStateKey(string deviceId, int reportId)
    {
        return $"{deviceId.Trim()}\u001F{reportId.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
    }
}

/// <summary>
/// Describes one stable HID button edge produced from a Raw Input button state snapshot.
/// </summary>
public sealed record RawInputButtonEdge
{
    /// <summary>
    /// Gets the one-based HID button usage index.
    /// </summary>
    public int ButtonIndex { get; init; }

    /// <summary>
    /// Gets a value indicating whether the edge is a press.
    /// </summary>
    public bool IsPressed { get; init; }

    /// <summary>
    /// Gets the number of button presses in the same state change.
    /// </summary>
    public int PressedChangeCount { get; init; }

    /// <summary>
    /// Gets the number of button press or release changes in the same state change.
    /// </summary>
    public int ChangedButtonCount { get; init; }
}
