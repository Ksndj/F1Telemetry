using System.Globalization;
using System.Windows.Input;
using F1Telemetry.Core.Abstractions;
using F1Telemetry.Core.Models;

namespace F1Telemetry.App.ViewModels;

/// <summary>
/// Represents one editable race-weekend tyre inventory row on the AI/TTS page.
/// </summary>
public sealed class RaceWeekendTyreInventoryItemViewModel : ViewModelBase
{
    private readonly Action _onCountChanged;
    private int _count;

    /// <summary>
    /// Initializes a new tyre inventory row.
    /// </summary>
    public RaceWeekendTyreInventoryItemViewModel(
        string compound,
        string displayName,
        string englishName,
        string accentBrush,
        Action onCountChanged)
    {
        Compound = compound;
        DisplayName = displayName;
        EnglishName = englishName;
        AccentBrush = accentBrush;
        _onCountChanged = onCountChanged ?? throw new ArgumentNullException(nameof(onCountChanged));
        IncrementCommand = new RelayCommand(() => Count += 1, () => Count < RaceWeekendTyrePlan.MaxInventoryCount);
        DecrementCommand = new RelayCommand(() => Count -= 1, () => Count > 0);
    }

    /// <summary>
    /// Gets the canonical compound key.
    /// </summary>
    public string Compound { get; }

    /// <summary>
    /// Gets the Chinese display name.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets the English compound name.
    /// </summary>
    public string EnglishName { get; }

    /// <summary>
    /// Gets the card accent brush.
    /// </summary>
    public string AccentBrush { get; }

    /// <summary>
    /// Gets or sets the editable inventory count.
    /// </summary>
    public int Count
    {
        get => _count;
        set
        {
            var clamped = Math.Clamp(value, 0, RaceWeekendTyrePlan.MaxInventoryCount);
            if (SetProperty(ref _count, clamped))
            {
                OnPropertyChanged(nameof(CountText));
                IncrementCommand.RaiseCanExecuteChanged();
                DecrementCommand.RaiseCanExecuteChanged();
                _onCountChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the editable count text, rejecting invalid values by restoring the current count.
    /// </summary>
    public string CountText
    {
        get => Count.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                Count = parsed;
            }
            else
            {
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets the command that increases the count.
    /// </summary>
    public RelayCommand IncrementCommand { get; }

    /// <summary>
    /// Gets the command that decreases the count.
    /// </summary>
    public RelayCommand DecrementCommand { get; }

    /// <summary>
    /// Sets the count without notifying the parent save callback.
    /// </summary>
    public void SetCountSilently(int count)
    {
        var clamped = Math.Clamp(count, 0, RaceWeekendTyrePlan.MaxInventoryCount);
        if (SetProperty(ref _count, clamped, nameof(Count)))
        {
            OnPropertyChanged(nameof(CountText));
            IncrementCommand.RaiseCanExecuteChanged();
            DecrementCommand.RaiseCanExecuteChanged();
        }
    }
}
