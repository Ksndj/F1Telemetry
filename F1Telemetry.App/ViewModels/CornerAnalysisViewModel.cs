using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Windows.Input;
using F1Telemetry.AI.Interfaces;
using F1Telemetry.AI.Models;
using F1Telemetry.AI.Services;
using F1Telemetry.Analytics.Corners;
using F1Telemetry.Analytics.Events;
using F1Telemetry.Analytics.Laps;
using F1Telemetry.Analytics.Tracks;
using F1Telemetry.App.TrackMaps;
using F1Telemetry.Core.Abstractions;
using F1Telemetry.Core.Models;
using F1Telemetry.Storage.Interfaces;
using F1Telemetry.Storage.Models;
using F1Telemetry.TTS.Services;
using F1Telemetry.Udp.Packets;

namespace F1Telemetry.App.ViewModels;

/// <summary>
/// Drives the V3 corner analysis page from persisted history session samples.
/// </summary>
public sealed class CornerAnalysisViewModel : ViewModelBase
{
    private const int MinimumReferenceLapSamples = 3;
    private const int MinimumVisualChartSamples = 3;
    private const int MinimumReasonableLapTimeMs = 30_000;
    private const int MaximumReasonableLapTimeMs = 600_000;
    private const float SignificantFuelDifferenceLitres = 5f;
    private const double VisualChartWidth = 220d;
    private const double VisualChartHeight = 72d;
    private const double TrackMapCanvasWidth = 240d;
    private const double TrackMapCanvasHeight = 170d;
    private const int PositionIndicatorSegments = 10;
    private readonly ILapSampleRepository _lapSampleRepository;
    private readonly IEventRepository? _eventRepository;
    private readonly ITrackSegmentMapProvider _trackSegmentMapProvider;
    private readonly CornerMetricsExtractor _cornerMetricsExtractor;
    private readonly TrackMapBuilder _trackMapBuilder;
    private readonly ITrackMapTrajectoryStore? _trackMapTrajectoryStore;
    private readonly IAIAnalysisService? _aiAnalysisService;
    private readonly IAppSettingsStore? _settingsStore;
    private readonly TtsMessageFactory? _ttsMessageFactory;
    private readonly TtsQueue? _ttsQueue;
    private readonly RelayCommand _refreshCommand;
    private readonly RelayCommand _generateEngineerAdviceCommand;
    private LapSummaryItemViewModel? _selectedLap;
    private LapSummaryItemViewModel? _selectedReferenceLap;
    private CornerSummaryRowViewModel? _selectedCorner;
    private int? _selectedLapNumber;
    private bool _isLoading;
    private bool _isEngineerAdviceLoading;
    private string _statusText = "请选择历史会话后刷新弯角分析。";
    private string _emptyStateText = "等待历史会话和圈采样数据。";
    private string _errorMessage = string.Empty;
    private string _analysisTimeText = "-";
    private string _totalTimeLossText = "-";
    private string _netTimeDeltaText = "-";
    private string _weakestCornerText = "-";
    private string _bestConfidenceCornerText = "-";
    private string _dataQualityText = "-";
    private string _dataQualityReasonText = "-";
    private string _referenceStatusText = "-";
    private string _engineerAdviceStatusText = "等待 AI 弯角分析。";
    private string _engineerAdviceNoticeText = "等待 AI 弯角分析。";
    private string _engineerPrimaryProblemText = "等待 AI 弯角分析。";
    private string _engineerDrivingActionText = "刷新弯角分析后生成驾驶动作。";
    private string _engineerNextLapFocusText = "刷新弯角分析后生成下圈重点。";
    private string _aiAnnotationText = "刷新弯角分析后生成 AI 工程师建议。";
    private CornerReferenceInfo _referenceInfo = CornerReferenceInfo.None();
    private CornerEngineerAdviceState _engineerAdviceState = CornerEngineerAdviceState.Ready;

    /// <summary>
    /// Initializes a new corner analysis ViewModel.
    /// </summary>
    /// <param name="historyBrowser">The shared history browser.</param>
    /// <param name="lapSampleRepository">The stored lap sample repository.</param>
    /// <param name="eventRepository">Optional event repository used to avoid flag-affected reference laps.</param>
    /// <param name="trackSegmentMapProvider">Optional segment map provider.</param>
    /// <param name="cornerMetricsExtractor">Optional metrics extractor.</param>
    /// <param name="aiAnalysisService">Optional AI analysis service for engineer advice.</param>
    /// <param name="settingsStore">Optional settings store used to load AI/TTS options.</param>
    /// <param name="ttsMessageFactory">Optional TTS message factory for AI advice speech.</param>
    /// <param name="ttsQueue">Optional TTS queue used to speak AI advice.</param>
    /// <param name="trackMapTrajectoryStore">Optional live Motion trajectory store used for track-map rendering.</param>
    public CornerAnalysisViewModel(
        HistorySessionBrowserViewModel historyBrowser,
        ILapSampleRepository lapSampleRepository,
        IEventRepository? eventRepository = null,
        ITrackSegmentMapProvider? trackSegmentMapProvider = null,
        CornerMetricsExtractor? cornerMetricsExtractor = null,
        IAIAnalysisService? aiAnalysisService = null,
        IAppSettingsStore? settingsStore = null,
        TtsMessageFactory? ttsMessageFactory = null,
        TtsQueue? ttsQueue = null,
        ITrackMapTrajectoryStore? trackMapTrajectoryStore = null)
    {
        HistoryBrowser = historyBrowser ?? throw new ArgumentNullException(nameof(historyBrowser));
        _lapSampleRepository = lapSampleRepository ?? throw new ArgumentNullException(nameof(lapSampleRepository));
        _eventRepository = eventRepository;
        _trackSegmentMapProvider = trackSegmentMapProvider ?? new StaticTrackSegmentMapProvider();
        _cornerMetricsExtractor = cornerMetricsExtractor ?? new CornerMetricsExtractor();
        _trackMapBuilder = new TrackMapBuilder();
        _trackMapTrajectoryStore = trackMapTrajectoryStore;
        _aiAnalysisService = aiAnalysisService;
        _settingsStore = settingsStore;
        _ttsMessageFactory = ttsMessageFactory;
        _ttsQueue = ttsQueue;
        CornerRows = new ObservableCollection<CornerSummaryRowViewModel>();
        EngineerSuggestions = new ObservableCollection<string>();
        _refreshCommand = new RelayCommand(() => _ = RefreshAsync(), () => !IsLoading && !IsEngineerAdviceLoading);
        _generateEngineerAdviceCommand = new RelayCommand(
            () => _ = GenerateEngineerAdviceAsync(CancellationToken.None),
            () => CanGenerateEngineerAdvice);
        CornerRows.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasCornerRows));
            OnPropertyChanged(nameof(CanGenerateEngineerAdvice));
            if (CornerRows.Count == 0 && !IsEngineerAdviceLoading)
            {
                SetEngineerAdviceState(CornerEngineerAdviceState.InsufficientData);
            }
            else if (CornerRows.Count > 0 && !IsEngineerAdviceLoading && _engineerAdviceState == CornerEngineerAdviceState.InsufficientData)
            {
                SetEngineerAdviceState(CornerEngineerAdviceState.Ready);
            }

            _generateEngineerAdviceCommand.RaiseCanExecuteChanged();
        };
        HistoryBrowser.HistoryLaps.CollectionChanged += OnHistoryLapsChanged;
        HistoryBrowser.PropertyChanged += OnHistoryBrowserPropertyChanged;
    }

    /// <summary>
    /// Gets the command that refreshes corner analysis.
    /// </summary>
    public ICommand RefreshCommand => _refreshCommand;

    /// <summary>
    /// Gets the command that regenerates AI engineer advice for the current corner rows.
    /// </summary>
    public ICommand GenerateEngineerAdviceCommand => _generateEngineerAdviceCommand;

    /// <summary>
    /// Gets the shared history browser.
    /// </summary>
    public HistorySessionBrowserViewModel HistoryBrowser { get; }

    /// <summary>
    /// Gets corner analysis rows.
    /// </summary>
    public ObservableCollection<CornerSummaryRowViewModel> CornerRows { get; }

    /// <summary>
    /// Gets AI-generated engineer suggestions for the current corner analysis.
    /// </summary>
    public ObservableCollection<string> EngineerSuggestions { get; }

    /// <summary>
    /// Gets a value indicating whether any corner rows are available.
    /// </summary>
    public bool HasCornerRows => CornerRows.Count > 0;

    /// <summary>
    /// Gets or sets the selected historical lap for corner analysis.
    /// </summary>
    public LapSummaryItemViewModel? SelectedLap
    {
        get => _selectedLap;
        set
        {
            if (SetProperty(ref _selectedLap, value))
            {
                _selectedLapNumber = value?.LapNumber;
                OnPropertyChanged(nameof(SelectionText));
                OnPropertyChanged(nameof(HasReferenceLapChoices));
                OnPropertyChanged(nameof(ReferencePickerText));
                _generateEngineerAdviceCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the manually selected reference lap for corner comparison.
    /// </summary>
    public LapSummaryItemViewModel? SelectedReferenceLap
    {
        get => _selectedReferenceLap;
        set
        {
            if (SetProperty(ref _selectedReferenceLap, value))
            {
                if (value is not null)
                {
                    ReferenceInfo = new CornerReferenceInfo
                    {
                        LapNumber = value.LapNumber,
                        Source = ReferenceLapSource.Manual,
                        LapTimeMs = value.LapTimeInMs,
                        Compound = value.CompoundText,
                        Confidence = ConfidenceLevel.Medium,
                        WarningText = "手动参考圈，刷新分析后更新对比。"
                    };
                }

                OnPropertyChanged(nameof(ReferencePickerText));
                _generateEngineerAdviceCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the selected corner row shown in the detail panel.
    /// </summary>
    public CornerSummaryRowViewModel? SelectedCorner
    {
        get => _selectedCorner;
        set => SetProperty(ref _selectedCorner, value);
    }

    /// <summary>
    /// Gets the currently selected session and lap summary.
    /// </summary>
    public string SelectionText
    {
        get
        {
            var sessionText = HistoryBrowser.SelectedSession?.SummaryText ?? "未选择会话";
            var lapText = SelectedLap?.LapText ?? "未选择圈";
            return $"{sessionText} · {lapText}";
        }
    }

    /// <summary>
    /// Gets a value indicating whether data is loading.
    /// </summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                _refreshCommand.RaiseCanExecuteChanged();
                _generateEngineerAdviceCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(CanGenerateEngineerAdvice));
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether AI engineer advice is loading.
    /// </summary>
    public bool IsEngineerAdviceLoading
    {
        get => _isEngineerAdviceLoading;
        private set
        {
            if (SetProperty(ref _isEngineerAdviceLoading, value))
            {
                _refreshCommand.RaiseCanExecuteChanged();
                _generateEngineerAdviceCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(EngineerAdviceButtonText));
                OnPropertyChanged(nameof(CanGenerateEngineerAdvice));
            }
        }
    }

    /// <summary>
    /// Gets the engineer-advice button text.
    /// </summary>
    public string EngineerAdviceButtonText => _engineerAdviceState switch
    {
        CornerEngineerAdviceState.Loading => "AI分析中...",
        CornerEngineerAdviceState.Generated => "已生成",
        CornerEngineerAdviceState.InsufficientData => "数据不足",
        CornerEngineerAdviceState.Failed => "生成失败",
        _ => "AI建议"
    };

    /// <summary>
    /// Gets a value indicating whether AI advice can be requested now.
    /// </summary>
    public bool CanGenerateEngineerAdvice => !IsLoading && !IsEngineerAdviceLoading && CornerRows.Count > 0;

    /// <summary>
    /// Gets the page status text.
    /// </summary>
    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    /// <summary>
    /// Gets the empty-state text.
    /// </summary>
    public string EmptyStateText
    {
        get => _emptyStateText;
        private set => SetProperty(ref _emptyStateText, value);
    }

    /// <summary>
    /// Gets the latest corner-analysis loading error, when available.
    /// </summary>
    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    /// <summary>
    /// Gets the time label for the latest analysis run.
    /// </summary>
    public string AnalysisTimeText
    {
        get => _analysisTimeText;
        private set => SetProperty(ref _analysisTimeText, value);
    }

    /// <summary>
    /// Gets the aggregate positive time loss summary.
    /// </summary>
    public string TotalTimeLossText
    {
        get => _totalTimeLossText;
        private set => SetProperty(ref _totalTimeLossText, value);
    }

    /// <summary>
    /// Gets the net signed corner time delta summary.
    /// </summary>
    public string NetTimeDeltaText
    {
        get => _netTimeDeltaText;
        private set => SetProperty(ref _netTimeDeltaText, value);
    }

    /// <summary>
    /// Gets the weakest corner summary.
    /// </summary>
    public string WeakestCornerText
    {
        get => _weakestCornerText;
        private set => SetProperty(ref _weakestCornerText, value);
    }

    /// <summary>
    /// Gets the best confidence corner summary.
    /// </summary>
    public string BestConfidenceCornerText
    {
        get => _bestConfidenceCornerText;
        private set => SetProperty(ref _bestConfidenceCornerText, value);
    }

    /// <summary>
    /// Gets the overall data-quality label.
    /// </summary>
    public string DataQualityText
    {
        get => _dataQualityText;
        private set => SetProperty(ref _dataQualityText, value);
    }

    /// <summary>
    /// Gets the overall data-quality reason.
    /// </summary>
    public string DataQualityReasonText
    {
        get => _dataQualityReasonText;
        private set => SetProperty(ref _dataQualityReasonText, value);
    }

    /// <summary>
    /// Gets the reference data status summary.
    /// </summary>
    public string ReferenceStatusText
    {
        get => _referenceStatusText;
        private set => SetProperty(ref _referenceStatusText, value);
    }

    /// <summary>
    /// Gets the reference lap used by the latest corner analysis.
    /// </summary>
    public CornerReferenceInfo ReferenceInfo
    {
        get => _referenceInfo;
        private set
        {
            SetProperty(ref _referenceInfo, value);
            ReferenceStatusText = value.LapText;
            OnPropertyChanged(nameof(ReferencePickerText));
        }
    }

    /// <summary>
    /// Gets a value indicating whether the reference picker has any candidate laps.
    /// </summary>
    public bool HasReferenceLapChoices => HistoryBrowser.HistoryLaps.Any(lap => SelectedLap is null || lap.LapNumber != SelectedLap.LapNumber);

    /// <summary>
    /// Gets the text shown in the reference picker when automatic selection is active.
    /// </summary>
    public string ReferencePickerText
    {
        get
        {
            if (!HasReferenceLapChoices)
            {
                return "暂无可用参考圈";
            }

            if (SelectedReferenceLap is not null)
            {
                return $"手动参考圈：{SelectedReferenceLap.LapText}";
            }

            return ReferenceInfo.HasReference
                ? $"自动参考圈：{ReferenceInfo.LapText}"
                : "暂无可用参考圈";
        }
    }

    /// <summary>
    /// Gets the AI engineer advice loading and TTS status.
    /// </summary>
    public string EngineerAdviceStatusText
    {
        get => _engineerAdviceStatusText;
        private set => SetProperty(ref _engineerAdviceStatusText, value);
    }

    /// <summary>
    /// Gets the engineer-advice status notice shown above the structured cards.
    /// </summary>
    public string EngineerAdviceNoticeText
    {
        get => _engineerAdviceNoticeText;
        private set => SetProperty(ref _engineerAdviceNoticeText, value);
    }

    /// <summary>
    /// Gets the primary problem from the latest engineer advice.
    /// </summary>
    public string EngineerPrimaryProblemText
    {
        get => _engineerPrimaryProblemText;
        private set => SetProperty(ref _engineerPrimaryProblemText, value);
    }

    /// <summary>
    /// Gets the concrete driving action from the latest engineer advice.
    /// </summary>
    public string EngineerDrivingActionText
    {
        get => _engineerDrivingActionText;
        private set => SetProperty(ref _engineerDrivingActionText, value);
    }

    /// <summary>
    /// Gets the next-lap focus from the latest engineer advice.
    /// </summary>
    public string EngineerNextLapFocusText
    {
        get => _engineerNextLapFocusText;
        private set => SetProperty(ref _engineerNextLapFocusText, value);
    }

    /// <summary>
    /// Gets the detailed AI analysis note shown below the corner view.
    /// </summary>
    public string AiAnnotationText
    {
        get => _aiAnnotationText;
        private set => SetProperty(ref _aiAnnotationText, value);
    }

    /// <summary>
    /// Refreshes the selected session corner analysis.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        CornerRows.Clear();
        EngineerSuggestions.Clear();
        SelectedCorner = null;
        ResetDashboardSummaries();
        ErrorMessage = string.Empty;
        EngineerAdviceStatusText = "等待 AI 弯角分析。";
        ResetEngineerAdviceCards();
        AiAnnotationText = "刷新弯角分析后生成 AI 工程师建议。";
        SetEngineerAdviceState(CornerEngineerAdviceState.InsufficientData);
        try
        {
            if (HistoryBrowser.SelectedSession is null)
            {
                await HistoryBrowser.RefreshSessionsAsync(cancellationToken);
            }

            var session = HistoryBrowser.SelectedSession;
            if (session is null)
            {
                EmptyStateText = "没有可用于弯角分析的历史会话。";
                StatusText = EmptyStateText;
                return;
            }

            await HistoryBrowser.LoadSelectedSessionLapsAsync(cancellationToken);
            SelectDefaultLapIfNeeded();

            var lapNumber = SelectedLap?.LapNumber;
            if (lapNumber is null)
            {
                EmptyStateText = "该会话没有可用于弯角分析的历史圈记录。";
                StatusText = EmptyStateText;
                return;
            }

            var map = _trackSegmentMapProvider.GetMap(NormalizeTrackId(session.TrackId));
            if (map.Status == TrackSegmentMapStatus.Unsupported)
            {
                EmptyStateText = "该赛道暂未支持弯角分析。";
                StatusText = map.StatusReason ?? EmptyStateText;
                return;
            }

            var storedSamples = await _lapSampleRepository.GetForLapAsync(session.SessionId, lapNumber.Value, cancellationToken);
            if (storedSamples.Count == 0)
            {
                EmptyStateText = "该圈没有保存高频采样，无法生成弯角分析。";
                StatusText = EmptyStateText;
                return;
            }

            var referenceSelection = await ResolveReferenceLapAsync(
                session.SessionId,
                lapNumber.Value,
                storedSamples,
                cancellationToken);
            var lapSamples = storedSamples.Select(ToLapSample).ToArray();
            var referenceSamples = referenceSelection.Samples.Count == 0
                ? null
                : referenceSelection.Samples.Select(ToLapSample).ToArray();
            var trackMapSnapshot = ResolveTrackMapSnapshot(
                session,
                lapNumber.Value);
            var result = _cornerMetricsExtractor.Extract(map, lapSamples, referenceSamples);
            foreach (var summary in result.Corners)
            {
                var referenceMetrics = BuildReferenceCornerMetrics(summary.Segment, referenceSamples);
                var visualEvidence = BuildVisualEvidence(summary.Segment, map, lapSamples, referenceSamples, trackMapSnapshot);
                var row = CornerSummaryRowViewModel.FromSummary(
                    summary,
                    referenceMetrics?.EntrySpeedKph,
                    referenceMetrics?.MinimumSpeedKph,
                    referenceMetrics?.ExitSpeedKph,
                    referenceMetrics?.MaxBrake,
                    visualEvidence.Speed.CurrentPathData,
                    visualEvidence.Speed.ReferencePathData,
                    visualEvidence.Speed.StatusText,
                    visualEvidence.Brake.CurrentPathData,
                    visualEvidence.Brake.ReferencePathData,
                    visualEvidence.Brake.StatusText,
                    visualEvidence.Throttle.CurrentPathData,
                    visualEvidence.Throttle.ReferencePathData,
                    visualEvidence.Throttle.StatusText,
                    visualEvidence.Position.IndicatorText,
                    visualEvidence.Position.StatusText,
                    visualEvidence.TrackMap.PathData,
                    visualEvidence.TrackMap.HighlightPathData,
                    visualEvidence.TrackMap.StatusText,
                    visualEvidence.TrackMap.SourceText,
                    visualEvidence.TrackMap.QualityText,
                    visualEvidence.TrackMap.WarningText,
                    visualEvidence.TrackMap.EmptyStateText,
                    visualEvidence.TrackMap.MarkerX,
                    visualEvidence.TrackMap.MarkerY,
                    visualEvidence.TrackMap.MarkerSize,
                    visualEvidence.TrackMap.CornerLabelText);
                CornerRows.Add(row);
            }

            EmptyStateText = CornerRows.Count == 0 ? "没有可显示的弯角摘要。" : string.Empty;
            UpdateDashboardSummaries(result, session, lapNumber.Value, referenceSelection.Info);
            StatusText = $"已生成 {session.TrackText} {session.SessionTypeText} Lap {lapNumber.Value} 弯角分析：{CornerRows.Count} 个弯角，置信度 {result.Confidence}。";
            if (CornerRows.Count > 0)
            {
                SetEngineerAdviceState(CornerEngineerAdviceState.Ready);
                await GenerateEngineerAdviceAsync(session, SelectedLap, result, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            EmptyStateText = "弯角数据加载已取消。";
            StatusText = EmptyStateText;
            ErrorMessage = EmptyStateText;
        }
        catch (Exception ex)
        {
            CornerRows.Clear();
            EmptyStateText = "弯角数据加载失败。";
            StatusText = EmptyStateText;
            ErrorMessage = $"弯角数据加载失败：{ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Regenerates AI engineer advice for the currently loaded corner rows.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task GenerateEngineerAdviceAsync(CancellationToken cancellationToken = default)
    {
        return GenerateEngineerAdviceAsync(
            HistoryBrowser.SelectedSession,
            SelectedLap,
            null,
            cancellationToken);
    }

    private void SetEngineerAdviceState(CornerEngineerAdviceState state)
    {
        if (_engineerAdviceState == state)
        {
            return;
        }

        _engineerAdviceState = state;
        OnPropertyChanged(nameof(EngineerAdviceButtonText));
        OnPropertyChanged(nameof(CanGenerateEngineerAdvice));
        _generateEngineerAdviceCommand.RaiseCanExecuteChanged();
    }

    private void ResetEngineerAdviceCards()
    {
        EngineerAdviceNoticeText = "等待 AI 弯角分析。";
        EngineerPrimaryProblemText = "等待 AI 弯角分析。";
        EngineerDrivingActionText = "刷新弯角分析后生成驾驶动作。";
        EngineerNextLapFocusText = "刷新弯角分析后生成下圈重点。";
    }

    private void SetEngineerAdviceLoadingCards(bool hasLimitedData)
    {
        EngineerAdviceNoticeText = hasLimitedData
            ? "参考圈或采样不足，建议仅供参考"
            : "AI 正在分析弯角证据。";
        EngineerPrimaryProblemText = "AI 分析中...";
        EngineerDrivingActionText = "正在整理驾驶动作。";
        EngineerNextLapFocusText = "正在生成下圈重点。";
    }

    private void SetEngineerAdviceInsufficientCards()
    {
        EngineerAdviceNoticeText = "数据不足，无法生成建议";
        EngineerPrimaryProblemText = "数据不足，无法生成建议。";
        EngineerDrivingActionText = "先补齐参考圈或 LapSample 后再复盘。";
        EngineerNextLapFocusText = "下一圈先稳定完成有效圈。";
    }

    private void SetEngineerAdviceFailureCards(string failureText)
    {
        EngineerAdviceNoticeText = failureText;
        EngineerPrimaryProblemText = "AI 生成失败。";
        EngineerDrivingActionText = failureText;
        EngineerNextLapFocusText = "修复配置或网络后可重新生成。";
    }

    private void SetEngineerAdviceGeneratedCards(
        AIAnalysisResult aiResult,
        IReadOnlyList<string> suggestions,
        bool hasLimitedData)
    {
        EngineerAdviceNoticeText = hasLimitedData
            ? "参考圈或采样不足，建议仅供参考"
            : "已生成结构化工程师建议。";
        EngineerPrimaryProblemText = FirstMeaningful(
            aiResult.KeyProblems.Concat([aiResult.Summary, AiAnnotationText]))
            ?? "主要问题未明确，先按时间差最大的弯角复盘。";
        EngineerDrivingActionText = FirstMeaningful(
            suggestions
                .Concat([aiResult.StrategyReview, aiResult.TyreReview, aiResult.Summary]))
            ?? "入弯先稳定刹车，再逐步打开油门。";
        EngineerNextLapFocusText = FirstMeaningful(
            suggestions
                .Skip(1)
                .Concat([aiResult.OpponentReview, aiResult.ErsFuelReview, aiResult.Summary]))
            ?? "下一圈优先复核最弱弯角的刹车点和出弯油门。";
    }

    private async Task GenerateEngineerAdviceAsync(
        HistorySessionItemViewModel? session,
        LapSummaryItemViewModel? lap,
        CornerMetricsResult? result,
        CancellationToken cancellationToken)
    {
        if (IsEngineerAdviceLoading)
        {
            return;
        }

        if (session is null || lap is null || CornerRows.Count == 0)
        {
            SetEngineerAdviceState(CornerEngineerAdviceState.InsufficientData);
            EngineerAdviceStatusText = "数据不足，无法生成建议。";
            SetEngineerAdviceInsufficientCards();
            return;
        }

        if (_aiAnalysisService is null || _settingsStore is null)
        {
            SetEngineerAdviceState(CornerEngineerAdviceState.Failed);
            EngineerAdviceStatusText = "生成失败：未接入 AI 服务。";
            SetEngineerAdviceFailureCards("生成失败：未接入 AI 服务。");
            return;
        }

        IsEngineerAdviceLoading = true;
        SetEngineerAdviceState(CornerEngineerAdviceState.Loading);
        var hasLimitedData = HasLimitedCornerData();
        SetEngineerAdviceLoadingCards(hasLimitedData);
        EngineerAdviceStatusText = hasLimitedData
            ? "AI分析中，当前数据不足，建议仅供参考。"
            : "AI分析中，请稍候。";
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(20));
            var settings = await _settingsStore.LoadAsync(timeoutCts.Token);
            if (!settings.Ai.AiEnabled)
            {
                SetEngineerAdviceState(CornerEngineerAdviceState.Failed);
                EngineerAdviceStatusText = "生成失败：AI未启用。";
                SetEngineerAdviceFailureCards("生成失败：AI未启用。");
                return;
            }

            if (string.IsNullOrWhiteSpace(settings.Ai.ApiKey))
            {
                SetEngineerAdviceState(CornerEngineerAdviceState.Failed);
                EngineerAdviceStatusText = "生成失败：API Key 未配置";
                SetEngineerAdviceFailureCards("API Key 未配置");
                return;
            }

            var context = BuildEngineerAdviceContext(session, lap, result);
            var aiResult = await _aiAnalysisService.AnalyzeAsync(context, settings.Ai, timeoutCts.Token);
            if (!aiResult.IsSuccess)
            {
                SetEngineerAdviceState(CornerEngineerAdviceState.Failed);
                var failureText = $"生成失败：{ResolveFailureMessage(aiResult.ErrorMessage)}";
                EngineerAdviceStatusText = failureText;
                SetEngineerAdviceFailureCards(failureText);
                return;
            }

            var suggestions = aiResult.Improvements
                .Where(value => !string.IsNullOrWhiteSpace(value) && value.Trim() != "-")
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .Take(4)
                .ToArray();

            EngineerSuggestions.Clear();
            foreach (var suggestion in suggestions)
            {
                EngineerSuggestions.Add(suggestion);
            }

            AiAnnotationText = BuildAiAnnotationText(aiResult);
            SetEngineerAdviceGeneratedCards(aiResult, suggestions, hasLimitedData);
            SetEngineerAdviceState(CornerEngineerAdviceState.Generated);
            EngineerAdviceStatusText = suggestions.Length == 0
                ? "已生成，但没有可显示的工程师建议。"
                : BuildEngineerAdviceTtsStatus(session, lap, aiResult, settings.Tts, hasLimitedData);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            SetEngineerAdviceState(CornerEngineerAdviceState.Failed);
            EngineerAdviceStatusText = "生成失败：请求超时";
            SetEngineerAdviceFailureCards("请求超时");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            SetEngineerAdviceState(CornerEngineerAdviceState.Failed);
            EngineerAdviceStatusText = "生成失败：AI建议生成已取消。";
            SetEngineerAdviceFailureCards("生成失败：AI建议生成已取消。");
        }
        catch (Exception ex)
        {
            SetEngineerAdviceState(CornerEngineerAdviceState.Failed);
            EngineerAdviceStatusText = $"生成失败：{ex.Message}";
            SetEngineerAdviceFailureCards($"生成失败：{ex.Message}");
        }
        finally
        {
            IsEngineerAdviceLoading = false;
        }
    }

    private string BuildEngineerAdviceTtsStatus(
        HistorySessionItemViewModel session,
        LapSummaryItemViewModel lap,
        AIAnalysisResult aiResult,
        F1Telemetry.TTS.Models.TtsOptions ttsOptions,
        bool hasLimitedData)
    {
        var prefix = hasLimitedData ? "已生成，当前数据不足，建议仅供参考。" : "已生成。";
        if (!ttsOptions.TtsEnabled)
        {
            return $"{prefix}TTS未启用。";
        }

        if (_ttsMessageFactory is null || _ttsQueue is null)
        {
            return $"{prefix}未接入 TTS 队列。";
        }

        var speechText = ResolveEngineerAdviceSpeechText(aiResult);
        var message = _ttsMessageFactory.CreateForEngineerAdvice(
            $"{session.SessionId}:lap{lap.LapNumber.ToString(CultureInfo.InvariantCulture)}",
            speechText,
            ttsOptions);
        if (message is null)
        {
            return $"{prefix}TTS冷却中未重复播报。";
        }

        _ttsQueue.UpdateOptions(ttsOptions);
        return _ttsQueue.TryEnqueue(message)
            ? $"{prefix}TTS已加入播报队列。"
            : $"{prefix}TTS队列暂未接受播报。";
    }

    private bool HasLimitedCornerData()
    {
        return CornerRows.Any(row =>
            row.ConfidenceText is nameof(ConfidenceLevel.Low) or nameof(ConfidenceLevel.Unknown) ||
            !string.Equals(row.CompactWarningText, "OK", StringComparison.Ordinal));
    }

    private static string ResolveFailureMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "AI 返回格式无效";
        }

        var normalized = message.Trim();
        if (string.Equals(normalized, AIErrorMessageFormatter.MissingApiKey, StringComparison.Ordinal)
            || normalized.Contains("API Key", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("apikey", StringComparison.OrdinalIgnoreCase))
        {
            return "API Key 未配置";
        }

        if (string.Equals(normalized, AIErrorMessageFormatter.NetworkError, StringComparison.Ordinal)
            || normalized.Contains("网络错误", StringComparison.Ordinal)
            || normalized.Contains("network", StringComparison.OrdinalIgnoreCase))
        {
            return "网络请求失败";
        }

        if (string.Equals(normalized, AIErrorMessageFormatter.ParseFailure, StringComparison.Ordinal)
            || normalized.Contains("解析失败", StringComparison.Ordinal)
            || normalized.Contains("parse", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("format", StringComparison.OrdinalIgnoreCase))
        {
            return "AI 返回格式无效";
        }

        if (normalized.Contains("timeout", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("超时", StringComparison.Ordinal))
        {
            return "请求超时";
        }

        if (normalized.Contains("数据不足", StringComparison.Ordinal))
        {
            return "数据不足，无法生成建议";
        }

        return normalized;
    }

    private AIAnalysisContext BuildEngineerAdviceContext(
        HistorySessionItemViewModel session,
        LapSummaryItemViewModel lap,
        CornerMetricsResult? result)
    {
        return new AIAnalysisContext
        {
            SessionMode = SessionMode.Unknown,
            SessionTypeText = session.SessionTypeText,
            SessionFocusText = "弯角复盘：只给驾驶工程师建议，重点关注入弯速度、刹车峰值、出弯速度、时间损失和数据质量。",
            LatestLap = new LapSummary
            {
                LapNumber = lap.LapNumber,
                IsValid = true,
                ClosedAt = DateTimeOffset.Now
            },
            RecentLaps =
            [
                new LapSummary
                {
                    LapNumber = lap.LapNumber,
                    IsValid = true,
                    ClosedAt = DateTimeOffset.Now
                }
            ],
            HistoricalSessionSummary = $"{session.SummaryText} · {lap.LapText}",
            TelemetryAnalysisSummary = BuildCornerTelemetrySummary(result),
            RecentEvents = BuildCornerAdviceEventSummaries()
        };
    }

    private string BuildCornerTelemetrySummary(CornerMetricsResult? result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Corner analysis for AI engineer advice. Do not invent missing values.");
        builder.AppendLine($"Track/session/lap: {SelectionText}");
        builder.AppendLine($"Result confidence: {result?.Confidence.ToString() ?? ReferenceStatusText}");
        builder.AppendLine($"Reference lap: {ReferenceInfo.LapText}, source {ReferenceInfo.SourceText}, quality {ReferenceInfo.QualityText}, note {ReferenceInfo.WarningText}");
        builder.AppendLine($"Positive time-loss total: {TotalTimeLossText}");
        builder.AppendLine($"Net time delta: {NetTimeDeltaText}");
        builder.AppendLine($"Weakest corner: {WeakestCornerText}");
        builder.AppendLine("Rows:");

        foreach (var row in CornerRows)
        {
            builder.Append("  - ");
            builder.Append(row.CornerText);
            builder.Append(", min ");
            builder.Append(row.MinimumSpeedText);
            builder.Append(", entry/exit ");
            builder.Append(row.SpeedWindowText);
            builder.Append(", brake ");
            builder.Append(row.BrakeText);
            builder.Append(", loss ");
            builder.Append(row.TimeLossText);
            builder.Append(", confidence ");
            builder.Append(row.ConfidenceText);
            builder.Append(", warnings ");
            builder.AppendLine(row.WarningText);
        }

        builder.Append("Return concrete improvements only for these corners. The tts field must be one short Chinese engineer callout.");
        return builder.ToString();
    }

    private IReadOnlyList<string> BuildCornerAdviceEventSummaries()
    {
        return CornerRows
            .Where(row => row.WarningText != "-")
            .Select(row => $"{row.CornerText} 数据限制：{row.WarningText}")
            .Take(5)
            .ToArray();
    }

    private static string BuildAiAnnotationText(AIAnalysisResult result)
    {
        foreach (var candidate in new[] { result.Summary, result.StrategyReview, result.TyreReview })
        {
            if (!string.IsNullOrWhiteSpace(candidate) && candidate.Trim() != "-")
            {
                return candidate.Trim();
            }
        }

        return "AI已完成弯角分析，建议见工程师建议。";
    }

    private static string? FirstMeaningful(IEnumerable<string> candidates)
    {
        return candidates
            .Select(value => value?.Trim())
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value) && value != "-");
    }

    private static string ResolveEngineerAdviceSpeechText(AIAnalysisResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.Tts) && result.Tts.Trim() != "-")
        {
            return result.Tts.Trim();
        }

        if (!string.IsNullOrWhiteSpace(result.TtsText) && result.TtsText.Trim() != "-")
        {
            return result.TtsText.Trim();
        }

        return result.Improvements.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
    }

    private void ResetDashboardSummaries()
    {
        AnalysisTimeText = "-";
        TotalTimeLossText = "-";
        NetTimeDeltaText = "-";
        WeakestCornerText = "-";
        BestConfidenceCornerText = "-";
        DataQualityText = "-";
        DataQualityReasonText = "-";
        ReferenceInfo = CornerReferenceInfo.None("缺少可用参考圈");
    }

    private void UpdateDashboardSummaries(
        CornerMetricsResult result,
        HistorySessionItemViewModel session,
        int lapNumber,
        CornerReferenceInfo referenceInfo)
    {
        var rows = CornerRows.ToArray();
        var totalLoss = rows.Sum(row => row.PositiveTimeLossInMs);
        var netTimeDelta = rows
            .Where(row => row.TimeLossInMs is not null)
            .Sum(row => row.TimeLossInMs!.Value);
        var weakestCorner = rows
            .Where(row => row.PositiveTimeLossInMs > 0)
            .OrderByDescending(row => row.PositiveTimeLossInMs)
            .ThenBy(row => row.CornerNumber ?? int.MaxValue)
            .FirstOrDefault();

        AnalysisTimeText = DateTimeOffset.Now.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        TotalTimeLossText = FormatPositiveSeconds(totalLoss);
        NetTimeDeltaText = FormatSignedSeconds(netTimeDelta);
        WeakestCornerText = weakestCorner is null ? "没有明显损失" : $"{weakestCorner.CornerText} · {FormatLossSeconds(weakestCorner.TimeLossInMs)}";
        BestConfidenceCornerText = "-";
        DataQualityText = result.Confidence.ToString();
        DataQualityReasonText = BuildDataQualityReasonText(result, referenceInfo);
        ReferenceInfo = referenceInfo;
        SelectedCorner = weakestCorner ?? rows.FirstOrDefault();

        if (rows.Length > 0)
        {
            StatusText = $"已生成 {session.TrackText} {session.SessionTypeText} Lap {lapNumber} 弯角分析：{rows.Length} 个弯角，置信度 {result.Confidence}。";
        }
    }

    private static string BuildDataQualityReasonText(CornerMetricsResult result, CornerReferenceInfo referenceInfo)
    {
        var reasons = result.Warnings
            .Select(FormatDataQualityReason)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (!referenceInfo.HasReference)
        {
            reasons.Add("缺少可用参考圈");
        }
        else if (referenceInfo.Confidence is ConfidenceLevel.Low or ConfidenceLevel.Medium &&
                 !string.IsNullOrWhiteSpace(referenceInfo.WarningText))
        {
            reasons.Add(referenceInfo.WarningText);
        }

        return reasons.Count == 0 ? "数据质量良好" : string.Join(" / ", reasons.Distinct(StringComparer.Ordinal));
    }

    private static string FormatDataQualityReason(DataQualityWarning warning)
    {
        return warning switch
        {
            DataQualityWarning.EstimatedTrackMap => "使用估算赛道图",
            DataQualityWarning.MissingReferenceLap => "缺少参考圈",
            DataQualityWarning.LowSampleDensity => "采样偏少",
            DataQualityWarning.MissingSamples => "缺少采样",
            DataQualityWarning.MissingLapDistance => "缺少距离采样",
            DataQualityWarning.MissingTimingSamples => "缺少计时采样",
            DataQualityWarning.MissingSpeedSamples => "缺少速度采样",
            DataQualityWarning.MissingThrottleSamples => "缺少油门采样",
            DataQualityWarning.MissingBrakeSamples => "缺少刹车采样",
            DataQualityWarning.MissingSteeringSamples => "缺少转向采样",
            DataQualityWarning.UnsupportedTrack => "赛道暂不支持",
            _ => warning.ToString()
        };
    }

    private static string FormatLossSeconds(int? timeLossInMs)
    {
        return timeLossInMs is null ? "缺少参考" : $"{timeLossInMs.Value / 1000d:+0.000;-0.000;0.000}s";
    }

    private static string FormatPositiveSeconds(int timeInMs)
    {
        return timeInMs > 0 ? $"+{timeInMs / 1000d:0.000}s" : "0.000s";
    }

    private static string FormatSignedSeconds(int timeInMs)
    {
        return $"{timeInMs / 1000d:+0.000;-0.000;0.000}s";
    }

    private TrackMapSnapshot ResolveTrackMapSnapshot(
        HistorySessionItemViewModel session,
        int selectedLapNumber)
    {
        return _trackMapTrajectoryStore?.GetPreferredOrBest(
                session.SessionUid,
                session.TrackId,
                selectedLapNumber)
            ?? TrackMapBuilder.CreateEmptySnapshot(
                session.SessionUid,
                session.TrackId,
                selectedLapNumber,
                TrackMapStatus.MissingMotionData,
                "该会话缺少 Motion 坐标");
    }

    private CornerVisualEvidence BuildVisualEvidence(
        TrackSegment segment,
        TrackSegmentMap map,
        IReadOnlyList<LapSample> currentSamples,
        IReadOnlyList<LapSample>? referenceSamples,
        TrackMapSnapshot trackMapSnapshot)
    {
        var currentSegmentSamples = SelectSegmentSamples(segment, currentSamples);
        var referenceSegmentSamples = referenceSamples is null
            ? Array.Empty<LapSample>()
            : SelectSegmentSamples(segment, referenceSamples);

        return new CornerVisualEvidence(
            BuildMetricChartEvidence(currentSegmentSamples, referenceSegmentSamples, referenceSamples is not null, sample => sample.SpeedKph),
            BuildMetricChartEvidence(currentSegmentSamples, referenceSegmentSamples, referenceSamples is not null, sample => sample.Brake),
            BuildMetricChartEvidence(currentSegmentSamples, referenceSegmentSamples, referenceSamples is not null, sample => sample.Throttle),
            BuildPositionEvidence(segment, map),
            BuildTrackMapEvidence(segment, map, trackMapSnapshot));
    }

    private static IReadOnlyList<LapSample> SelectSegmentSamples(
        TrackSegment segment,
        IReadOnlyList<LapSample> samples)
    {
        return samples
            .Where(sample => sample.LapDistance is not null && segment.ContainsDistance(sample.LapDistance.Value))
            .OrderBy(sample => sample.LapDistance)
            .ToArray();
    }

    private static CornerMetricChartEvidence BuildMetricChartEvidence(
        IReadOnlyList<LapSample> currentSamples,
        IReadOnlyList<LapSample> referenceSamples,
        bool hasReferenceLap,
        Func<LapSample, double?> metricSelector)
    {
        if (!hasReferenceLap)
        {
            return CornerMetricChartEvidence.MissingReference();
        }

        var currentValues = SelectMetricValues(currentSamples, metricSelector);
        var referenceValues = SelectMetricValues(referenceSamples, metricSelector);
        if (currentValues.Count < MinimumVisualChartSamples || referenceValues.Count < MinimumVisualChartSamples)
        {
            return CornerMetricChartEvidence.InsufficientSamples();
        }

        var min = Math.Min(currentValues.Min(), referenceValues.Min());
        var max = Math.Max(currentValues.Max(), referenceValues.Max());
        return new CornerMetricChartEvidence(
            BuildChartPathData(currentValues, min, max),
            BuildChartPathData(referenceValues, min, max),
            "当前圈 vs 参考圈");
    }

    private static IReadOnlyList<double> SelectMetricValues(
        IReadOnlyList<LapSample> samples,
        Func<LapSample, double?> metricSelector)
    {
        return samples
            .Select(metricSelector)
            .Where(value => value is not null)
            .Select(value => value!.Value)
            .ToArray();
    }

    private static string BuildChartPathData(
        IReadOnlyList<double> values,
        double min,
        double max)
    {
        var builder = new StringBuilder();
        var range = Math.Abs(max - min) < 0.001d ? 1d : max - min;
        for (var index = 0; index < values.Count; index++)
        {
            var x = values.Count == 1
                ? VisualChartWidth / 2d
                : index * VisualChartWidth / (values.Count - 1);
            var y = VisualChartHeight - ((values[index] - min) / range * VisualChartHeight);
            builder.Append(index == 0 ? "M " : " L ");
            builder.Append(x.ToString("0.##", CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(y.ToString("0.##", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    private static CornerPositionEvidence BuildPositionEvidence(TrackSegment segment, TrackSegmentMap map)
    {
        if (map.LapLengthMeters is not > 0)
        {
            return CornerPositionEvidence.Missing();
        }

        var midpoint = CalculateSegmentMidpoint(segment, map.LapLengthMeters.Value);
        var percent = Math.Clamp(midpoint / map.LapLengthMeters.Value * 100d, 0d, 100d);
        var markerIndex = Math.Clamp((int)Math.Round(percent / 100d * PositionIndicatorSegments), 0, PositionIndicatorSegments);
        var beforeMarker = new string('━', markerIndex);
        var afterMarker = new string('━', PositionIndicatorSegments - markerIndex);
        var cornerText = segment.CornerNumber is null
            ? segment.Name
            : $"T{segment.CornerNumber.Value.ToString(CultureInfo.InvariantCulture)} {segment.Name}";
        var status = map.Status == TrackSegmentMapStatus.Estimated ||
                     map.Warnings.Contains(DataQualityWarning.EstimatedTrackMap) ||
                     segment.Warnings.Contains(DataQualityWarning.EstimatedTrackMap)
            ? "估算位置"
            : "赛道位置";

        return new CornerPositionEvidence(
            $"起点 {beforeMarker}●{afterMarker}  {cornerText}",
            $"{status} · {percent:0}%");
    }

    private CornerTrackMapEvidence BuildTrackMapEvidence(
        TrackSegment segment,
        TrackSegmentMap map,
        TrackMapSnapshot snapshot)
    {
        var isEstimated = map.Status == TrackSegmentMapStatus.Estimated ||
                          map.Warnings.Contains(DataQualityWarning.EstimatedTrackMap) ||
                          segment.Warnings.Contains(DataQualityWarning.EstimatedTrackMap);
        var cornerLabel = segment.CornerNumber is null
            ? segment.Name
            : $"T{segment.CornerNumber.Value.ToString(CultureInfo.InvariantCulture)} {segment.Name}";
        if (!snapshot.HasDrawableMap)
        {
            var statusText = TrackMapStatusFormatter.FormatStatus(snapshot.Status);
            return new CornerTrackMapEvidence(
                null,
                null,
                statusText,
                $"来源：{snapshot.Source}",
                $"质量：{snapshot.Quality}",
                snapshot.WarningText,
                TrackMapStatusFormatter.FormatEmptyState(snapshot.Status),
                0d,
                0d,
                0d,
                string.Empty);
        }

        if (!HasCornerRange(segment, map))
        {
            return new CornerTrackMapEvidence(
                BuildTrackMapPathData(snapshot.Points),
                null,
                TrackMapStatusFormatter.FormatStatus(TrackMapStatus.MissingCornerRange),
                $"来源：{snapshot.Source}",
                $"质量：{snapshot.Quality}",
                "暂无弯角位置数据",
                TrackMapStatusFormatter.FormatEmptyState(TrackMapStatus.MissingCornerRange),
                0d,
                0d,
                0d,
                string.Empty);
        }

        var overlay = _trackMapBuilder.BuildOverlay(
            snapshot,
            segment,
            cornerLabel,
            isEstimated,
            map.LapLengthMeters);
        var warningText = BuildTrackMapWarningText(snapshot, overlay);
        var hasMarker = overlay.MarkerX is not null && overlay.MarkerY is not null;
        return new CornerTrackMapEvidence(
            BuildTrackMapPathData(snapshot.Points),
            BuildTrackMapPathData(overlay.HighlightPoints),
            TrackMapStatusFormatter.FormatStatus(TrackMapStatus.Ready),
            $"来源：{snapshot.Source}",
            $"质量：{snapshot.Quality}",
            warningText,
            string.Empty,
            (overlay.MarkerX ?? 0d) * TrackMapCanvasWidth,
            (overlay.MarkerY ?? 0d) * TrackMapCanvasHeight,
            hasMarker ? 9d : 0d,
            hasMarker ? cornerLabel : string.Empty);
    }

    private static bool HasCornerRange(TrackSegment segment, TrackSegmentMap map)
    {
        return map.LapLengthMeters is > 0
            && float.IsFinite(segment.StartDistanceMeters)
            && float.IsFinite(segment.EndDistanceMeters)
            && Math.Abs(segment.EndDistanceMeters - segment.StartDistanceMeters) > 0.001f;
    }

    private static string? BuildTrackMapPathData(IReadOnlyList<TrackMapPoint> points)
    {
        if (points.Count == 0)
        {
            return null;
        }

        var builder = new StringBuilder();
        for (var index = 0; index < points.Count; index++)
        {
            var x = points[index].NormalizedX * TrackMapCanvasWidth;
            var y = points[index].NormalizedY * TrackMapCanvasHeight;
            builder.Append(index == 0 ? "M " : " L ");
            builder.Append(x.ToString("0.##", CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(y.ToString("0.##", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    private static string BuildTrackMapWarningText(TrackMapSnapshot snapshot, CornerTrackMapOverlay overlay)
    {
        var warnings = new List<string>();
        if (!string.IsNullOrWhiteSpace(snapshot.WarningText))
        {
            warnings.Add(snapshot.WarningText);
        }

        if (!string.IsNullOrWhiteSpace(overlay.WarningText))
        {
            warnings.Add(overlay.WarningText);
        }

        return warnings.Count == 0 ? "弯角位置来自 Motion 轨迹" : string.Join(" · ", warnings.Distinct(StringComparer.Ordinal));
    }

    private static float CalculateSegmentMidpoint(TrackSegment segment, float lapLengthMeters)
    {
        if (segment.StartDistanceMeters <= segment.EndDistanceMeters)
        {
            return (segment.StartDistanceMeters + segment.EndDistanceMeters) / 2f;
        }

        var wrappedMidpoint = (segment.StartDistanceMeters + segment.EndDistanceMeters + lapLengthMeters) / 2f;
        return wrappedMidpoint >= lapLengthMeters
            ? wrappedMidpoint - lapLengthMeters
            : wrappedMidpoint;
    }

    private static ReferenceCornerMetrics? BuildReferenceCornerMetrics(
        TrackSegment segment,
        IReadOnlyList<LapSample>? referenceSamples)
    {
        if (referenceSamples is null || referenceSamples.Count == 0)
        {
            return null;
        }

        var segmentSamples = referenceSamples
            .Where(sample => sample.LapDistance is not null && segment.ContainsDistance(sample.LapDistance.Value))
            .OrderBy(sample => sample.LapDistance)
            .ToArray();
        if (segmentSamples.Length == 0)
        {
            return null;
        }

        return new ReferenceCornerMetrics(
            segmentSamples.FirstOrDefault()?.SpeedKph,
            MinOrNull(segmentSamples.Select(sample => sample.SpeedKph)),
            segmentSamples.LastOrDefault()?.SpeedKph,
            MaxOrNull(segmentSamples.Select(sample => sample.Brake)));
    }

    private static double? MinOrNull(IEnumerable<double?> values)
    {
        var concreteValues = values
            .Where(value => value is not null)
            .Select(value => value!.Value)
            .ToArray();

        return concreteValues.Length == 0 ? null : concreteValues.Min();
    }

    private static double? MaxOrNull(IEnumerable<double?> values)
    {
        var concreteValues = values
            .Where(value => value is not null)
            .Select(value => value!.Value)
            .ToArray();

        return concreteValues.Length == 0 ? null : concreteValues.Max();
    }

    private async Task<ReferenceLapSelection> ResolveReferenceLapAsync(
        string sessionId,
        int currentLapNumber,
        IReadOnlyList<StoredLapSample> currentSamples,
        CancellationToken cancellationToken)
    {
        var currentLap = HistoryBrowser.HistoryLaps.FirstOrDefault(lap => lap.LapNumber == currentLapNumber);
        if (currentLap is null)
        {
            return ReferenceLapSelection.None("缺少可用参考圈");
        }

        var allLaps = HistoryBrowser.HistoryLaps.ToArray();
        var blockedLaps = await LoadFlagAffectedLapsAsync(sessionId, cancellationToken);
        var sampleCache = new Dictionary<int, IReadOnlyList<StoredLapSample>>
        {
            [currentLapNumber] = currentSamples
        };

        if (SelectedReferenceLap is not null)
        {
            var manualSelection = await TryCreateReferenceSelectionAsync(
                sessionId,
                currentLap,
                SelectedReferenceLap,
                ReferenceLapSource.Manual,
                currentSamples,
                blockedLaps,
                sampleCache,
                cancellationToken);
            if (manualSelection is not null)
            {
                return manualSelection;
            }
        }

        var autoCandidates = allLaps
            .Where(lap => lap.LapNumber != currentLapNumber)
            .ToArray();

        var sameStintSelection = await TrySelectFirstReferenceAsync(
            sessionId,
            currentLap,
            autoCandidates
                .Where(lap => IsSameInferredStint(lap, currentLap, allLaps))
                .OrderBy(lap => lap.LapTimeInMs),
            ReferenceLapSource.SameStintBest,
            currentSamples,
            blockedLaps,
            sampleCache,
            cancellationToken);
        if (sameStintSelection is not null)
        {
            return sameStintSelection;
        }

        var sameCompoundSelection = await TrySelectFirstReferenceAsync(
            sessionId,
            currentLap,
            autoCandidates
                .Where(lap => HasSameCompound(lap, currentLap))
                .OrderBy(lap => lap.LapTimeInMs),
            ReferenceLapSource.SameCompoundBest,
            currentSamples,
            blockedLaps,
            sampleCache,
            cancellationToken);
        if (sameCompoundSelection is not null)
        {
            return sameCompoundSelection;
        }

        var sessionBestSelection = await TrySelectFirstReferenceAsync(
            sessionId,
            currentLap,
            autoCandidates.OrderBy(lap => lap.LapTimeInMs),
            ReferenceLapSource.SessionBest,
            currentSamples,
            blockedLaps,
            sampleCache,
            cancellationToken);
        if (sessionBestSelection is not null)
        {
            return sessionBestSelection;
        }

        var previousLap = allLaps.FirstOrDefault(lap => lap.LapNumber == currentLapNumber - 1);
        if (previousLap is not null)
        {
            var previousSelection = await TryCreateReferenceSelectionAsync(
                sessionId,
                currentLap,
                previousLap,
                ReferenceLapSource.PreviousLap,
                currentSamples,
                blockedLaps,
                sampleCache,
                cancellationToken);
            if (previousSelection is not null)
            {
                return previousSelection;
            }
        }

        return ReferenceLapSelection.None("缺少可用参考圈");
    }

    private async Task<ReferenceLapSelection?> TrySelectFirstReferenceAsync(
        string sessionId,
        LapSummaryItemViewModel currentLap,
        IEnumerable<LapSummaryItemViewModel> candidates,
        ReferenceLapSource source,
        IReadOnlyList<StoredLapSample> currentSamples,
        ISet<int> blockedLaps,
        IDictionary<int, IReadOnlyList<StoredLapSample>> sampleCache,
        CancellationToken cancellationToken)
    {
        foreach (var candidate in candidates)
        {
            var selection = await TryCreateReferenceSelectionAsync(
                sessionId,
                currentLap,
                candidate,
                source,
                currentSamples,
                blockedLaps,
                sampleCache,
                cancellationToken);
            if (selection is not null)
            {
                return selection;
            }
        }

        return null;
    }

    private async Task<ReferenceLapSelection?> TryCreateReferenceSelectionAsync(
        string sessionId,
        LapSummaryItemViewModel currentLap,
        LapSummaryItemViewModel candidate,
        ReferenceLapSource source,
        IReadOnlyList<StoredLapSample> currentSamples,
        ISet<int> blockedLaps,
        IDictionary<int, IReadOnlyList<StoredLapSample>> sampleCache,
        CancellationToken cancellationToken)
    {
        if (!IsBasicReferenceCandidate(candidate, blockedLaps))
        {
            return null;
        }

        var samples = await LoadReferenceSamplesAsync(sessionId, candidate.LapNumber, sampleCache, cancellationToken);
        if (!HasUsableReferenceSamples(samples) || ContainsPitSamples(samples))
        {
            return null;
        }

        return new ReferenceLapSelection(
            BuildReferenceInfo(currentLap, candidate, source, currentSamples, samples),
            samples);
    }

    private async Task<IReadOnlyList<StoredLapSample>> LoadReferenceSamplesAsync(
        string sessionId,
        int lapNumber,
        IDictionary<int, IReadOnlyList<StoredLapSample>> sampleCache,
        CancellationToken cancellationToken)
    {
        if (sampleCache.TryGetValue(lapNumber, out var cachedSamples))
        {
            return cachedSamples;
        }

        var samples = await _lapSampleRepository.GetForLapAsync(sessionId, lapNumber, cancellationToken);
        sampleCache[lapNumber] = samples;
        return samples;
    }

    private async Task<HashSet<int>> LoadFlagAffectedLapsAsync(string sessionId, CancellationToken cancellationToken)
    {
        if (_eventRepository is null)
        {
            return [];
        }

        var events = await _eventRepository.GetRecentAsync(sessionId, 500, cancellationToken);
        return events
            .Where(storedEvent => storedEvent.LapNumber is not null && IsReferenceBlockingEvent(storedEvent.EventType))
            .Select(storedEvent => storedEvent.LapNumber!.Value)
            .ToHashSet();
    }

    private static bool IsReferenceBlockingEvent(EventType eventType)
    {
        return eventType is EventType.SafetyCar
            or EventType.VirtualSafetyCar
            or EventType.YellowFlag
            or EventType.RedFlag
            or EventType.SafetyCarRestart
            or EventType.RedFlagTyreChange;
    }

    private static bool IsBasicReferenceCandidate(LapSummaryItemViewModel lap, ISet<int> blockedLaps)
    {
        return lap.IsValid
            && lap.LapTimeInMs is >= MinimumReasonableLapTimeMs and <= MaximumReasonableLapTimeMs
            && !blockedLaps.Contains(lap.LapNumber)
            && !HasTyreTransition(lap);
    }

    private static bool HasUsableReferenceSamples(IReadOnlyList<StoredLapSample> samples)
    {
        return samples.Count(sample =>
            sample.LapDistance is not null &&
            sample.CurrentLapTimeInMs is not null &&
            sample.SpeedKph is not null) >= MinimumReferenceLapSamples;
    }

    private static bool ContainsPitSamples(IReadOnlyList<StoredLapSample> samples)
    {
        return samples.Any(sample => sample.PitStatus is > 0);
    }

    private static bool IsSameInferredStint(
        LapSummaryItemViewModel candidate,
        LapSummaryItemViewModel current,
        IReadOnlyList<LapSummaryItemViewModel> allLaps)
    {
        if (!HasSameCompound(candidate, current))
        {
            return false;
        }

        var minLap = Math.Min(candidate.LapNumber, current.LapNumber);
        var maxLap = Math.Max(candidate.LapNumber, current.LapNumber);
        return allLaps
            .Where(lap => lap.LapNumber >= minLap && lap.LapNumber <= maxLap)
            .All(lap => HasSameCompound(lap, current) && !HasTyreTransition(lap));
    }

    private static bool HasSameCompound(LapSummaryItemViewModel left, LapSummaryItemViewModel right)
    {
        var leftCompound = NormalizeCompound(left.StartTyre);
        var rightCompound = NormalizeCompound(right.StartTyre);
        return leftCompound.Length > 0 &&
               string.Equals(leftCompound, rightCompound, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasTyreTransition(LapSummaryItemViewModel lap)
    {
        var start = NormalizeCompound(lap.StartTyre);
        var end = NormalizeCompound(lap.EndTyre);
        return start.Length > 0 &&
               end.Length > 0 &&
               !string.Equals(start, end, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeCompound(string? compound)
    {
        if (string.IsNullOrWhiteSpace(compound) || compound.Trim() == "-")
        {
            return string.Empty;
        }

        return compound.Trim();
    }

    private static CornerReferenceInfo BuildReferenceInfo(
        LapSummaryItemViewModel currentLap,
        LapSummaryItemViewModel referenceLap,
        ReferenceLapSource source,
        IReadOnlyList<StoredLapSample> currentSamples,
        IReadOnlyList<StoredLapSample> referenceSamples)
    {
        var warnings = new List<string>();
        var sameCompound = HasSameCompound(currentLap, referenceLap);
        if (!sameCompound)
        {
            warnings.Add("参考圈轮胎不同");
        }
        else if (source is ReferenceLapSource.SameCompoundBest or ReferenceLapSource.SessionBest or ReferenceLapSource.PreviousLap)
        {
            warnings.Add("参考圈可能不是同 Stint");
        }

        var currentFuel = FirstFuelLitres(currentSamples);
        var referenceFuel = FirstFuelLitres(referenceSamples);
        if (currentFuel is not null &&
            referenceFuel is not null &&
            Math.Abs(currentFuel.Value - referenceFuel.Value) > SignificantFuelDifferenceLitres)
        {
            warnings.Add("参考圈燃油差异较大");
        }

        var confidence = source switch
        {
            ReferenceLapSource.SameStintBest => ConfidenceLevel.High,
            ReferenceLapSource.Manual when sameCompound && warnings.Count == 0 => ConfidenceLevel.High,
            ReferenceLapSource.Manual or ReferenceLapSource.SameCompoundBest => ConfidenceLevel.Medium,
            ReferenceLapSource.SessionBest or ReferenceLapSource.PreviousLap => ConfidenceLevel.Medium,
            _ => ConfidenceLevel.Unknown
        };

        if (!sameCompound || warnings.Count > 1)
        {
            confidence = ConfidenceLevel.Low;
        }

        return new CornerReferenceInfo
        {
            LapNumber = referenceLap.LapNumber,
            Source = source,
            LapTimeMs = referenceLap.LapTimeInMs,
            Compound = referenceLap.CompoundText,
            Confidence = confidence,
            WarningText = warnings.Count == 0
                ? "参考圈条件接近"
                : $"{string.Join("；", warnings)}，结果仅供参考"
        };
    }

    private static float? FirstFuelLitres(IReadOnlyList<StoredLapSample> samples)
    {
        return samples
            .Select(sample => sample.FuelRemainingLitres)
            .FirstOrDefault(fuel => fuel is not null);
    }

    private sealed record ReferenceLapSelection(
        CornerReferenceInfo Info,
        IReadOnlyList<StoredLapSample> Samples)
    {
        public static ReferenceLapSelection None(string warningText)
        {
            return new ReferenceLapSelection(CornerReferenceInfo.None(warningText), Array.Empty<StoredLapSample>());
        }
    }

    private sealed record ReferenceCornerMetrics(
        double? EntrySpeedKph,
        double? MinimumSpeedKph,
        double? ExitSpeedKph,
        double? MaxBrake);

    private sealed record CornerVisualEvidence(
        CornerMetricChartEvidence Speed,
        CornerMetricChartEvidence Brake,
        CornerMetricChartEvidence Throttle,
        CornerPositionEvidence Position,
        CornerTrackMapEvidence TrackMap);

    private sealed record CornerTrackMapEvidence(
        string? PathData,
        string? HighlightPathData,
        string StatusText,
        string SourceText,
        string QualityText,
        string WarningText,
        string EmptyStateText,
        double MarkerX,
        double MarkerY,
        double MarkerSize,
        string CornerLabelText);

    private sealed record CornerMetricChartEvidence(
        string? CurrentPathData,
        string? ReferencePathData,
        string StatusText)
    {
        public static CornerMetricChartEvidence MissingReference()
        {
            return new CornerMetricChartEvidence(null, null, "缺少参考圈，暂无法对比");
        }

        public static CornerMetricChartEvidence InsufficientSamples()
        {
            return new CornerMetricChartEvidence(null, null, "采样不足，暂无法绘制");
        }
    }

    private sealed record CornerPositionEvidence(
        string IndicatorText,
        string StatusText)
    {
        public static CornerPositionEvidence Missing()
        {
            return new CornerPositionEvidence("暂无赛道位置数据", "暂无赛道位置数据");
        }
    }

    private enum CornerEngineerAdviceState
    {
        Ready,
        Loading,
        Generated,
        InsufficientData,
        Failed
    }

    private void OnHistoryLapsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (HistoryBrowser.HistoryLaps.Count == 0)
        {
            if (_selectedLap is not null)
            {
                _selectedLap = null;
                OnPropertyChanged(nameof(SelectedLap));
            }

            if (_selectedReferenceLap is not null)
            {
                _selectedReferenceLap = null;
                OnPropertyChanged(nameof(SelectedReferenceLap));
            }
        }
        else if (SelectedReferenceLap is not null && !HistoryBrowser.HistoryLaps.Contains(SelectedReferenceLap))
        {
            SelectedReferenceLap = null;
        }

        OnPropertyChanged(nameof(SelectionText));
        OnPropertyChanged(nameof(HasReferenceLapChoices));
        OnPropertyChanged(nameof(ReferencePickerText));
    }

    private void OnHistoryBrowserPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HistorySessionBrowserViewModel.SelectedSession))
        {
            SelectedLap = null;
            SelectedReferenceLap = null;
            OnPropertyChanged(nameof(SelectionText));
            OnPropertyChanged(nameof(HasReferenceLapChoices));
            OnPropertyChanged(nameof(ReferencePickerText));
        }

        if (e.PropertyName == nameof(HistorySessionBrowserViewModel.IsLoadingLaps)
            && !HistoryBrowser.IsLoadingLaps)
        {
            SelectDefaultLapIfNeeded();
        }
    }

    private void SelectDefaultLapIfNeeded()
    {
        if (SelectedLap is not null && HistoryBrowser.HistoryLaps.Contains(SelectedLap))
        {
            return;
        }

        SelectedLap = _selectedLapNumber is null
            ? null
            : HistoryBrowser.HistoryLaps.FirstOrDefault(lap => lap.LapNumber == _selectedLapNumber.Value);

        SelectedLap ??= HistoryBrowser.HistoryLaps
            .Where(lap => lap.LapNumber > 0)
            .OrderByDescending(lap => lap.LapNumber)
            .FirstOrDefault();
    }

    private static sbyte? NormalizeTrackId(int? trackId)
    {
        return trackId is >= sbyte.MinValue and <= sbyte.MaxValue ? (sbyte)trackId.Value : null;
    }

    private static LapSample ToLapSample(StoredLapSample sample)
    {
        return new LapSample
        {
            SampledAt = sample.SampledAt,
            FrameIdentifier = checked((uint)sample.FrameIdentifier),
            LapNumber = sample.LapNumber,
            LapDistance = sample.LapDistance,
            TotalDistance = sample.TotalDistance,
            CurrentLapTimeInMs = sample.CurrentLapTimeInMs is null ? null : checked((uint)sample.CurrentLapTimeInMs.Value),
            LastLapTimeInMs = sample.LastLapTimeInMs is null ? null : checked((uint)sample.LastLapTimeInMs.Value),
            SpeedKph = sample.SpeedKph,
            Throttle = sample.Throttle,
            Brake = sample.Brake,
            Steering = sample.Steering,
            Gear = sample.Gear is null ? null : checked((sbyte)sample.Gear.Value),
            FuelRemaining = sample.FuelRemainingLitres,
            FuelLapsRemaining = sample.FuelLapsRemaining,
            ErsStoreEnergy = sample.ErsStoreEnergy,
            TyreWear = sample.TyreWear,
            TyreWearPerWheel = sample.TyreWearFrontLeft is null
                || sample.TyreWearFrontRight is null
                || sample.TyreWearRearLeft is null
                || sample.TyreWearRearRight is null
                    ? null
                    : new WheelSet<float>(
                        sample.TyreWearRearLeft.Value,
                        sample.TyreWearRearRight.Value,
                        sample.TyreWearFrontLeft.Value,
                        sample.TyreWearFrontRight.Value),
            Position = sample.Position is null ? null : checked((byte)sample.Position.Value),
            DeltaFrontInMs = sample.DeltaFrontInMs is null ? null : checked((ushort)sample.DeltaFrontInMs.Value),
            DeltaLeaderInMs = sample.DeltaLeaderInMs is null ? null : checked((ushort)sample.DeltaLeaderInMs.Value),
            PitStatus = sample.PitStatus is null ? null : checked((byte)sample.PitStatus.Value),
            IsValid = sample.IsValid,
            VisualTyreCompound = sample.VisualTyreCompound is null ? null : checked((byte)sample.VisualTyreCompound.Value),
            ActualTyreCompound = sample.ActualTyreCompound is null ? null : checked((byte)sample.ActualTyreCompound.Value)
        };
    }
}
