using F1Telemetry.AI.Interfaces;
using F1Telemetry.AI.Models;
using F1Telemetry.AI.Services;
using F1Telemetry.Analytics.Laps;
using F1Telemetry.App.Services;
using F1Telemetry.Core.Interfaces;
using F1Telemetry.Core.Models;
using F1Telemetry.TTS.Models;
using F1Telemetry.TTS.Services;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies realtime corner advice trigger pacing and session filtering.
/// </summary>
public sealed class RealtimeCornerAdviceServiceTests
{
    /// <summary>
    /// Verifies realtime corner advice triggers from the second completed lap and then every two laps.
    /// </summary>
    [Fact]
    public async Task EvaluateCompletedLapAsync_PracticeRaceAndSprintRace_TriggersOnLapsTwoFourSix()
    {
        var aiService = new RecordingAiAnalysisService();
        using var queue = new TtsQueue(new RecordingTtsService(), new TtsOptions { TtsEnabled = true });
        var service = new RealtimeCornerAdviceService(aiService, new TtsMessageFactory(), queue);

        for (var lapNumber = 1; lapNumber <= 6; lapNumber++)
        {
            await service.EvaluateCompletedLapAsync(CreateRequest(lapNumber, sessionType: 17));
        }

        Assert.Equal(new[] { 2, 4, 6 }, aiService.Contexts.Select(context => context.LatestLap!.LapNumber));
    }

    /// <summary>
    /// Verifies qualifying and time-trial sessions never trigger realtime corner speech.
    /// </summary>
    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(18)]
    public async Task EvaluateCompletedLapAsync_UnsupportedSessionModes_DoNotTrigger(byte sessionType)
    {
        var aiService = new RecordingAiAnalysisService();
        using var queue = new TtsQueue(new RecordingTtsService(), new TtsOptions { TtsEnabled = true });
        var service = new RealtimeCornerAdviceService(aiService, new TtsMessageFactory(), queue);

        await service.EvaluateCompletedLapAsync(CreateRequest(2, sessionType));

        Assert.Empty(aiService.Contexts);
    }

    /// <summary>
    /// Verifies the same session/lap is generated at most once even if persistence retries a better-quality summary.
    /// </summary>
    [Fact]
    public async Task EvaluateCompletedLapAsync_SameSessionLap_DeduplicatesBeforeAiCall()
    {
        var aiService = new RecordingAiAnalysisService();
        using var queue = new TtsQueue(new RecordingTtsService(), new TtsOptions { TtsEnabled = true });
        var service = new RealtimeCornerAdviceService(aiService, new TtsMessageFactory(), queue);
        var request = CreateRequest(2, sessionType: 1);

        await service.EvaluateCompletedLapAsync(request);
        await service.EvaluateCompletedLapAsync(request);

        Assert.Single(aiService.Contexts);
    }

    /// <summary>
    /// Verifies trigger cadence follows completed-lap count instead of the absolute lap number.
    /// </summary>
    [Fact]
    public async Task EvaluateCompletedLapAsync_SecondCompletedLapWithOddLapNumber_Triggers()
    {
        var aiService = new RecordingAiAnalysisService();
        using var queue = new TtsQueue(new RecordingTtsService(), new TtsOptions { TtsEnabled = true });
        var service = new RealtimeCornerAdviceService(aiService, new TtsMessageFactory(), queue);
        var request = CreateRequest(7, sessionType: 17) with
        {
            RecentCompletedLaps = new[]
            {
                new LapSummary { LapNumber = 6, IsValid = true, ClosedAt = DateTimeOffset.UtcNow },
                new LapSummary { LapNumber = 7, IsValid = true, ClosedAt = DateTimeOffset.UtcNow }
            }
        };

        await service.EvaluateCompletedLapAsync(request);

        var context = Assert.Single(aiService.Contexts);
        Assert.Equal(7, context.LatestLap!.LapNumber);
    }

    /// <summary>
    /// Verifies a transient AI failure does not permanently consume realtime advice deduplication.
    /// </summary>
    [Fact]
    public async Task EvaluateCompletedLapAsync_AiFailure_AllowsSameLapRetry()
    {
        var aiService = new FailingThenSuccessAiAnalysisService();
        using var queue = new TtsQueue(new RecordingTtsService(), new TtsOptions { TtsEnabled = true });
        var service = new RealtimeCornerAdviceService(aiService, new TtsMessageFactory(), queue);
        var request = CreateRequest(2, sessionType: 17);

        await service.EvaluateCompletedLapAsync(request);
        await service.EvaluateCompletedLapAsync(request);

        Assert.Equal(2, aiService.Contexts.Count);
    }

    private static RealtimeCornerAdviceRequest CreateRequest(int lapNumber, byte sessionType)
    {
        return new RealtimeCornerAdviceRequest
        {
            SessionState = new SessionState
            {
                SessionType = sessionType,
                TrackId = 11,
                TotalLaps = 53,
                PlayerCar = new CarSnapshot
                {
                    FuelRemainingLaps = 8.2f,
                    FuelInTank = 18.5f,
                    ErsStoreEnergy = 2_500_000f
                }
            },
            ActiveSessionUid = 123,
            CompletedLap = new LapSummary
            {
                LapNumber = lapNumber,
                LapTimeInMs = 85_000,
                IsValid = true,
                ClosedAt = DateTimeOffset.UtcNow
            },
            LapSamples = CreateMonzaStyleSamples(lapNumber),
            RecentCompletedLaps = Enumerable.Range(1, lapNumber)
                .Select(number => new LapSummary
                {
                    LapNumber = number,
                    LapTimeInMs = (uint)(86_000 - number * 100),
                    IsValid = true,
                    ClosedAt = DateTimeOffset.UtcNow
                })
                .ToArray(),
            AiSettings = new AISettings
            {
                AiEnabled = true,
                ApiKey = "test-key"
            },
            TtsOptions = new TtsOptions
            {
                TtsEnabled = true,
                CooldownSeconds = 1
            }
        };
    }

    private static IReadOnlyList<LapSample> CreateMonzaStyleSamples(int lapNumber)
    {
        return new[]
        {
            CreateSample(lapNumber, 520f, 305, 0.05, 0.92, 0.30f),
            CreateSample(lapNumber, 720f, 92, 0.20, 0.45, -0.60f),
            CreateSample(lapNumber, 940f, 170, 0.82, 0.02, 0.10f),
            CreateSample(lapNumber, 1_920f, 290, 0.05, 0.88, 0.24f),
            CreateSample(lapNumber, 2_120f, 128, 0.45, 0.20, -0.40f),
            CreateSample(lapNumber, 2_300f, 205, 0.90, 0.01, 0.08f),
            CreateSample(lapNumber, 2_560f, 260, 0.10, 0.45, 0.65f),
            CreateSample(lapNumber, 3_060f, 215, 0.88, 0.00, -0.62f),
            CreateSample(lapNumber, 4_120f, 300, 0.08, 0.78, 0.42f),
            CreateSample(lapNumber, 4_660f, 232, 0.86, 0.00, -0.35f),
            CreateSample(lapNumber, 5_120f, 318, 0.12, 0.65, 0.55f),
            CreateSample(lapNumber, 5_680f, 226, 0.92, 0.00, -0.52f)
        };
    }

    private static LapSample CreateSample(int lapNumber, float distance, double speed, double throttle, double brake, float steering)
    {
        return new LapSample
        {
            LapNumber = lapNumber,
            LapDistance = distance,
            SpeedKph = speed,
            Throttle = throttle,
            Brake = brake,
            Steering = steering,
            IsValid = true
        };
    }

    private sealed class RecordingAiAnalysisService : IAIAnalysisService
    {
        public List<AIAnalysisContext> Contexts { get; } = new();

        public Task<AIAnalysisResult> AnalyzeAsync(
            AIAnalysisContext context,
            AISettings settings,
            CancellationToken cancellationToken = default)
        {
            Contexts.Add(context);
            return Task.FromResult(new AIAnalysisResult
            {
                IsSuccess = true,
                Tts = "二号弯少晚刹，出弯更早给油。"
            });
        }
    }

    private sealed class FailingThenSuccessAiAnalysisService : IAIAnalysisService
    {
        private int _callCount;

        public List<AIAnalysisContext> Contexts { get; } = new();

        public Task<AIAnalysisResult> AnalyzeAsync(
            AIAnalysisContext context,
            AISettings settings,
            CancellationToken cancellationToken = default)
        {
            Contexts.Add(context);
            _callCount++;
            return Task.FromResult(_callCount == 1
                ? new AIAnalysisResult
                {
                    IsSuccess = false,
                    ErrorMessage = "temporary failure"
                }
                : new AIAnalysisResult
                {
                    IsSuccess = true,
                    Tts = "二号弯减少滑行，早一点回油。"
                });
        }
    }

    private sealed class RecordingTtsService : ITtsService
    {
        public void Configure(string? voiceName, int volume, int rate)
        {
        }

        public Task SpeakAsync(string text, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
