using System.Globalization;
using System.Text;
using F1Telemetry.AI.Interfaces;
using F1Telemetry.AI.Models;
using F1Telemetry.AI.Services;
using F1Telemetry.Analytics.Corners;
using F1Telemetry.Analytics.Laps;
using F1Telemetry.Analytics.Tracks;
using F1Telemetry.Core.Formatting;
using F1Telemetry.Core.Models;
using F1Telemetry.TTS.Services;

namespace F1Telemetry.App.Services;

/// <summary>
/// Generates short realtime AI corner advice from completed laps without requiring the corner page to be open.
/// </summary>
public sealed class RealtimeCornerAdviceService
{
    private readonly IAIAnalysisService _aiAnalysisService;
    private readonly TtsMessageFactory _ttsMessageFactory;
    private readonly TtsQueue _ttsQueue;
    private readonly CornerAutoDetector _cornerAutoDetector;
    private readonly Action<string, string>? _logSink;
    private readonly object _syncRoot = new();
    private readonly HashSet<string> _completedAdviceKeys = new(StringComparer.Ordinal);
    private readonly HashSet<string> _runningAdviceKeys = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a realtime corner advice service.
    /// </summary>
    /// <param name="aiAnalysisService">The AI analysis service.</param>
    /// <param name="ttsMessageFactory">The TTS message factory.</param>
    /// <param name="ttsQueue">The TTS queue.</param>
    /// <param name="cornerAutoDetector">The corner candidate detector.</param>
    /// <param name="logSink">Optional log sink for AI/TTS status messages.</param>
    public RealtimeCornerAdviceService(
        IAIAnalysisService aiAnalysisService,
        TtsMessageFactory ttsMessageFactory,
        TtsQueue ttsQueue,
        CornerAutoDetector? cornerAutoDetector = null,
        Action<string, string>? logSink = null)
    {
        _aiAnalysisService = aiAnalysisService ?? throw new ArgumentNullException(nameof(aiAnalysisService));
        _ttsMessageFactory = ttsMessageFactory ?? throw new ArgumentNullException(nameof(ttsMessageFactory));
        _ttsQueue = ttsQueue ?? throw new ArgumentNullException(nameof(ttsQueue));
        _cornerAutoDetector = cornerAutoDetector ?? new CornerAutoDetector();
        _logSink = logSink;
    }

    /// <summary>
    /// Clears session-scoped advice deduplication state.
    /// </summary>
    public void Reset()
    {
        lock (_syncRoot)
        {
            _completedAdviceKeys.Clear();
            _runningAdviceKeys.Clear();
        }
    }

    /// <summary>
    /// Evaluates a completed lap and generates one short realtime corner advice message when trigger rules allow it.
    /// </summary>
    /// <param name="request">The completed-lap advice request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task EvaluateCompletedLapAsync(
        RealtimeCornerAdviceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!ShouldAttemptAdvice(request))
        {
            return;
        }

        var lapLengthMeters = ResolveLapLength(request.SessionState.TrackId);
        var candidates = _cornerAutoDetector.Detect(
            request.SessionState.TrackId,
            lapLengthMeters,
            request.LapSamples);
        if (candidates.Count == 0)
        {
            return;
        }

        var adviceKey = BuildAdviceKey(request);
        lock (_syncRoot)
        {
            if (_completedAdviceKeys.Contains(adviceKey) ||
                !_runningAdviceKeys.Add(adviceKey))
            {
                return;
            }
        }

        try
        {
            var context = BuildAiContext(request, candidates, lapLengthMeters);
            var result = await _aiAnalysisService.AnalyzeAsync(context, request.AiSettings, cancellationToken);
            if (!result.IsSuccess)
            {
                Log("AI", $"实时弯角建议失败：Lap {request.CompletedLap.LapNumber} · {result.ErrorMessage}");
                return;
            }

            var speechText = string.IsNullOrWhiteSpace(result.TtsText) || result.TtsText.Trim() == "-"
                ? result.Tts
                : result.TtsText;
            var message = _ttsMessageFactory.CreateForEngineerAdvice(adviceKey, speechText, request.TtsOptions);
            if (message is null)
            {
                return;
            }

            var accepted = _ttsQueue.TryEnqueue(message);
            Log("AI", accepted
                ? $"实时弯角建议已加入 TTS：Lap {request.CompletedLap.LapNumber} · {message.Text}"
                : $"实时弯角建议未被 TTS 队列接受：Lap {request.CompletedLap.LapNumber}");
            if (accepted)
            {
                lock (_syncRoot)
                {
                    _completedAdviceKeys.Add(adviceKey);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Log("AI", $"实时弯角建议失败：Lap {request.CompletedLap.LapNumber} · {ex.Message}");
        }
        finally
        {
            lock (_syncRoot)
            {
                _runningAdviceKeys.Remove(adviceKey);
            }
        }
    }

    private static bool ShouldAttemptAdvice(RealtimeCornerAdviceRequest request)
    {
        if (!request.AiSettings.AiEnabled ||
            string.IsNullOrWhiteSpace(request.AiSettings.ApiKey) ||
            !request.TtsOptions.TtsEnabled ||
            request.LapSamples.Count == 0 ||
            request.CompletedLap.LapNumber < 2)
        {
            return false;
        }

        var sessionMode = ResolveSessionMode(request.SessionState);
        if (sessionMode is not (SessionMode.Practice or SessionMode.SprintRace or SessionMode.Race))
        {
            return false;
        }

        var completedLapCount = request.RecentCompletedLaps
            .Select(lap => lap.LapNumber)
            .Where(lapNumber => lapNumber <= request.CompletedLap.LapNumber)
            .Distinct()
            .Count();
        return completedLapCount >= 2 && completedLapCount % 2 == 0;
    }

    private static AIAnalysisContext BuildAiContext(
        RealtimeCornerAdviceRequest request,
        IReadOnlyList<DetectedCornerCandidate> candidates,
        float? lapLengthMeters)
    {
        var sessionMode = ResolveSessionMode(request.SessionState);
        var playerCar = request.SessionState.PlayerCar;
        return new AIAnalysisContext
        {
            SessionMode = sessionMode,
            SessionTypeText = SessionModeFormatter.FormatDisplayName(sessionMode),
            SessionFocusText = "实时弯角建议：只输出一条中文驾驶动作，重点关注刹车点、最小速度、出弯给油和下一圈执行。",
            LatestLap = request.CompletedLap,
            RecentLaps = request.RecentCompletedLaps,
            CurrentFuelRemainingLaps = playerCar?.FuelRemainingLaps,
            CurrentFuelInTank = playerCar?.FuelInTank,
            CurrentErsStoreEnergy = playerCar?.ErsStoreEnergy,
            CurrentTyreAgeLaps = playerCar?.TyresAgeLaps,
            CurrentTyre = playerCar is null ? "-" : BuildTyreText(playerCar),
            TelemetryAnalysisSummary = BuildTelemetrySummary(request, candidates, lapLengthMeters),
            RealtimeEngineerAdviceSummary = "每 2 个完成圈自动触发；只播报一条最重要的中文驾驶动作建议。",
            RecentEvents = candidates
                .Select(candidate => $"{candidate.CornerLabel}: 自动识别/估算窗口 {candidate.StartDistanceMeters:0}-{candidate.EndDistanceMeters:0}m，置信度 {candidate.Confidence}")
                .ToArray()
        };
    }

    private static string BuildTelemetrySummary(
        RealtimeCornerAdviceRequest request,
        IReadOnlyList<DetectedCornerCandidate> candidates,
        float? lapLengthMeters)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Realtime corner advice request. Use Chinese only.");
        builder.AppendLine("Return the tts field as one short driving action, no full corner list, no post-race summary.");
        builder.AppendLine($"Completed lap: {request.CompletedLap.LapNumber}");
        builder.AppendLine("Detected corner candidates:");

        foreach (var candidate in candidates)
        {
            var windowSamples = SelectCandidateSamples(candidate, request.LapSamples, lapLengthMeters);
            var minSpeed = windowSamples
                .Select(sample => sample.SpeedKph)
                .Where(speed => speed is not null)
                .Select(speed => speed!.Value)
                .DefaultIfEmpty(0d)
                .Min();
            var maxBrake = windowSamples
                .Select(sample => sample.Brake)
                .Where(brake => brake is not null)
                .Select(brake => brake!.Value)
                .DefaultIfEmpty(0d)
                .Max();
            builder.Append("  - ");
            builder.Append(candidate.CornerLabel);
            builder.Append(' ');
            builder.Append(candidate.DisplayName);
            builder.Append(", min speed ");
            builder.Append(minSpeed.ToString("0", CultureInfo.InvariantCulture));
            builder.Append(" km/h, max brake ");
            builder.Append(maxBrake.ToString("P0", CultureInfo.InvariantCulture));
            builder.Append(", confidence ");
            builder.AppendLine(candidate.Confidence.ToString());
        }

        builder.Append("Pick the single highest-impact corner and give one concrete action for the next lap.");
        return builder.ToString();
    }

    private static IReadOnlyList<LapSample> SelectCandidateSamples(
        DetectedCornerCandidate candidate,
        IReadOnlyList<LapSample> samples,
        float? lapLengthMeters)
    {
        var segment = candidate.ToTrackSegment();
        return samples
            .Where(sample => sample.LapDistance is not null &&
                segment.ContainsDistance(LapDistanceNormalizer.Normalize(sample.LapDistance.Value, lapLengthMeters)))
            .ToArray();
    }

    private static string BuildAdviceKey(RealtimeCornerAdviceRequest request)
    {
        var sessionToken = request.ActiveSessionUid?.ToString(CultureInfo.InvariantCulture) ?? "unknown";
        return $"{sessionToken}:lap{request.CompletedLap.LapNumber}";
    }

    private static SessionMode ResolveSessionMode(SessionState sessionState)
    {
        return SessionModeFormatter.Resolve(
            sessionState.SessionType,
            sessionState.TotalLaps,
            sessionState.WeekendStructure);
    }

    private static float? ResolveLapLength(sbyte? trackId)
    {
        return trackId == 11 ? 5_798f : null;
    }

    private static string BuildTyreText(CarSnapshot playerCar)
    {
        return playerCar.VisualTyreCompound is null && playerCar.ActualTyreCompound is null
            ? "-"
            : $"Visual {playerCar.VisualTyreCompound?.ToString(CultureInfo.InvariantCulture) ?? "-"} / Actual {playerCar.ActualTyreCompound?.ToString(CultureInfo.InvariantCulture) ?? "-"}";
    }

    private void Log(string category, string message)
    {
        _logSink?.Invoke(category, message);
    }
}
