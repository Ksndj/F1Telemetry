using F1Telemetry.AI.Interfaces;
using F1Telemetry.AI.Models;
using F1Telemetry.Analytics.Corners;
using F1Telemetry.Analytics.Events;
using F1Telemetry.Analytics.Laps;
using F1Telemetry.Analytics.Tracks;
using F1Telemetry.App.ViewModels;
using F1Telemetry.Core.Interfaces;
using F1Telemetry.Core.Models;
using F1Telemetry.Storage.Interfaces;
using F1Telemetry.Storage.Models;
using F1Telemetry.TTS.Models;
using F1Telemetry.TTS.Services;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies the V3 corner analysis ViewModel loads persisted lap samples through the history browser.
/// </summary>
public sealed class CornerAnalysisViewModelTests
{
    /// <summary>
    /// Verifies a supported stored session produces corner rows.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_WithStoredSamples_BuildsCornerRows()
    {
        var sessionRepository = new RecordingSessionRepository
        {
            Sessions =
            [
                new StoredSession
                {
                    Id = "session-a",
                    SessionUid = "uid-a",
                    TrackId = 0,
                    SessionType = 15,
                    StartedAt = DateTimeOffset.Parse("2026-05-17T10:00:00Z")
                }
            ]
        };
        var lapRepository = new RecordingLapRepository
        {
            Laps =
            [
                new StoredLap { SessionId = "session-a", LapNumber = 7, LapTimeInMs = 91_000, IsValid = true, StartTyre = "Medium", EndTyre = "Medium", CreatedAt = DateTimeOffset.Parse("2026-05-17T10:07:00Z") }
            ]
        };
        var sampleRepository = new RecordingLapSampleRepository
        {
            Samples =
            [
                CreateSample("session-a", 7, 0, 270, 10_000, 210, 0.8, 0.0),
                CreateSample("session-a", 7, 1, 330, 10_600, 160, 0.2, 0.6),
                CreateSample("session-a", 7, 2, 410, 11_500, 110, 0.1, 0.9),
                CreateSample("session-a", 7, 3, 500, 12_700, 190, 0.9, 0.0)
            ]
        };
        var historyBrowser = new HistorySessionBrowserViewModel(sessionRepository, lapRepository);
        var viewModel = new CornerAnalysisViewModel(historyBrowser, sampleRepository);

        await viewModel.RefreshAsync();

        Assert.NotEmpty(viewModel.CornerRows);
        Assert.Contains("Lap 7", viewModel.StatusText, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies the selected historical lap controls which stored samples are analyzed.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_WithSelectedLap_AnalyzesSelectedLapSamples()
    {
        var sessionRepository = new RecordingSessionRepository
        {
            Sessions =
            [
                new StoredSession
                {
                    Id = "session-selected",
                    SessionUid = "uid-selected",
                    TrackId = 0,
                    SessionType = 15,
                    StartedAt = DateTimeOffset.Parse("2026-05-17T10:00:00Z")
                }
            ]
        };
        var lapRepository = new RecordingLapRepository
        {
            Laps =
            [
                new StoredLap { SessionId = "session-selected", LapNumber = 3, LapTimeInMs = 92_000, IsValid = true, StartTyre = "Medium", EndTyre = "Medium", CreatedAt = DateTimeOffset.Parse("2026-05-17T10:03:00Z") },
                new StoredLap { SessionId = "session-selected", LapNumber = 8, LapTimeInMs = 90_000, IsValid = true, StartTyre = "Medium", EndTyre = "Medium", CreatedAt = DateTimeOffset.Parse("2026-05-17T10:08:00Z") }
            ]
        };
        var sampleRepository = new RecordingLapSampleRepository
        {
            Samples =
            [
                CreateSample("session-selected", 3, 0, 270, 10_000, 210, 0.8, 0.0),
                CreateSample("session-selected", 3, 1, 330, 10_600, 160, 0.2, 0.6),
                CreateSample("session-selected", 3, 2, 410, 11_500, 110, 0.1, 0.9),
                CreateSample("session-selected", 3, 3, 500, 12_700, 190, 0.9, 0.0),
                CreateSample("session-selected", 8, 0, 270, 10_000, 220, 0.8, 0.0)
            ]
        };
        var historyBrowser = new HistorySessionBrowserViewModel(sessionRepository, lapRepository);
        var viewModel = new CornerAnalysisViewModel(historyBrowser, sampleRepository);
        await historyBrowser.RefreshSessionsAsync();
        viewModel.SelectedLap = historyBrowser.HistoryLaps.Single(lap => lap.LapNumber == 3);

        await viewModel.RefreshAsync();

        Assert.Equal("session-selected", sampleRepository.RequestedSessionId);
        Assert.Equal(3, sampleRepository.RequestedLapNumbers.First());
        Assert.Contains("Lap 3", viewModel.StatusText, StringComparison.Ordinal);
        Assert.NotEmpty(viewModel.CornerRows);
    }

    /// <summary>
    /// Verifies unsupported track ids surface a clear empty state.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_WithUnsupportedTrack_ShowsUnsupportedState()
    {
        var historyBrowser = new HistorySessionBrowserViewModel(
            new RecordingSessionRepository
            {
                Sessions =
                [
                    new StoredSession
                    {
                        Id = "session-b",
                        SessionUid = "uid-b",
                        TrackId = 44,
                        SessionType = 15,
                        StartedAt = DateTimeOffset.Parse("2026-05-17T10:00:00Z")
                    }
                ]
            },
            new RecordingLapRepository
            {
                Laps =
                [
                    new StoredLap { SessionId = "session-b", LapNumber = 1, IsValid = true, StartTyre = "Soft", EndTyre = "Soft", CreatedAt = DateTimeOffset.Parse("2026-05-17T10:01:00Z") }
                ]
            });
        var viewModel = new CornerAnalysisViewModel(historyBrowser, new RecordingLapSampleRepository());

        await viewModel.RefreshAsync();

        Assert.Empty(viewModel.CornerRows);
        Assert.Contains("暂未支持", viewModel.EmptyStateText, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies repository failures are converted into a stable error state.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_WhenSampleRepositoryThrows_ShowsFailureState()
    {
        var historyBrowser = CreateHistoryBrowserWithSupportedLap("session-error");
        var viewModel = new CornerAnalysisViewModel(historyBrowser, new ThrowingLapSampleRepository());

        await viewModel.RefreshAsync();

        Assert.Empty(viewModel.CornerRows);
        Assert.Contains("加载失败", viewModel.EmptyStateText, StringComparison.Ordinal);
        Assert.Contains("sample read failed", viewModel.ErrorMessage, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies corrupt stored samples do not surface as unhandled refresh exceptions.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_WhenStoredSampleCannotBeProjected_ShowsFailureState()
    {
        var historyBrowser = CreateHistoryBrowserWithSupportedLap("session-corrupt");
        var sampleRepository = new RecordingLapSampleRepository
        {
            Samples =
            [
                CreateSample("session-corrupt", 7, 0, 270, 10_000, 210, 0.8, 0.0)
                    with
                    {
                        FrameIdentifier = -1
                    }
            ]
        };
        var viewModel = new CornerAnalysisViewModel(historyBrowser, sampleRepository);

        await viewModel.RefreshAsync();

        Assert.Empty(viewModel.CornerRows);
        Assert.Contains("加载失败", viewModel.StatusText, StringComparison.Ordinal);
        Assert.NotEmpty(viewModel.ErrorMessage);
    }

    /// <summary>
    /// Verifies AI-generated engineer advice is shown in the UI and TTS is paced per session lap.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_WithAiEngineerAdvice_ShowsSuggestionsAndLimitsTts()
    {
        var historyBrowser = CreateHistoryBrowserWithSupportedLap("session-ai");
        var sampleRepository = new RecordingLapSampleRepository
        {
            Samples =
            [
                CreateSample("session-ai", 7, 0, 270, 10_000, 210, 0.8, 0.0),
                CreateSample("session-ai", 7, 1, 330, 10_600, 160, 0.2, 0.6),
                CreateSample("session-ai", 7, 2, 410, 11_500, 110, 0.1, 0.9),
                CreateSample("session-ai", 7, 3, 500, 12_700, 190, 0.9, 0.0)
            ]
        };
        var aiService = new RecordingAiAnalysisService
        {
            Result = new AIAnalysisResult
            {
                IsSuccess = true,
                Summary = "7 号弯损失最集中，需要优化入弯速度。",
                Improvements =
                [
                    "7 号弯入弯提前轻刹，避免最低速度过高导致推头。",
                    "出弯油门分两段打开，先稳住车尾再全油。",
                    "下一圈优先复核 7 号弯和 11 号弯的刹车点。"
                ],
                Tts = "7号弯先稳入弯，再提早出弯。"
            }
        };
        var settingsStore = new FixedSettingsStore(
            new AppSettingsDocument
            {
                Ai = new AISettings
                {
                    AiEnabled = true,
                    ApiKey = "test-key"
                },
                Tts = new TtsOptions
                {
                    TtsEnabled = true,
                    CooldownSeconds = 8
                }
            });
        var ttsService = new RecordingTtsService();
        using var ttsQueue = new TtsQueue(ttsService, new TtsOptions { TtsEnabled = true });
        var viewModel = new CornerAnalysisViewModel(
            historyBrowser,
            sampleRepository,
            aiAnalysisService: aiService,
            settingsStore: settingsStore,
            ttsMessageFactory: new F1Telemetry.AI.Services.TtsMessageFactory(),
            ttsQueue: ttsQueue);

        await viewModel.RefreshAsync();
        await ttsService.WaitForSpeechCountAsync(1);
        await viewModel.GenerateEngineerAdviceAsync();
        await Task.Delay(100);

        Assert.Equal(2, aiService.RequestCount);
        Assert.Equal(3, viewModel.EngineerSuggestions.Count);
        Assert.Contains("7 号弯", viewModel.EngineerSuggestions[0], StringComparison.Ordinal);
        Assert.Contains("TTS冷却中", viewModel.EngineerAdviceStatusText, StringComparison.Ordinal);
        Assert.Single(ttsService.SpokenTexts);
        Assert.Equal("7号弯先稳入弯，再提早出弯。", ttsService.SpokenTexts[0]);
    }

    /// <summary>
    /// Verifies repeated AI clicks while loading do not start duplicate requests.
    /// </summary>
    [Fact]
    public async Task GenerateEngineerAdviceAsync_WhileLoading_DoesNotStartDuplicateRequest()
    {
        var aiService = new RecordingAiAnalysisService
        {
            Delay = TimeSpan.FromMilliseconds(150),
            Result = CreateSuccessfulAiResult()
        };
        var viewModel = await CreateReadyAiAdviceViewModelAsync("session-ai-repeat", aiService);

        var first = viewModel.GenerateEngineerAdviceAsync();
        await Task.Delay(20);
        var second = viewModel.GenerateEngineerAdviceAsync();
        await Task.WhenAll(first, second);

        Assert.Equal(1, aiService.RequestCount);
        Assert.Equal("已生成", viewModel.EngineerAdviceButtonText);
        Assert.False(viewModel.IsEngineerAdviceLoading);
    }

    /// <summary>
    /// Verifies a failed AI request is visible and can be retried.
    /// </summary>
    [Fact]
    public async Task GenerateEngineerAdviceAsync_AfterFailure_AllowsRetry()
    {
        var aiService = new RecordingAiAnalysisService();
        aiService.Results.Enqueue(new AIAnalysisResult
        {
            IsSuccess = false,
            ErrorMessage = "service unavailable"
        });
        aiService.Results.Enqueue(CreateSuccessfulAiResult());
        var viewModel = await CreateReadyAiAdviceViewModelAsync("session-ai-retry", aiService);

        await viewModel.GenerateEngineerAdviceAsync();

        Assert.Equal("生成失败", viewModel.EngineerAdviceButtonText);
        Assert.Contains("service unavailable", viewModel.EngineerAdviceStatusText, StringComparison.Ordinal);
        Assert.True(viewModel.GenerateEngineerAdviceCommand.CanExecute(null));

        await viewModel.GenerateEngineerAdviceAsync();

        Assert.Equal(2, aiService.RequestCount);
        Assert.Equal("已生成", viewModel.EngineerAdviceButtonText);
        Assert.Contains("已生成", viewModel.EngineerAdviceStatusText, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies the AI button reports insufficient data when no rows are available.
    /// </summary>
    [Fact]
    public async Task GenerateEngineerAdviceAsync_WithoutRows_ShowsInsufficientData()
    {
        var viewModel = await CreateReadyAiAdviceViewModelAsync(
            "session-ai-empty",
            new RecordingAiAnalysisService(),
            addCornerRow: false);

        await viewModel.GenerateEngineerAdviceAsync();

        Assert.Equal("数据不足", viewModel.EngineerAdviceButtonText);
        Assert.Contains("当前数据不足", viewModel.EngineerAdviceStatusText, StringComparison.Ordinal);
        Assert.False(viewModel.GenerateEngineerAdviceCommand.CanExecute(null));
    }

    /// <summary>
    /// Verifies the automatic reference selector prefers the fastest valid same-stint lap.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_WithSameStintFastestLap_UsesSameStintReference()
    {
        var historyBrowser = CreateHistoryBrowser(
            "session-same-stint",
            [
                CreateStoredLap("session-same-stint", 2, 88_000, "Soft"),
                CreateStoredLap("session-same-stint", 4, 90_000, "Medium"),
                CreateStoredLap("session-same-stint", 5, 93_000, "Medium")
            ]);
        var sampleRepository = new RecordingLapSampleRepository
        {
            Samples =
            [
                ..CreateCornerSamples("session-same-stint", 2, 9_000),
                ..CreateCornerSamples("session-same-stint", 4, 9_200),
                ..CreateCornerSamples("session-same-stint", 5, 10_000)
            ]
        };
        var viewModel = new CornerAnalysisViewModel(historyBrowser, sampleRepository);

        await viewModel.RefreshAsync();

        Assert.Equal(ReferenceLapSource.SameStintBest, viewModel.ReferenceInfo.Source);
        Assert.Equal(4, viewModel.ReferenceInfo.LapNumber);
        Assert.Equal("Lap 4", viewModel.ReferenceStatusText);
        Assert.True(viewModel.HasReferenceLapChoices);
        Assert.Equal("自动参考圈：Lap 4", viewModel.ReferencePickerText);
        Assert.DoesNotContain(viewModel.CornerRows, row => row.WarningDisplayText.Contains("MissingRefLap", StringComparison.Ordinal));
    }

    /// <summary>
    /// Verifies the reference picker reflects manual selection without showing a blank state.
    /// </summary>
    [Fact]
    public async Task SelectedReferenceLap_WithManualSelection_UpdatesPickerAndSource()
    {
        var historyBrowser = CreateHistoryBrowser(
            "session-manual-ref",
            [
                CreateStoredLap("session-manual-ref", 2, 88_000, "Soft"),
                CreateStoredLap("session-manual-ref", 4, 90_000, "Medium"),
                CreateStoredLap("session-manual-ref", 5, 93_000, "Medium")
            ]);
        var sampleRepository = new RecordingLapSampleRepository
        {
            Samples =
            [
                ..CreateCornerSamples("session-manual-ref", 2, 9_000),
                ..CreateCornerSamples("session-manual-ref", 4, 9_200),
                ..CreateCornerSamples("session-manual-ref", 5, 10_000)
            ]
        };
        var viewModel = new CornerAnalysisViewModel(historyBrowser, sampleRepository);
        await viewModel.RefreshAsync();

        viewModel.SelectedReferenceLap = historyBrowser.HistoryLaps.Single(lap => lap.LapNumber == 2);

        Assert.Equal(ReferenceLapSource.Manual, viewModel.ReferenceInfo.Source);
        Assert.Equal("手动参考圈：Lap 2", viewModel.ReferencePickerText);
    }

    /// <summary>
    /// Verifies positive loss and net time delta cards use the same row values as the table.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_WithMixedCornerDeltas_CalculatesPositiveAndNetTotals()
    {
        var sessionId = "session-delta";
        var historyBrowser = CreateHistoryBrowser(
            sessionId,
            [
                CreateStoredLap(sessionId, 4, 90_000, "Medium"),
                CreateStoredLap(sessionId, 5, 93_000, "Medium")
            ]);
        var sampleRepository = new RecordingLapSampleRepository
        {
            Samples =
            [
                ..CreateTwoSegmentSamples(sessionId, 4, firstSegmentDurationMs: 800, secondSegmentDurationMs: 900),
                ..CreateTwoSegmentSamples(sessionId, 5, firstSegmentDurationMs: 1_000, secondSegmentDurationMs: 600)
            ]
        };
        var viewModel = new CornerAnalysisViewModel(historyBrowser, sampleRepository);

        await viewModel.RefreshAsync();

        Assert.Equal("+0.200s", viewModel.TotalTimeLossText);
        Assert.Equal("-0.100s", viewModel.NetTimeDeltaText);
        Assert.Contains(viewModel.CornerRows, row => row.TimeLossInMs == 200);
        Assert.Contains(viewModel.CornerRows, row => row.TimeLossInMs == -300);
    }

    /// <summary>
    /// Verifies the automatic reference selector falls back to the session-best lap when no same-stint lap is usable.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_WithoutSameStintReference_UsesSessionBestReference()
    {
        var historyBrowser = CreateHistoryBrowser(
            "session-best",
            [
                CreateStoredLap("session-best", 2, 88_000, "Soft"),
                CreateStoredLap("session-best", 5, 93_000, "Medium")
            ]);
        var sampleRepository = new RecordingLapSampleRepository
        {
            Samples =
            [
                ..CreateCornerSamples("session-best", 2, 9_000),
                ..CreateCornerSamples("session-best", 5, 10_000)
            ]
        };
        var viewModel = new CornerAnalysisViewModel(historyBrowser, sampleRepository);

        await viewModel.RefreshAsync();

        Assert.Equal(ReferenceLapSource.SessionBest, viewModel.ReferenceInfo.Source);
        Assert.Equal(2, viewModel.ReferenceInfo.LapNumber);
        Assert.Contains("轮胎不同", viewModel.ReferenceInfo.WarningText, StringComparison.Ordinal);
        Assert.Equal("Low", viewModel.ReferenceInfo.QualityText);
    }

    /// <summary>
    /// Verifies invalid, pit, and flag-affected laps are not used as automatic references.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_SkipsInvalidPitAndFlagAffectedReferenceLaps()
    {
        var sessionId = "session-filter";
        var historyBrowser = CreateHistoryBrowser(
            sessionId,
            [
                CreateStoredLap(sessionId, 1, 94_000, "Medium"),
                CreateStoredLap(sessionId, 2, 88_000, "Medium"),
                CreateStoredLap(sessionId, 3, 89_000, "Medium"),
                CreateStoredLap(sessionId, 4, 87_000, "Medium", isValid: false),
                CreateStoredLap(sessionId, 5, 95_000, "Medium")
            ]);
        var sampleRepository = new RecordingLapSampleRepository
        {
            Samples =
            [
                ..CreateCornerSamples(sessionId, 1, 9_800),
                ..CreateCornerSamples(sessionId, 2, 9_000),
                ..CreateCornerSamples(sessionId, 3, 9_100, pitStatus: 1),
                ..CreateCornerSamples(sessionId, 4, 8_900),
                ..CreateCornerSamples(sessionId, 5, 10_000)
            ]
        };
        var eventRepository = new RecordingEventRepository
        {
            Events =
            [
                new StoredEvent
                {
                    SessionId = sessionId,
                    EventType = EventType.SafetyCar,
                    Severity = EventSeverity.Information,
                    LapNumber = 2,
                    CreatedAt = DateTimeOffset.Parse("2026-05-17T10:02:00Z")
                }
            ]
        };
        var viewModel = new CornerAnalysisViewModel(historyBrowser, sampleRepository, eventRepository);

        await viewModel.RefreshAsync();

        Assert.Equal(1, viewModel.ReferenceInfo.LapNumber);
        Assert.Equal(ReferenceLapSource.SameStintBest, viewModel.ReferenceInfo.Source);
    }

    /// <summary>
    /// Verifies the page shows a clear missing-reference state without raw enum text.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_WithoutUsableReference_ShowsClearMissingReferenceState()
    {
        var sessionId = "session-no-ref";
        var historyBrowser = CreateHistoryBrowser(
            sessionId,
            [
                CreateStoredLap(sessionId, 7, 91_000, "Medium")
            ]);
        var sampleRepository = new RecordingLapSampleRepository
        {
            Samples =
            [
                ..CreateCornerSamples(sessionId, 7, 10_000)
            ]
        };
        var viewModel = new CornerAnalysisViewModel(historyBrowser, sampleRepository);

        await viewModel.RefreshAsync();

        Assert.Equal(ReferenceLapSource.None, viewModel.ReferenceInfo.Source);
        Assert.Equal("缺少可用参考圈", viewModel.ReferenceStatusText);
        Assert.False(viewModel.HasReferenceLapChoices);
        Assert.Equal("暂无可用参考圈", viewModel.ReferencePickerText);
        Assert.Contains("缺少可用参考圈", viewModel.ReferenceInfo.WarningText, StringComparison.Ordinal);
        Assert.DoesNotContain(viewModel.CornerRows, row => row.WarningDisplayText.Contains("MissingRefLap", StringComparison.Ordinal));
        Assert.Contains(viewModel.CornerRows, row => row.WarningDisplayText.Contains("缺参考", StringComparison.Ordinal));
    }

    private static async Task<CornerAnalysisViewModel> CreateReadyAiAdviceViewModelAsync(
        string sessionId,
        RecordingAiAnalysisService aiService,
        bool addCornerRow = true)
    {
        var historyBrowser = CreateHistoryBrowser(
            sessionId,
            [
                CreateStoredLap(sessionId, 7, 91_000, "Medium")
            ]);
        await historyBrowser.RefreshSessionsAsync();
        await historyBrowser.LoadSelectedSessionLapsAsync();

        var viewModel = new CornerAnalysisViewModel(
            historyBrowser,
            new RecordingLapSampleRepository(),
            aiAnalysisService: aiService,
            settingsStore: CreateEnabledAiSettingsStore());
        viewModel.SelectedLap = historyBrowser.HistoryLaps.Single(lap => lap.LapNumber == 7);
        if (addCornerRow)
        {
            viewModel.CornerRows.Add(CreateTestCornerRow());
        }

        return viewModel;
    }

    private static FixedSettingsStore CreateEnabledAiSettingsStore()
    {
        return new FixedSettingsStore(
            new AppSettingsDocument
            {
                Ai = new AISettings
                {
                    AiEnabled = true,
                    ApiKey = "test-key"
                },
                Tts = new TtsOptions
                {
                    TtsEnabled = false
                }
            });
    }

    private static AIAnalysisResult CreateSuccessfulAiResult()
    {
        return new AIAnalysisResult
        {
            IsSuccess = true,
            Summary = "7 号弯建议已生成。",
            Improvements =
            [
                "7 号弯入弯前稳定刹车。",
                "最低速度后再逐步加油。",
                "出弯保持方向盘更早回正。"
            ],
            Tts = "7号弯先稳住入弯。"
        };
    }

    private static CornerSummaryRowViewModel CreateTestCornerRow()
    {
        return CornerSummaryRowViewModel.FromSummary(new CornerSummary
        {
            Segment = new TrackSegment
            {
                SegmentId = "t7",
                Name = "Turns 7-8",
                SegmentType = TrackSegmentType.CornerComplex,
                CornerNumber = 7
            },
            EntrySpeedKph = 170,
            MinSpeedKph = 120,
            ExitSpeedKph = 160,
            MaxBrake = 0.5,
            TimeLossToReferenceInMs = 120,
            Confidence = ConfidenceLevel.Medium,
            Warnings = []
        });
    }

    private static HistorySessionBrowserViewModel CreateHistoryBrowserWithSupportedLap(string sessionId)
    {
        return new HistorySessionBrowserViewModel(
            new RecordingSessionRepository
            {
                Sessions =
                [
                    new StoredSession
                    {
                        Id = sessionId,
                        SessionUid = $"uid-{sessionId}",
                        TrackId = 0,
                        SessionType = 15,
                        StartedAt = DateTimeOffset.Parse("2026-05-17T10:00:00Z")
                    }
                ]
            },
            new RecordingLapRepository
            {
                Laps =
                [
                    new StoredLap { SessionId = sessionId, LapNumber = 7, IsValid = true, StartTyre = "Medium", EndTyre = "Medium", CreatedAt = DateTimeOffset.Parse("2026-05-17T10:07:00Z") }
                ]
            });
    }

    private static HistorySessionBrowserViewModel CreateHistoryBrowser(string sessionId, IReadOnlyList<StoredLap> laps)
    {
        return new HistorySessionBrowserViewModel(
            new RecordingSessionRepository
            {
                Sessions =
                [
                    new StoredSession
                    {
                        Id = sessionId,
                        SessionUid = $"uid-{sessionId}",
                        TrackId = 0,
                        SessionType = 15,
                        StartedAt = DateTimeOffset.Parse("2026-05-17T10:00:00Z")
                    }
                ]
            },
            new RecordingLapRepository { Laps = laps });
    }

    private static StoredLap CreateStoredLap(
        string sessionId,
        int lapNumber,
        int lapTimeMs,
        string compound,
        bool isValid = true)
    {
        return new StoredLap
        {
            SessionId = sessionId,
            LapNumber = lapNumber,
            LapTimeInMs = lapTimeMs,
            IsValid = isValid,
            StartTyre = compound,
            EndTyre = compound,
            CreatedAt = DateTimeOffset.Parse("2026-05-17T10:00:00Z").AddMinutes(lapNumber)
        };
    }

    private static StoredLapSample[] CreateCornerSamples(
        string sessionId,
        int lapNumber,
        int baseTimeMs,
        int? pitStatus = null,
        float? fuelLitres = 80f)
    {
        return
        [
            CreateSample(sessionId, lapNumber, 0, 270, baseTimeMs, 210, 0.8, 0.0) with
            {
                PitStatus = pitStatus,
                FuelRemainingLitres = fuelLitres
            },
            CreateSample(sessionId, lapNumber, 1, 330, baseTimeMs + 600, 160, 0.2, 0.6) with
            {
                PitStatus = pitStatus,
                FuelRemainingLitres = fuelLitres
            },
            CreateSample(sessionId, lapNumber, 2, 410, baseTimeMs + 1_500, 110, 0.1, 0.9) with
            {
                PitStatus = pitStatus,
                FuelRemainingLitres = fuelLitres
            },
            CreateSample(sessionId, lapNumber, 3, 500, baseTimeMs + 2_700, 190, 0.9, 0.0) with
            {
                PitStatus = pitStatus,
                FuelRemainingLitres = fuelLitres
            }
        ];
    }

    private static StoredLapSample[] CreateTwoSegmentSamples(
        string sessionId,
        int lapNumber,
        int firstSegmentDurationMs,
        int secondSegmentDurationMs)
    {
        return
        [
            CreateSample(sessionId, lapNumber, 0, 260, 1_000, 225, 0.8, 0.0),
            CreateSample(sessionId, lapNumber, 1, 390, 1_000 + firstSegmentDurationMs / 2, 145, 0.2, 0.8),
            CreateSample(sessionId, lapNumber, 2, 520, 1_000 + firstSegmentDurationMs, 185, 0.9, 0.0),
            CreateSample(sessionId, lapNumber, 3, 700, 3_000, 210, 0.7, 0.0),
            CreateSample(sessionId, lapNumber, 4, 850, 3_000 + secondSegmentDurationMs / 2, 130, 0.2, 0.7),
            CreateSample(sessionId, lapNumber, 5, 1_000, 3_000 + secondSegmentDurationMs, 176, 0.9, 0.0)
        ];
    }

    private static StoredLapSample CreateSample(
        string sessionId,
        int lapNumber,
        int sampleIndex,
        float distance,
        int timeMs,
        double speed,
        double throttle,
        double brake)
    {
        return new StoredLapSample
        {
            SessionId = sessionId,
            LapNumber = lapNumber,
            SampleIndex = sampleIndex,
            SampledAt = DateTimeOffset.Parse("2026-05-17T10:00:00Z").AddMilliseconds(timeMs),
            FrameIdentifier = sampleIndex,
            LapDistance = distance,
            CurrentLapTimeInMs = timeMs,
            SpeedKph = speed,
            Throttle = throttle,
            Brake = brake,
            Steering = 0.2f,
            IsValid = true,
            CreatedAt = DateTimeOffset.Parse("2026-05-17T10:00:00Z").AddMilliseconds(timeMs)
        };
    }

    private sealed class RecordingSessionRepository : ISessionRepository
    {
        public IReadOnlyList<StoredSession> Sessions { get; init; } = Array.Empty<StoredSession>();

        public Task CreateAsync(StoredSession session, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task EndAsync(string sessionId, DateTimeOffset endedAt, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<StoredSession>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Sessions);
        }

        public Task<bool> DeleteAsync(string sessionId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }
    }

    private sealed class RecordingLapRepository : ILapRepository
    {
        public IReadOnlyList<StoredLap> Laps { get; init; } = Array.Empty<StoredLap>();

        public Task AddAsync(string sessionId, F1Telemetry.Analytics.Laps.LapSummary lapSummary, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<StoredLap>> GetRecentAsync(string sessionId, int count, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<StoredLap>>(Laps.Where(lap => lap.SessionId == sessionId).ToArray());
        }
    }

    private sealed class RecordingEventRepository : IEventRepository
    {
        public IReadOnlyList<StoredEvent> Events { get; init; } = Array.Empty<StoredEvent>();

        public Task AddAsync(string sessionId, RaceEvent raceEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<StoredEvent>> GetRecentAsync(string sessionId, int count, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<StoredEvent>>(
                Events
                    .Where(storedEvent => storedEvent.SessionId == sessionId)
                    .OrderByDescending(storedEvent => storedEvent.CreatedAt)
                    .Take(count)
                    .ToArray());
        }
    }

    private sealed class RecordingLapSampleRepository : ILapSampleRepository
    {
        public IReadOnlyList<StoredLapSample> Samples { get; init; } = Array.Empty<StoredLapSample>();

        public string? RequestedSessionId { get; private set; }

        public int? RequestedLapNumber { get; private set; }

        public List<int> RequestedLapNumbers { get; } = [];

        public Task AddAsync(StoredLapSample sample, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task AddRangeAsync(IEnumerable<StoredLapSample> samples, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<StoredLapSample>> GetForLapAsync(string sessionId, int lapNumber, CancellationToken cancellationToken = default)
        {
            RequestedSessionId = sessionId;
            RequestedLapNumber = lapNumber;
            RequestedLapNumbers.Add(lapNumber);

            return Task.FromResult<IReadOnlyList<StoredLapSample>>(
                Samples
                    .Where(sample => sample.SessionId == sessionId && sample.LapNumber == lapNumber)
                    .OrderBy(sample => sample.SampleIndex)
                    .ToArray());
        }

        public Task<IReadOnlyList<StoredLapTyreWearTrendPoint>> GetTyreWearTrendAsync(
            string sessionId,
            int count,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<StoredLapTyreWearTrendPoint>>(Array.Empty<StoredLapTyreWearTrendPoint>());
        }
    }

    private sealed class RecordingAiAnalysisService : IAIAnalysisService
    {
        public AIAnalysisResult Result { get; init; } = new();

        public Queue<AIAnalysisResult> Results { get; } = new();

        public TimeSpan Delay { get; init; }

        public int RequestCount { get; private set; }

        public async Task<AIAnalysisResult> AnalyzeAsync(
            AIAnalysisContext context,
            AISettings settings,
            CancellationToken cancellationToken = default)
        {
            RequestCount++;
            Assert.Contains("Corner analysis", context.TelemetryAnalysisSummary, StringComparison.Ordinal);
            if (Delay > TimeSpan.Zero)
            {
                await Task.Delay(Delay, cancellationToken);
            }

            return Results.Count > 0 ? Results.Dequeue() : Result;
        }
    }

    private sealed class FixedSettingsStore : IAppSettingsStore
    {
        private readonly AppSettingsDocument _document;

        public FixedSettingsStore(AppSettingsDocument document)
        {
            _document = document;
        }

        public Task<AppSettingsDocument> LoadAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_document);
        }

        public Task SaveAiSettingsAsync(AISettings settings, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SaveTtsSettingsAsync(TtsOptions options, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SaveRaceWeekendTyrePlanAsync(RaceWeekendTyrePlan plan, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SaveUdpRawLogOptionsAsync(UdpRawLogOptions options, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SaveUdpSettingsAsync(UdpSettings settings, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class RecordingTtsService : ITtsService
    {
        private readonly List<string> _spokenTexts = new();

        public IReadOnlyList<string> SpokenTexts
        {
            get
            {
                lock (_spokenTexts)
                {
                    return _spokenTexts.ToArray();
                }
            }
        }

        public void Configure(string? voiceName, int volume, int rate)
        {
        }

        public Task SpeakAsync(string text, CancellationToken cancellationToken = default)
        {
            lock (_spokenTexts)
            {
                _spokenTexts.Add(text);
            }

            return Task.CompletedTask;
        }

        public async Task WaitForSpeechCountAsync(int expectedCount)
        {
            var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
            while (DateTimeOffset.UtcNow < deadline)
            {
                if (SpokenTexts.Count >= expectedCount)
                {
                    return;
                }

                await Task.Delay(25);
            }

            Assert.True(SpokenTexts.Count >= expectedCount, $"Expected at least {expectedCount} spoken TTS messages.");
        }
    }

    private sealed class ThrowingLapSampleRepository : ILapSampleRepository
    {
        public Task AddAsync(StoredLapSample sample, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task AddRangeAsync(IEnumerable<StoredLapSample> samples, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<StoredLapSample>> GetForLapAsync(string sessionId, int lapNumber, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("sample read failed");
        }

        public Task<IReadOnlyList<StoredLapTyreWearTrendPoint>> GetTyreWearTrendAsync(
            string sessionId,
            int count,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("sample read failed");
        }
    }
}
