using F1Telemetry.App.ViewModels;
using F1Telemetry.Storage.Interfaces;
using F1Telemetry.Storage.Models;
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

        public Task AddAsync(StoredLapSample sample, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task AddRangeAsync(IEnumerable<StoredLapSample> samples, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<StoredLapSample>> GetForLapAsync(string sessionId, int lapNumber, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<StoredLapSample>>(
                Samples
                    .Where(sample => sample.SessionId == sessionId && sample.LapNumber == lapNumber)
                    .OrderBy(sample => sample.SampleIndex)
                    .ToArray());
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
    }
}
