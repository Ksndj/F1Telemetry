using F1Telemetry.Core;
using F1Telemetry.Core.Models;
using F1Telemetry.Core.Security;
using System.IO;

namespace F1Telemetry.App.Logging;

/// <summary>
/// Writes RaceAssistant question-answer audit records to asynchronous JSONL.
/// </summary>
public sealed class RaceAssistantAuditLogger : IAsyncDisposable, IDisposable
{
    private readonly AsyncJsonLineLogWriter<RaceAssistantAuditRecord> _writer;

    /// <summary>
    /// Initializes a new RaceAssistant audit logger.
    /// </summary>
    public RaceAssistantAuditLogger(AppRunContext runContext, string? directoryPath = null)
    {
        ArgumentNullException.ThrowIfNull(runContext);
        RunContext = runContext;
        var logDirectory = string.IsNullOrWhiteSpace(directoryPath) ? AppPaths.GetRaceAssistantLogDir() : directoryPath.Trim();
        _writer = new AsyncJsonLineLogWriter<RaceAssistantAuditRecord>(
            () => logDirectory,
            settings => settings.EnableRaceAssistantAuditLog,
            "race-assistant",
            ".jsonl");
    }

    /// <summary>
    /// Gets the logger status for Settings.
    /// </summary>
    public LogWriterStatus Status => _writer.Status;

    /// <summary>
    /// Gets the run context used for audit correlation ids.
    /// </summary>
    public AppRunContext RunContext { get; }

    /// <summary>
    /// Updates settings used by future audit records.
    /// </summary>
    public void UpdateSettings(LogSettings settings)
    {
        _writer.UpdateSettings(settings);
    }

    /// <summary>
    /// Enqueues one audit record without waiting for file I/O.
    /// </summary>
    public bool TryEnqueue(RaceAssistantAuditRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return _writer.TryEnqueue(SanitizeRecord(record));
    }

    /// <summary>
    /// Waits briefly for pending audit records to be written.
    /// </summary>
    public Task FlushAsync(TimeSpan? timeout = null)
    {
        return _writer.FlushAsync(timeout);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _writer.Dispose();
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        return _writer.DisposeAsync();
    }

    private static RaceAssistantAuditRecord SanitizeRecord(RaceAssistantAuditRecord record)
    {
        return record with
        {
            Question = SensitiveContentSanitizer.Sanitize(record.Question),
            RecognizedText = SensitiveContentSanitizer.Sanitize(record.RecognizedText),
            UdpRawLogFile = SummarizeUdpRawLogFile(record.UdpRawLogFile),
            MissingData = SanitizeList(record.MissingData),
            FailureReason = SensitiveContentSanitizer.Sanitize(record.FailureReason),
            SpeechSkippedReason = SensitiveContentSanitizer.Sanitize(record.SpeechSkippedReason),
            RuleSignals = record.RuleSignals.Select(SanitizeRuleSignal).ToArray(),
            PitDecisionSignal = SanitizeSignal(record.PitDecisionSignal),
            SafetyCarPitOpportunitySignal = SanitizeSignal(record.SafetyCarPitOpportunitySignal),
            Result = SanitizeResult(record.Result)
        };
    }

    private static RaceAssistantAuditSignal? SanitizeSignal(RaceAssistantAuditSignal? signal)
    {
        return signal is null
            ? null
            : signal with
            {
                Summary = SensitiveContentSanitizer.Sanitize(signal.Summary),
                RecommendedAction = SensitiveContentSanitizer.Sanitize(signal.RecommendedAction),
                MissingData = SanitizeList(signal.MissingData)
            };
    }

    private static RaceAssistantAuditSignal SanitizeRuleSignal(RaceAssistantAuditSignal signal)
    {
        return SanitizeSignal(signal) ?? new RaceAssistantAuditSignal();
    }

    private static RaceAssistantAuditResult? SanitizeResult(RaceAssistantAuditResult? result)
    {
        return result is null
            ? null
            : result with
            {
                Summary = SensitiveContentSanitizer.Sanitize(result.Summary),
                Reason = SensitiveContentSanitizer.Sanitize(result.Reason),
                RecommendedAction = SensitiveContentSanitizer.Sanitize(result.RecommendedAction),
                MissingData = SanitizeList(result.MissingData),
                Tts = SensitiveContentSanitizer.Sanitize(result.Tts)
            };
    }

    private static IReadOnlyList<string> SanitizeList(IEnumerable<string> values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(SensitiveContentSanitizer.Sanitize)
            .ToArray();
    }

    private static string SummarizeUdpRawLogFile(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        try
        {
            var fileName = Path.GetFileName(value.Trim());
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return string.Empty;
            }

            return fileName
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Trim();
        }
        catch
        {
            return string.Empty;
        }
    }
}

/// <summary>
/// Represents one RaceAssistant question-answer audit JSONL record.
/// </summary>
public sealed record RaceAssistantAuditRecord
{
    public int SchemaVersion { get; init; } = 1;

    public string RunId { get; init; } = string.Empty;

    public string QuestionId { get; init; } = string.Empty;

    public DateTimeOffset Timestamp { get; init; }

    public long ElapsedMsSinceRunStart { get; init; }

    public ulong? SessionUid { get; init; }

    public string Track { get; init; } = string.Empty;

    public string SessionType { get; init; } = string.Empty;

    public int? Lap { get; init; }

    public string UdpRawLogFile { get; init; } = string.Empty;

    public string Question { get; init; } = string.Empty;

    public string RecognizedText { get; init; } = string.Empty;

    public string Intent { get; init; } = string.Empty;

    public string IntentDisplayName { get; init; } = string.Empty;

    public string Mode { get; init; } = string.Empty;

    public string ModeDisplayName { get; init; } = string.Empty;

    public int? SnapshotAgeMs { get; init; }

    public IReadOnlyList<string> MissingData { get; init; } = Array.Empty<string>();

    public IReadOnlyList<RaceAssistantAuditSignal> RuleSignals { get; init; } = Array.Empty<RaceAssistantAuditSignal>();

    public RaceAssistantAuditSignal? PitDecisionSignal { get; init; }

    public RaceAssistantAuditSignal? SafetyCarPitOpportunitySignal { get; init; }

    public RaceAssistantAuditResult? Result { get; init; }

    public bool UsedFallback { get; init; }

    public string FailureReason { get; init; } = string.Empty;

    public bool TtsQueued { get; init; }

    public string SpeechSkippedReason { get; init; } = string.Empty;

    public long? AiLatencyMs { get; init; }
}

/// <summary>
/// Represents a compact RaceAssistant rule or decision signal.
/// </summary>
public sealed record RaceAssistantAuditSignal
{
    public string SignalType { get; init; } = string.Empty;

    public string AdviceType { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string RecommendedAction { get; init; } = string.Empty;

    public string Confidence { get; init; } = string.Empty;

    public string RiskLevel { get; init; } = string.Empty;

    public IReadOnlyList<string> MissingData { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Represents the compact RaceAssistant answer stored in audit JSONL.
/// </summary>
public sealed record RaceAssistantAuditResult
{
    public string AdviceType { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;

    public string RecommendedAction { get; init; } = string.Empty;

    public string Confidence { get; init; } = string.Empty;

    public string RiskLevel { get; init; } = string.Empty;

    public IReadOnlyList<string> MissingData { get; init; } = Array.Empty<string>();

    public string Tts { get; init; } = string.Empty;
}
