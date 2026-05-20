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
    private const int MinimumReasonableLapTimeMs = 30_000;
    private const int MaximumReasonableLapTimeMs = 600_000;
    private const float SignificantFuelDifferenceLitres = 5f;
    private readonly ILapSampleRepository _lapSampleRepository;
    private readonly IEventRepository? _eventRepository;
    private readonly ITrackSegmentMapProvider _trackSegmentMapProvider;
    private readonly CornerMetricsExtractor _cornerMetricsExtractor;
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
    private string _weakestCornerText = "-";
    private string _bestConfidenceCornerText = "-";
    private string _referenceStatusText = "-";
    private string _engineerAdviceStatusText = "等待 AI 弯角分析。";
    private string _aiAnnotationText = "刷新弯角分析后生成 AI 工程师建议。";
    private CornerReferenceInfo _referenceInfo = CornerReferenceInfo.None();

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
    public CornerAnalysisViewModel(
        HistorySessionBrowserViewModel historyBrowser,
        ILapSampleRepository lapSampleRepository,
        IEventRepository? eventRepository = null,
        ITrackSegmentMapProvider? trackSegmentMapProvider = null,
        CornerMetricsExtractor? cornerMetricsExtractor = null,
        IAIAnalysisService? aiAnalysisService = null,
        IAppSettingsStore? settingsStore = null,
        TtsMessageFactory? ttsMessageFactory = null,
        TtsQueue? ttsQueue = null)
    {
        HistoryBrowser = historyBrowser ?? throw new ArgumentNullException(nameof(historyBrowser));
        _lapSampleRepository = lapSampleRepository ?? throw new ArgumentNullException(nameof(lapSampleRepository));
        _eventRepository = eventRepository;
        _trackSegmentMapProvider = trackSegmentMapProvider ?? new StaticTrackSegmentMapProvider();
        _cornerMetricsExtractor = cornerMetricsExtractor ?? new CornerMetricsExtractor();
        _aiAnalysisService = aiAnalysisService;
        _settingsStore = settingsStore;
        _ttsMessageFactory = ttsMessageFactory;
        _ttsQueue = ttsQueue;
        CornerRows = new ObservableCollection<CornerSummaryRowViewModel>();
        EngineerSuggestions = new ObservableCollection<string>();
        _refreshCommand = new RelayCommand(() => _ = RefreshAsync(), () => !IsLoading && !IsEngineerAdviceLoading);
        _generateEngineerAdviceCommand = new RelayCommand(
            () => _ = GenerateEngineerAdviceAsync(CancellationToken.None),
            () => !IsLoading && !IsEngineerAdviceLoading && CornerRows.Count > 0);
        CornerRows.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasCornerRows));
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
            }
        }
    }

    /// <summary>
    /// Gets the engineer-advice button text.
    /// </summary>
    public string EngineerAdviceButtonText => IsEngineerAdviceLoading ? "AI分析中" : "AI建议";

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
        AiAnnotationText = "刷新弯角分析后生成 AI 工程师建议。";
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
            var result = _cornerMetricsExtractor.Extract(map, lapSamples, referenceSamples);
            foreach (var row in result.Corners.Select(CornerSummaryRowViewModel.FromSummary))
            {
                CornerRows.Add(row);
            }

            EmptyStateText = CornerRows.Count == 0 ? "没有可显示的弯角摘要。" : string.Empty;
            UpdateDashboardSummaries(result, session, lapNumber.Value, referenceSelection.Info);
            StatusText = $"已生成 {session.TrackText} {session.SessionTypeText} Lap {lapNumber.Value} 弯角分析：{CornerRows.Count} 个弯角，置信度 {result.Confidence}。";
            if (CornerRows.Count > 0)
            {
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
            EngineerAdviceStatusText = "没有可用于 AI 建议的弯角数据。";
            return;
        }

        if (_aiAnalysisService is null || _settingsStore is null)
        {
            EngineerAdviceStatusText = "AI建议不可用：未接入 AI 服务。";
            return;
        }

        IsEngineerAdviceLoading = true;
        EngineerAdviceStatusText = "AI正在分析弯角数据...";
        try
        {
            var settings = await _settingsStore.LoadAsync(cancellationToken);
            if (!settings.Ai.AiEnabled)
            {
                EngineerAdviceStatusText = "AI未启用，工程师建议暂不生成。";
                return;
            }

            var context = BuildEngineerAdviceContext(session, lap, result);
            var aiResult = await _aiAnalysisService.AnalyzeAsync(context, settings.Ai, cancellationToken);
            if (!aiResult.IsSuccess)
            {
                EngineerAdviceStatusText = $"AI建议生成失败：{aiResult.ErrorMessage}";
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
            EngineerAdviceStatusText = suggestions.Length == 0
                ? "AI已返回分析，但没有可显示的工程师建议。"
                : BuildEngineerAdviceTtsStatus(session, lap, aiResult, settings.Tts);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            EngineerAdviceStatusText = "AI建议生成已取消。";
        }
        catch (Exception ex)
        {
            EngineerAdviceStatusText = $"AI建议生成失败：{ex.Message}";
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
        F1Telemetry.TTS.Models.TtsOptions ttsOptions)
    {
        if (!ttsOptions.TtsEnabled)
        {
            return "AI建议已生成，TTS未启用。";
        }

        if (_ttsMessageFactory is null || _ttsQueue is null)
        {
            return "AI建议已生成，未接入 TTS 队列。";
        }

        var speechText = ResolveEngineerAdviceSpeechText(aiResult);
        var message = _ttsMessageFactory.CreateForEngineerAdvice(
            $"{session.SessionId}:lap{lap.LapNumber.ToString(CultureInfo.InvariantCulture)}",
            speechText,
            ttsOptions);
        if (message is null)
        {
            return "AI建议已生成，TTS冷却中未重复播报。";
        }

        _ttsQueue.UpdateOptions(ttsOptions);
        return _ttsQueue.TryEnqueue(message)
            ? "AI建议已生成，TTS已加入播报队列。"
            : "AI建议已生成，TTS队列暂未接受播报。";
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
        builder.AppendLine($"Total positive time loss: {TotalTimeLossText}");
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
        WeakestCornerText = "-";
        BestConfidenceCornerText = "-";
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
        var weakestCorner = rows
            .OrderByDescending(row => row.PositiveTimeLossInMs)
            .ThenBy(row => row.CornerNumber ?? int.MaxValue)
            .FirstOrDefault();
        var bestConfidenceCorner = rows
            .OrderByDescending(row => ResolveConfidenceRank(row.ConfidenceText))
            .ThenBy(row => row.PositiveTimeLossInMs)
            .FirstOrDefault();

        AnalysisTimeText = DateTimeOffset.Now.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        TotalTimeLossText = totalLoss > 0 ? $"+{totalLoss / 1000d:0.000}s" : "-";
        WeakestCornerText = weakestCorner is null ? "-" : $"{weakestCorner.CornerText} · {FormatLossSeconds(weakestCorner.TimeLossInMs)}";
        BestConfidenceCornerText = bestConfidenceCorner is null ? "-" : $"{bestConfidenceCorner.CornerText} · {bestConfidenceCorner.ConfidenceText}";
        ReferenceInfo = referenceInfo;
        SelectedCorner = weakestCorner ?? rows.FirstOrDefault();

        if (rows.Length > 0)
        {
            StatusText = $"已生成 {session.TrackText} {session.SessionTypeText} Lap {lapNumber} 弯角分析：{rows.Length} 个弯角，置信度 {result.Confidence}。";
        }
    }

    private static int ResolveConfidenceRank(string confidenceText)
    {
        return confidenceText switch
        {
            nameof(ConfidenceLevel.High) => 3,
            nameof(ConfidenceLevel.Medium) => 2,
            nameof(ConfidenceLevel.Low) => 1,
            _ => 0
        };
    }

    private static string FormatLossSeconds(int? timeLossInMs)
    {
        return timeLossInMs is null ? "缺少参考" : $"{timeLossInMs.Value / 1000d:+0.000;-0.000;0.000}s";
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
    }

    private void OnHistoryBrowserPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HistorySessionBrowserViewModel.SelectedSession))
        {
            SelectedLap = null;
            SelectedReferenceLap = null;
            OnPropertyChanged(nameof(SelectionText));
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
