using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace F1Telemetry.App.ViewModels;

/// <summary>
/// Exposes the AI/TTS page binding surface while DashboardViewModel keeps the current business coordination.
/// </summary>
public sealed class AiTtsViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly DashboardViewModel _dashboard;
    private bool _disposed;

    /// <summary>
    /// Initializes a new AI/TTS page view model.
    /// </summary>
    /// <param name="dashboard">The dashboard view model that still coordinates AI, TTS, tyre inventory, and assistant actions.</param>
    public AiTtsViewModel(DashboardViewModel dashboard)
    {
        _dashboard = dashboard ?? throw new ArgumentNullException(nameof(dashboard));
        _dashboard.PropertyChanged += OnDashboardPropertyChanged;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Gets or sets a value indicating whether AI analysis is enabled.
    /// </summary>
    public bool AiEnabled
    {
        get => _dashboard.AiEnabled;
        set => _dashboard.AiEnabled = value;
    }

    /// <summary>
    /// Gets or sets the configured AI base URL.
    /// </summary>
    public string AiBaseUrl
    {
        get => _dashboard.AiBaseUrl;
        set => _dashboard.AiBaseUrl = value;
    }

    /// <summary>
    /// Gets or sets the configured AI model.
    /// </summary>
    public string AiModel
    {
        get => _dashboard.AiModel;
        set => _dashboard.AiModel = value;
    }

    /// <summary>
    /// Gets or sets the configured AI API key.
    /// </summary>
    public string AiApiKey
    {
        get => _dashboard.AiApiKey;
        set => _dashboard.AiApiKey = value;
    }

    /// <summary>
    /// Gets the safe API key status label.
    /// </summary>
    public string AiApiKeyStatusText => _dashboard.AiApiKeyStatusText;

    /// <summary>
    /// Gets the current AI settings save status.
    /// </summary>
    public string AiSettingsSaveStatusText => _dashboard.AiSettingsSaveStatusText;

    /// <summary>
    /// Gets or sets a value indicating whether TTS playback is enabled.
    /// </summary>
    public bool TtsEnabled
    {
        get => _dashboard.TtsEnabled;
        set => _dashboard.TtsEnabled = value;
    }

    /// <summary>
    /// Gets a value indicating whether Windows voices were discovered.
    /// </summary>
    public bool HasAvailableVoices => _dashboard.HasAvailableVoices;

    /// <summary>
    /// Gets the Windows speech voices available for TTS selection.
    /// </summary>
    public ObservableCollection<string> AvailableVoices => _dashboard.AvailableVoices;

    /// <summary>
    /// Gets or sets the configured Windows voice name.
    /// </summary>
    public string TtsVoiceName
    {
        get => _dashboard.TtsVoiceName;
        set => _dashboard.TtsVoiceName = value;
    }

    /// <summary>
    /// Gets the current Windows voice discovery status.
    /// </summary>
    public string TtsVoiceStatusText => _dashboard.TtsVoiceStatusText;

    /// <summary>
    /// Gets or sets the TTS playback volume.
    /// </summary>
    public int TtsVolume
    {
        get => _dashboard.TtsVolume;
        set => _dashboard.TtsVolume = value;
    }

    /// <summary>
    /// Gets or sets the TTS playback rate.
    /// </summary>
    public int TtsRate
    {
        get => _dashboard.TtsRate;
        set => _dashboard.TtsRate = value;
    }

    /// <summary>
    /// Gets the current TyreSets packet status.
    /// </summary>
    public string RaceWeekendTyreSetsStatusText => _dashboard.RaceWeekendTyreSetsStatusText;

    /// <summary>
    /// Gets the current race-weekend tyre plan status.
    /// </summary>
    public string RaceWeekendTyrePlanStatusText => _dashboard.RaceWeekendTyrePlanStatusText;

    /// <summary>
    /// Gets the editable race-weekend tyre inventory rows.
    /// </summary>
    public ObservableCollection<RaceWeekendTyreInventoryItemViewModel> RaceWeekendTyreInventoryItems =>
        _dashboard.RaceWeekendTyreInventoryItems;

    /// <summary>
    /// Gets or sets the recommended maximum race-weekend tyre wear percentage.
    /// </summary>
    public int RaceWeekendTyreMaxWearPercent
    {
        get => _dashboard.RaceWeekendTyreMaxWearPercent;
        set => _dashboard.RaceWeekendTyreMaxWearPercent = value;
    }

    /// <summary>
    /// Gets the command that reads race-weekend tyre inventory from the latest TyreSets packet.
    /// </summary>
    public ICommand ReadTyreSetsInventoryCommand => _dashboard.ReadTyreSetsInventoryCommand;

    /// <summary>
    /// Gets the command that clears the race-weekend tyre inventory counts.
    /// </summary>
    public ICommand ClearTyreInventoryCommand => _dashboard.ClearTyreInventoryCommand;

    /// <summary>
    /// Gets the command that persists the race-weekend tyre inventory plan.
    /// </summary>
    public ICommand SaveTyreInventoryCommand => _dashboard.SaveTyreInventoryCommand;

    /// <summary>
    /// Gets or sets a value indicating whether text/voice race assistant queries are enabled.
    /// </summary>
    public bool VoiceAssistantEnabled
    {
        get => _dashboard.VoiceAssistantEnabled;
        set => _dashboard.VoiceAssistantEnabled = value;
    }

    /// <summary>
    /// Gets or sets a value indicating whether race assistant answers should be spoken.
    /// </summary>
    public bool VoiceAssistantEnableTtsAnswer
    {
        get => _dashboard.VoiceAssistantEnableTtsAnswer;
        set => _dashboard.VoiceAssistantEnableTtsAnswer = value;
    }

    /// <summary>
    /// Gets or sets the text question submitted to the race assistant.
    /// </summary>
    public string VoiceAssistantQuestionText
    {
        get => _dashboard.VoiceAssistantQuestionText;
        set => _dashboard.VoiceAssistantQuestionText = value;
    }

    /// <summary>
    /// Gets the telemetry notice shown near the race assistant controls.
    /// </summary>
    public string VoiceAssistantTelemetryNoticeText => _dashboard.VoiceAssistantTelemetryNoticeText;

    /// <summary>
    /// Gets the latest recognized voice assistant text.
    /// </summary>
    public string VoiceAssistantRecognizedText => _dashboard.VoiceAssistantRecognizedText;

    /// <summary>
    /// Gets the latest voice assistant intent label.
    /// </summary>
    public string VoiceAssistantIntentText => _dashboard.VoiceAssistantIntentText;

    /// <summary>
    /// Gets the latest voice assistant mode label.
    /// </summary>
    public string VoiceAssistantModeText => _dashboard.VoiceAssistantModeText;

    /// <summary>
    /// Gets the current race assistant status text.
    /// </summary>
    public string VoiceAssistantStatusText => _dashboard.VoiceAssistantStatusText;

    /// <summary>
    /// Gets the latest race assistant advice type label.
    /// </summary>
    public string VoiceAssistantAdviceTypeText => _dashboard.VoiceAssistantAdviceTypeText;

    /// <summary>
    /// Gets the latest race assistant summary.
    /// </summary>
    public string VoiceAssistantSummaryText => _dashboard.VoiceAssistantSummaryText;

    /// <summary>
    /// Gets the latest race assistant reasoning text.
    /// </summary>
    public string VoiceAssistantReasonText => _dashboard.VoiceAssistantReasonText;

    /// <summary>
    /// Gets the latest recommended action.
    /// </summary>
    public string VoiceAssistantRecommendedActionText => _dashboard.VoiceAssistantRecommendedActionText;

    /// <summary>
    /// Gets the latest race assistant confidence text.
    /// </summary>
    public string VoiceAssistantConfidenceText => _dashboard.VoiceAssistantConfidenceText;

    /// <summary>
    /// Gets the latest race assistant risk level text.
    /// </summary>
    public string VoiceAssistantRiskLevelText => _dashboard.VoiceAssistantRiskLevelText;

    /// <summary>
    /// Gets the latest missing-data summary.
    /// </summary>
    public string VoiceAssistantMissingDataText => _dashboard.VoiceAssistantMissingDataText;

    /// <summary>
    /// Gets the detailed missing-data tooltip text.
    /// </summary>
    public string VoiceAssistantMissingDataDetailText => _dashboard.VoiceAssistantMissingDataDetailText;

    /// <summary>
    /// Gets the recent race assistant history rows.
    /// </summary>
    public ObservableCollection<RaceAssistantHistoryItemViewModel> RaceAssistantHistory =>
        _dashboard.RaceAssistantHistory;

    /// <summary>
    /// Gets the command that submits the text question to the race assistant.
    /// </summary>
    public ICommand AskRaceAssistantQuestionCommand => _dashboard.AskRaceAssistantQuestionCommand;

    /// <summary>
    /// Gets the command that starts or stops voice race assistant input.
    /// </summary>
    public ICommand ToggleRaceAssistantVoiceCommand => _dashboard.ToggleRaceAssistantVoiceCommand;

    /// <summary>
    /// Gets the command that cancels the active race assistant query.
    /// </summary>
    public ICommand CancelRaceAssistantQuestionCommand => _dashboard.CancelRaceAssistantQuestionCommand;

    /// <summary>
    /// Gets the unified AI/TTS/system log entries.
    /// </summary>
    public ObservableCollection<LogEntryViewModel> AiTtsLogs => _dashboard.AiTtsLogs;

    /// <summary>
    /// Releases the dashboard property change subscription.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _dashboard.PropertyChanged -= OnDashboardPropertyChanged;
        _disposed = true;
    }

    private void OnDashboardPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.PropertyName))
        {
            PropertyChanged?.Invoke(this, e);
            return;
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(e.PropertyName));
    }
}
