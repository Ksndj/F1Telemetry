using F1Telemetry.AI.Interfaces;
using F1Telemetry.AI.Models;
using F1Telemetry.Analytics.Laps;
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
        Assert.Equal(3, sampleRepository.RequestedLapNumber);
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

    private sealed class RecordingLapSampleRepository : ILapSampleRepository
    {
        public IReadOnlyList<StoredLapSample> Samples { get; init; } = Array.Empty<StoredLapSample>();

        public string? RequestedSessionId { get; private set; }

        public int? RequestedLapNumber { get; private set; }

        public Task AddAsync(StoredLapSample sample, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task AddRangeAsync(IEnumerable<StoredLapSample> samples, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<StoredLapSample>> GetForLapAsync(string sessionId, int lapNumber, CancellationToken cancellationToken = default)
        {
            RequestedSessionId = sessionId;
            RequestedLapNumber = lapNumber;

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

        public int RequestCount { get; private set; }

        public Task<AIAnalysisResult> AnalyzeAsync(
            AIAnalysisContext context,
            AISettings settings,
            CancellationToken cancellationToken = default)
        {
            RequestCount++;
            Assert.Contains("Corner analysis", context.TelemetryAnalysisSummary, StringComparison.Ordinal);
            return Task.FromResult(Result);
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
