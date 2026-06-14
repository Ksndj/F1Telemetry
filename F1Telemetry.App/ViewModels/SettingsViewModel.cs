using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using F1Telemetry.Core.Models;

namespace F1Telemetry.App.ViewModels;

/// <summary>
/// Exposes the Settings page binding surface while DashboardViewModel keeps the current runtime coordination.
/// </summary>
public sealed class SettingsViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly DashboardViewModel _dashboard;
    private bool _disposed;

    /// <summary>
    /// Initializes a new Settings page view model.
    /// </summary>
    /// <param name="dashboard">The dashboard view model that still coordinates settings persistence and runtime state.</param>
    public SettingsViewModel(DashboardViewModel dashboard)
    {
        _dashboard = dashboard ?? throw new ArgumentNullException(nameof(dashboard));
        _dashboard.PropertyChanged += OnDashboardPropertyChanged;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Gets the command that opens the GitHub Releases download page.
    /// </summary>
    public ICommand DownloadLatestVersionCommand => _dashboard.DownloadLatestVersionCommand;

    /// <summary>
    /// Gets or sets the UDP port text.
    /// </summary>
    public string PortText
    {
        get => _dashboard.PortText;
        set => _dashboard.PortText = value;
    }

    /// <summary>
    /// Gets the connection state label.
    /// </summary>
    public string ConnectionStateText => _dashboard.ConnectionStateText;

    /// <summary>
    /// Gets the display text for the active UDP port.
    /// </summary>
    public string ListeningPortText => _dashboard.ListeningPortText;

    /// <summary>
    /// Gets the current application version text.
    /// </summary>
    public string ApplicationVersionText => _dashboard.ApplicationVersionText;

    /// <summary>
    /// Gets the latest status message.
    /// </summary>
    public string StatusMessage => _dashboard.StatusMessage;

    /// <summary>
    /// Gets the command that opens the categorized app log directory.
    /// </summary>
    public ICommand OpenAppLogDirectoryCommand => _dashboard.OpenAppLogDirectoryCommand;

    /// <summary>
    /// Gets the command that opens the RaceAssistant audit log directory.
    /// </summary>
    public ICommand OpenRaceAssistantLogDirectoryCommand => _dashboard.OpenRaceAssistantLogDirectoryCommand;

    /// <summary>
    /// Gets or sets a value indicating whether categorized app file logs are enabled.
    /// </summary>
    public bool EnableAppFileLog
    {
        get => _dashboard.EnableAppFileLog;
        set => _dashboard.EnableAppFileLog = value;
    }

    /// <summary>
    /// Gets or sets a value indicating whether RaceAssistant audit JSONL is enabled.
    /// </summary>
    public bool EnableRaceAssistantAuditLog
    {
        get => _dashboard.EnableRaceAssistantAuditLog;
        set => _dashboard.EnableRaceAssistantAuditLog = value;
    }

    /// <summary>
    /// Gets or sets a value indicating whether prompt summaries may be logged.
    /// </summary>
    public bool RaceAssistantLogPromptSummary
    {
        get => _dashboard.RaceAssistantLogPromptSummary;
        set => _dashboard.RaceAssistantLogPromptSummary = value;
    }

    /// <summary>
    /// Gets the categorized app log directory shown in Settings.
    /// </summary>
    public string AppLogDirectoryText => _dashboard.AppLogDirectoryText;

    /// <summary>
    /// Gets the latest categorized app log file path.
    /// </summary>
    public string AppLogLastFilePathText => _dashboard.AppLogLastFilePathText;

    /// <summary>
    /// Gets the latest categorized app log file size.
    /// </summary>
    public string AppLogLastFileSizeText => _dashboard.AppLogLastFileSizeText;

    /// <summary>
    /// Gets the latest categorized app log write time.
    /// </summary>
    public string AppLogLastWriteTimeText => _dashboard.AppLogLastWriteTimeText;

    /// <summary>
    /// Gets the RaceAssistant audit log directory shown in Settings.
    /// </summary>
    public string RaceAssistantLogDirectoryText => _dashboard.RaceAssistantLogDirectoryText;

    /// <summary>
    /// Gets the latest RaceAssistant audit log file path.
    /// </summary>
    public string RaceAssistantLogLastFilePathText => _dashboard.RaceAssistantLogLastFilePathText;

    /// <summary>
    /// Gets the latest RaceAssistant audit log file size.
    /// </summary>
    public string RaceAssistantLogLastFileSizeText => _dashboard.RaceAssistantLogLastFileSizeText;

    /// <summary>
    /// Gets the latest RaceAssistant audit log write time.
    /// </summary>
    public string RaceAssistantLogLastWriteTimeText => _dashboard.RaceAssistantLogLastWriteTimeText;

    /// <summary>
    /// Gets or sets the maximum log file size text in megabytes.
    /// </summary>
    public string MaxLogFileSizeMbText
    {
        get => _dashboard.MaxLogFileSizeMbText;
        set => _dashboard.MaxLogFileSizeMbText = value;
    }

    /// <summary>
    /// Gets or sets the log retention days text.
    /// </summary>
    public string MaxLogRetentionDaysText
    {
        get => _dashboard.MaxLogRetentionDaysText;
        set => _dashboard.MaxLogRetentionDaysText = value;
    }

    /// <summary>
    /// Gets the runtime log settings status text.
    /// </summary>
    public string LogSettingsStatusText => _dashboard.LogSettingsStatusText;

    /// <summary>
    /// Gets the command that opens the raw UDP log directory.
    /// </summary>
    public ICommand OpenUdpRawLogDirectoryCommand => _dashboard.OpenUdpRawLogDirectoryCommand;

    /// <summary>
    /// Gets or sets a value indicating whether raw UDP JSONL logging is enabled.
    /// </summary>
    public bool UdpRawLogEnabled
    {
        get => _dashboard.UdpRawLogEnabled;
        set => _dashboard.UdpRawLogEnabled = value;
    }

    /// <summary>
    /// Gets the raw UDP log state summary.
    /// </summary>
    public string UdpRawLogStatusText => _dashboard.UdpRawLogStatusText;

    /// <summary>
    /// Gets the raw UDP log directory shown in Settings.
    /// </summary>
    public string UdpRawLogDirectoryText => _dashboard.UdpRawLogDirectoryText;

    /// <summary>
    /// Gets the current or most recent raw UDP log file path.
    /// </summary>
    public string UdpRawLogLastFilePathText => _dashboard.UdpRawLogLastFilePathText;

    /// <summary>
    /// Gets the current or most recent raw UDP log file size.
    /// </summary>
    public string UdpRawLogLastFileSizeText => _dashboard.UdpRawLogLastFileSizeText;

    /// <summary>
    /// Gets the current or most recent raw UDP log file write time.
    /// </summary>
    public string UdpRawLogLastWriteTimeText => _dashboard.UdpRawLogLastWriteTimeText;

    /// <summary>
    /// Gets the raw UDP packet count written in this app session.
    /// </summary>
    public long UdpRawLogWrittenPacketCount => _dashboard.UdpRawLogWrittenPacketCount;

    /// <summary>
    /// Gets the raw UDP packet count dropped in this app session.
    /// </summary>
    public long UdpRawLogDroppedPacketCount => _dashboard.UdpRawLogDroppedPacketCount;

    /// <summary>
    /// Gets the latest raw UDP log write error.
    /// </summary>
    public string UdpRawLogLastErrorText => _dashboard.UdpRawLogLastErrorText;

    /// <summary>
    /// Gets or sets a value indicating whether microphone AI queries can be triggered by the bound key.
    /// </summary>
    public bool VoiceAiEnabled
    {
        get => _dashboard.VoiceAiEnabled;
        set => _dashboard.VoiceAiEnabled = value;
    }

    /// <summary>
    /// Gets the command that captures the next Raw Input steering-wheel button press.
    /// </summary>
    public ICommand BindVoiceAiInputCommand => _dashboard.BindVoiceAiInputCommand;

    /// <summary>
    /// Gets the command that clears the saved steering-wheel voice AI binding.
    /// </summary>
    public ICommand ClearVoiceAiInputCommand => _dashboard.ClearVoiceAiInputCommand;

    /// <summary>
    /// Gets the user-facing steering-wheel binding label.
    /// </summary>
    public string VoiceAiBindingText => _dashboard.VoiceAiBindingText;

    /// <summary>
    /// Gets a value indicating whether the next steering-wheel button press is being captured.
    /// </summary>
    public bool VoiceAiBindingCaptureActive => _dashboard.VoiceAiBindingCaptureActive;

    /// <summary>
    /// Gets selectable push-to-talk modes.
    /// </summary>
    public ObservableCollection<VoiceAiTalkModeOptionViewModel> VoiceAiTalkModeOptions =>
        _dashboard.VoiceAiTalkModeOptions;

    /// <summary>
    /// Gets or sets the selected push-to-talk mode option.
    /// </summary>
    public VoiceAiTalkModeOptionViewModel? SelectedVoiceAiTalkModeOption
    {
        get => _dashboard.SelectedVoiceAiTalkModeOption;
        set => _dashboard.SelectedVoiceAiTalkModeOption = value;
    }

    /// <summary>
    /// Gets available system microphones for voice AI recording.
    /// </summary>
    public ObservableCollection<MicrophoneDeviceInfo> VoiceAiMicrophoneDevices =>
        _dashboard.VoiceAiMicrophoneDevices;

    /// <summary>
    /// Gets or sets the selected microphone device identifier.
    /// </summary>
    public string VoiceAiMicrophoneDeviceId
    {
        get => _dashboard.VoiceAiMicrophoneDeviceId;
        set => _dashboard.VoiceAiMicrophoneDeviceId = value;
    }

    /// <summary>
    /// Gets the command that refreshes the system microphone list.
    /// </summary>
    public ICommand RefreshMicrophonesCommand => _dashboard.RefreshMicrophonesCommand;

    /// <summary>
    /// Gets the command that records a short microphone input test.
    /// </summary>
    public ICommand TestMicrophoneCommand => _dashboard.TestMicrophoneCommand;

    /// <summary>
    /// Gets or sets a value indicating whether microphone preprocessing is enabled.
    /// </summary>
    public bool VoiceAiNoiseReductionEnabled
    {
        get => _dashboard.VoiceAiNoiseReductionEnabled;
        set => _dashboard.VoiceAiNoiseReductionEnabled = value;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the wind-noise high-pass filter is enabled.
    /// </summary>
    public bool VoiceAiHighPassFilterEnabled
    {
        get => _dashboard.VoiceAiHighPassFilterEnabled;
        set => _dashboard.VoiceAiHighPassFilterEnabled = value;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the microphone noise gate is enabled.
    /// </summary>
    public bool VoiceAiNoiseGateEnabled
    {
        get => _dashboard.VoiceAiNoiseGateEnabled;
        set => _dashboard.VoiceAiNoiseGateEnabled = value;
    }

    /// <summary>
    /// Gets or sets a value indicating whether voice activity detection is enabled.
    /// </summary>
    public bool VoiceAiVadEnabled
    {
        get => _dashboard.VoiceAiVadEnabled;
        set => _dashboard.VoiceAiVadEnabled = value;
    }

    /// <summary>
    /// Gets or sets a value indicating whether automatic microphone gain is enabled.
    /// </summary>
    public bool VoiceAiAutoGainEnabled
    {
        get => _dashboard.VoiceAiAutoGainEnabled;
        set => _dashboard.VoiceAiAutoGainEnabled = value;
    }

    /// <summary>
    /// Gets or sets the high-pass filter cutoff text in hertz.
    /// </summary>
    public string VoiceAiHighPassCutoffHzText
    {
        get => _dashboard.VoiceAiHighPassCutoffHzText;
        set => _dashboard.VoiceAiHighPassCutoffHzText = value;
    }

    /// <summary>
    /// Gets or sets the noise gate threshold text in dBFS.
    /// </summary>
    public string VoiceAiNoiseGateThresholdDbText
    {
        get => _dashboard.VoiceAiNoiseGateThresholdDbText;
        set => _dashboard.VoiceAiNoiseGateThresholdDbText = value;
    }

    /// <summary>
    /// Gets or sets the maximum recording duration text in seconds.
    /// </summary>
    public string VoiceAiMaxRecordingSecondsText
    {
        get => _dashboard.VoiceAiMaxRecordingSecondsText;
        set => _dashboard.VoiceAiMaxRecordingSecondsText = value;
    }

    /// <summary>
    /// Gets or sets the minimum speech recognition confidence text.
    /// </summary>
    public string VoiceAiMinRecognitionConfidenceText
    {
        get => _dashboard.VoiceAiMinRecognitionConfidenceText;
        set => _dashboard.VoiceAiMinRecognitionConfidenceText = value;
    }

    /// <summary>
    /// Gets or sets the VAD pre-speech padding text in milliseconds.
    /// </summary>
    public string VoiceAiPreSpeechPaddingMsText
    {
        get => _dashboard.VoiceAiPreSpeechPaddingMsText;
        set => _dashboard.VoiceAiPreSpeechPaddingMsText = value;
    }

    /// <summary>
    /// Gets or sets the VAD post-speech padding text in milliseconds.
    /// </summary>
    public string VoiceAiPostSpeechPaddingMsText
    {
        get => _dashboard.VoiceAiPostSpeechPaddingMsText;
        set => _dashboard.VoiceAiPostSpeechPaddingMsText = value;
    }

    /// <summary>
    /// Gets or sets the minimum speech duration text in milliseconds.
    /// </summary>
    public string VoiceAiMinSpeechDurationMsText
    {
        get => _dashboard.VoiceAiMinSpeechDurationMsText;
        set => _dashboard.VoiceAiMinSpeechDurationMsText = value;
    }

    /// <summary>
    /// Gets the latest normalized microphone input test level.
    /// </summary>
    public double VoiceAiMicrophoneTestLevel => _dashboard.VoiceAiMicrophoneTestLevel;

    /// <summary>
    /// Gets the microphone test status shown in Settings.
    /// </summary>
    public string VoiceAiMicrophoneStatusText => _dashboard.VoiceAiMicrophoneStatusText;

    /// <summary>
    /// Gets the current microphone AI query status shown in Settings.
    /// </summary>
    public string VoiceAiStatusText => _dashboard.VoiceAiStatusText;

    /// <summary>
    /// Gets the latest microphone preprocessing and recognition status details.
    /// </summary>
    public string VoiceAiRecognitionStatusDetailText => _dashboard.VoiceAiRecognitionStatusDetailText;

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
