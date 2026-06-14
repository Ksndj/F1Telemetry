using System.Collections.ObjectModel;
using System.Windows.Input;
using F1Telemetry.Core.Abstractions;

namespace F1Telemetry.App.ViewModels;

/// <summary>
/// Owns the page-level state and commands shown by the AI broadcast page.
/// </summary>
public sealed class AiBroadcastViewModel : ViewModelBase
{
    private const string PostRaceAiNoReportText = "暂无 AI 分析报告";
    private const string PostRaceAiWaitingDataText = "等待完赛数据";

    private readonly Func<bool> _getAiEnabled;
    private readonly Func<string> _getAiApiKeyStatusText;
    private readonly Func<string> _getAiModel;
    private readonly Func<bool> _getTtsEnabled;
    private readonly Func<string> _getTtsVoiceName;
    private readonly Func<string> _getTtsVoiceStatusText;
    private readonly Func<bool> _canGeneratePostRaceAiSummary;
    private readonly Action<PostRaceAiCompletionModeOptionViewModel?> _selectedCompletionModeChanged;
    private readonly RelayCommand _generatePostRaceAiSummaryCommand;
    private readonly RelayCommand _regeneratePostRaceAiSummaryCommand;
    private PostRaceAiCompletionModeOptionViewModel? _selectedPostRaceAiCompletionMode;
    private string _postRaceAiStatusText = "等待完整正赛结束后生成 AI 总结。";
    private string _postRaceAiCompletionText = "自动判断：等待 UDP 最终分类。";
    private string _postRaceAiDataStatusText = PostRaceAiWaitingDataText;
    private string _postRaceAiSummaryCommandTooltipText = "等待可用于生成赛后总结的数据。";
    private string _postRaceAiFailureReason = string.Empty;
    private string _postRaceAiLastAnalysisText = "最近分析：暂无";
    private bool _postRaceAiHasReport;
    private string _postRaceAiReportSummaryText = PostRaceAiNoReportText;
    private string _postRaceAiKeyProblemsText = PostRaceAiWaitingDataText;
    private string _postRaceAiStrategyReviewText = PostRaceAiWaitingDataText;
    private string _postRaceAiTyreReviewText = PostRaceAiWaitingDataText;
    private string _postRaceAiErsFuelReviewText = PostRaceAiWaitingDataText;
    private string _postRaceAiOpponentReviewText = PostRaceAiWaitingDataText;
    private string _postRaceAiImprovementsText = PostRaceAiWaitingDataText;

    /// <summary>
    /// Initializes a new AI broadcast page view model.
    /// </summary>
    /// <param name="getAiEnabled">Returns whether AI analysis is enabled.</param>
    /// <param name="getAiApiKeyStatusText">Returns the safe API key status label.</param>
    /// <param name="getAiModel">Returns the configured AI model name.</param>
    /// <param name="getTtsEnabled">Returns whether TTS playback is enabled.</param>
    /// <param name="getTtsVoiceName">Returns the configured TTS voice name.</param>
    /// <param name="getTtsVoiceStatusText">Returns the TTS voice discovery status.</param>
    /// <param name="aiTtsLogs">The shared AI/TTS lifecycle log collection.</param>
    /// <param name="generatePostRaceAiSummary">Runs the first post-race AI generation action.</param>
    /// <param name="regeneratePostRaceAiSummary">Runs the post-race AI regeneration action.</param>
    /// <param name="canGeneratePostRaceAiSummary">Returns whether post-race AI generation can run.</param>
    /// <param name="selectedCompletionModeChanged">Handles completion mode changes that affect generation.</param>
    public AiBroadcastViewModel(
        Func<bool> getAiEnabled,
        Func<string> getAiApiKeyStatusText,
        Func<string> getAiModel,
        Func<bool> getTtsEnabled,
        Func<string> getTtsVoiceName,
        Func<string> getTtsVoiceStatusText,
        ObservableCollection<LogEntryViewModel> aiTtsLogs,
        Action generatePostRaceAiSummary,
        Action regeneratePostRaceAiSummary,
        Func<bool> canGeneratePostRaceAiSummary,
        Action<PostRaceAiCompletionModeOptionViewModel?> selectedCompletionModeChanged)
    {
        _getAiEnabled = getAiEnabled ?? throw new ArgumentNullException(nameof(getAiEnabled));
        _getAiApiKeyStatusText = getAiApiKeyStatusText ?? throw new ArgumentNullException(nameof(getAiApiKeyStatusText));
        _getAiModel = getAiModel ?? throw new ArgumentNullException(nameof(getAiModel));
        _getTtsEnabled = getTtsEnabled ?? throw new ArgumentNullException(nameof(getTtsEnabled));
        _getTtsVoiceName = getTtsVoiceName ?? throw new ArgumentNullException(nameof(getTtsVoiceName));
        _getTtsVoiceStatusText = getTtsVoiceStatusText ?? throw new ArgumentNullException(nameof(getTtsVoiceStatusText));
        _canGeneratePostRaceAiSummary = canGeneratePostRaceAiSummary ?? throw new ArgumentNullException(nameof(canGeneratePostRaceAiSummary));
        _selectedCompletionModeChanged = selectedCompletionModeChanged ?? throw new ArgumentNullException(nameof(selectedCompletionModeChanged));
        AiTtsLogs = aiTtsLogs ?? throw new ArgumentNullException(nameof(aiTtsLogs));
        ArgumentNullException.ThrowIfNull(generatePostRaceAiSummary);
        ArgumentNullException.ThrowIfNull(regeneratePostRaceAiSummary);
        AiAnalysisLogs = new ObservableCollection<LogEntryViewModel>();
        PostRaceAiCompletionModes = new ObservableCollection<PostRaceAiCompletionModeOptionViewModel>(
        [
            new()
            {
                Mode = PostRaceAiCompletionMode.Auto,
                DisplayName = "自动判断",
                Description = "收到正赛最终分类后自动生成总结。"
            },
            new()
            {
                Mode = PostRaceAiCompletionMode.Hold,
                DisplayName = "暂存不总结",
                Description = "中途存档或未跑完时保留状态，不上传 AI。"
            },
            new()
            {
                Mode = PostRaceAiCompletionMode.ForceComplete,
                DisplayName = "标记已完成",
                Description = "手动确认已跑完并生成赛后总结。"
            }
        ]);
        _selectedPostRaceAiCompletionMode = PostRaceAiCompletionModes[0];
        _generatePostRaceAiSummaryCommand = new RelayCommand(generatePostRaceAiSummary, () => CanGeneratePostRaceAiSummary);
        _regeneratePostRaceAiSummaryCommand = new RelayCommand(regeneratePostRaceAiSummary, () => CanGeneratePostRaceAiSummary);
    }

    /// <summary>
    /// Gets a value indicating whether AI analysis is enabled.
    /// </summary>
    public bool AiEnabled => _getAiEnabled();

    /// <summary>
    /// Gets the safe API key status label.
    /// </summary>
    public string AiApiKeyStatusText => _getAiApiKeyStatusText();

    /// <summary>
    /// Gets the configured AI model name.
    /// </summary>
    public string AiModel => _getAiModel();

    /// <summary>
    /// Gets a value indicating whether TTS playback is enabled.
    /// </summary>
    public bool TtsEnabled => _getTtsEnabled();

    /// <summary>
    /// Gets the configured TTS voice name.
    /// </summary>
    public string TtsVoiceName => _getTtsVoiceName();

    /// <summary>
    /// Gets the TTS voice discovery status.
    /// </summary>
    public string TtsVoiceStatusText => _getTtsVoiceStatusText();

    /// <summary>
    /// Gets the post-race AI summary lifecycle entries shown on the AI broadcast page.
    /// </summary>
    public ObservableCollection<LogEntryViewModel> AiAnalysisLogs { get; }

    /// <summary>
    /// Gets the shared AI/TTS lifecycle entries shown beside AI broadcast events.
    /// </summary>
    public ObservableCollection<LogEntryViewModel> AiTtsLogs { get; }

    /// <summary>
    /// Gets the selectable post-race AI completion modes.
    /// </summary>
    public ObservableCollection<PostRaceAiCompletionModeOptionViewModel> PostRaceAiCompletionModes { get; }

    /// <summary>
    /// Gets or sets the selected post-race AI completion mode.
    /// </summary>
    public PostRaceAiCompletionModeOptionViewModel? SelectedPostRaceAiCompletionMode
    {
        get => _selectedPostRaceAiCompletionMode;
        set
        {
            if (SetProperty(ref _selectedPostRaceAiCompletionMode, value))
            {
                _selectedCompletionModeChanged(value);
            }
        }
    }

    /// <summary>
    /// Gets the current post-race AI summary state.
    /// </summary>
    public string PostRaceAiStatusText
    {
        get => _postRaceAiStatusText;
        internal set => SetProperty(ref _postRaceAiStatusText, value);
    }

    /// <summary>
    /// Gets the current completion evidence used for post-race AI gating.
    /// </summary>
    public string PostRaceAiCompletionText
    {
        get => _postRaceAiCompletionText;
        internal set => SetProperty(ref _postRaceAiCompletionText, value);
    }

    /// <summary>
    /// Gets the compact post-race AI data readiness state.
    /// </summary>
    public string PostRaceAiDataStatusText
    {
        get => _postRaceAiDataStatusText;
        internal set => SetProperty(ref _postRaceAiDataStatusText, value);
    }

    /// <summary>
    /// Gets the tooltip explaining the current post-race summary button state.
    /// </summary>
    public string PostRaceAiSummaryCommandTooltipText
    {
        get => _postRaceAiSummaryCommandTooltipText;
        internal set => SetProperty(ref _postRaceAiSummaryCommandTooltipText, value);
    }

    /// <summary>
    /// Gets the latest post-race AI generation failure reason.
    /// </summary>
    public string PostRaceAiFailureReason
    {
        get => _postRaceAiFailureReason;
        internal set => SetProperty(ref _postRaceAiFailureReason, value);
    }

    /// <summary>
    /// Gets the last post-race AI analysis timestamp text.
    /// </summary>
    public string PostRaceAiLastAnalysisText
    {
        get => _postRaceAiLastAnalysisText;
        internal set => SetProperty(ref _postRaceAiLastAnalysisText, value);
    }

    /// <summary>
    /// Gets a value indicating whether a post-race AI report is available for display.
    /// </summary>
    public bool PostRaceAiHasReport
    {
        get => _postRaceAiHasReport;
        internal set => SetProperty(ref _postRaceAiHasReport, value);
    }

    /// <summary>
    /// Gets the latest post-race AI report conclusion.
    /// </summary>
    public string PostRaceAiReportSummaryText
    {
        get => _postRaceAiReportSummaryText;
        internal set => SetProperty(ref _postRaceAiReportSummaryText, value);
    }

    /// <summary>
    /// Gets the latest post-race AI report key problems.
    /// </summary>
    public string PostRaceAiKeyProblemsText
    {
        get => _postRaceAiKeyProblemsText;
        internal set => SetProperty(ref _postRaceAiKeyProblemsText, value);
    }

    /// <summary>
    /// Gets the latest post-race AI strategy review.
    /// </summary>
    public string PostRaceAiStrategyReviewText
    {
        get => _postRaceAiStrategyReviewText;
        internal set => SetProperty(ref _postRaceAiStrategyReviewText, value);
    }

    /// <summary>
    /// Gets the latest post-race AI tyre review.
    /// </summary>
    public string PostRaceAiTyreReviewText
    {
        get => _postRaceAiTyreReviewText;
        internal set => SetProperty(ref _postRaceAiTyreReviewText, value);
    }

    /// <summary>
    /// Gets the latest post-race AI ERS and fuel review.
    /// </summary>
    public string PostRaceAiErsFuelReviewText
    {
        get => _postRaceAiErsFuelReviewText;
        internal set => SetProperty(ref _postRaceAiErsFuelReviewText, value);
    }

    /// <summary>
    /// Gets the latest post-race AI opponent review.
    /// </summary>
    public string PostRaceAiOpponentReviewText
    {
        get => _postRaceAiOpponentReviewText;
        internal set => SetProperty(ref _postRaceAiOpponentReviewText, value);
    }

    /// <summary>
    /// Gets the latest post-race AI improvement suggestions.
    /// </summary>
    public string PostRaceAiImprovementsText
    {
        get => _postRaceAiImprovementsText;
        internal set => SetProperty(ref _postRaceAiImprovementsText, value);
    }

    /// <summary>
    /// Gets a value indicating whether the manual post-race AI summary command can run.
    /// </summary>
    public bool CanGeneratePostRaceAiSummary => _canGeneratePostRaceAiSummary();

    /// <summary>
    /// Gets the command that manually generates a post-race AI summary from staged race data.
    /// </summary>
    public ICommand GeneratePostRaceAiSummaryCommand => _generatePostRaceAiSummaryCommand;

    /// <summary>
    /// Gets the command that regenerates the current post-race AI summary even for the same lap.
    /// </summary>
    public ICommand RegeneratePostRaceAiSummaryCommand => _regeneratePostRaceAiSummaryCommand;

    internal void RaisePostRaceAiSummaryCommandStateChanged()
    {
        OnPropertyChanged(nameof(CanGeneratePostRaceAiSummary));
        _generatePostRaceAiSummaryCommand.RaiseCanExecuteChanged();
        _regeneratePostRaceAiSummaryCommand.RaiseCanExecuteChanged();
    }

    internal void RefreshSharedStatus()
    {
        OnPropertyChanged(nameof(AiEnabled));
        OnPropertyChanged(nameof(AiApiKeyStatusText));
        OnPropertyChanged(nameof(AiModel));
        OnPropertyChanged(nameof(TtsEnabled));
        OnPropertyChanged(nameof(TtsVoiceName));
        OnPropertyChanged(nameof(TtsVoiceStatusText));
    }

    internal void ResetPostRaceAiReportDetails()
    {
        PostRaceAiFailureReason = string.Empty;
        PostRaceAiHasReport = false;
        PostRaceAiLastAnalysisText = "最近分析：暂无";
        PostRaceAiReportSummaryText = PostRaceAiNoReportText;
        PostRaceAiKeyProblemsText = PostRaceAiWaitingDataText;
        PostRaceAiStrategyReviewText = PostRaceAiWaitingDataText;
        PostRaceAiTyreReviewText = PostRaceAiWaitingDataText;
        PostRaceAiErsFuelReviewText = PostRaceAiWaitingDataText;
        PostRaceAiOpponentReviewText = PostRaceAiWaitingDataText;
        PostRaceAiImprovementsText = PostRaceAiWaitingDataText;
    }
}
