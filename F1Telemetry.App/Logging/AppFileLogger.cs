using F1Telemetry.Core;
using F1Telemetry.Core.Models;
using F1Telemetry.Core.Security;

namespace F1Telemetry.App.Logging;

/// <summary>
/// Writes categorized application events to an asynchronous JSON-line file log.
/// </summary>
public sealed class AppFileLogger : IAsyncDisposable, IDisposable
{
    private const int SchemaVersion = 1;
    private readonly AppRunContext _runContext;
    private readonly AsyncJsonLineLogWriter<AppFileLogRecord> _writer;

    /// <summary>
    /// Initializes a new categorized app file logger.
    /// </summary>
    public AppFileLogger(AppRunContext runContext, string? directoryPath = null)
    {
        _runContext = runContext ?? throw new ArgumentNullException(nameof(runContext));
        var logDirectory = string.IsNullOrWhiteSpace(directoryPath) ? AppPaths.GetAppLogDir() : directoryPath.Trim();
        _writer = new AsyncJsonLineLogWriter<AppFileLogRecord>(
            () => logDirectory,
            settings => settings.EnableAppFileLog,
            "app",
            ".log");
    }

    /// <summary>
    /// Gets the logger status for Settings.
    /// </summary>
    public LogWriterStatus Status => _writer.Status;

    /// <summary>
    /// Updates settings used by future app log records.
    /// </summary>
    public void UpdateSettings(LogSettings settings)
    {
        _writer.UpdateSettings(settings);
    }

    /// <summary>
    /// Enqueues one categorized log record without waiting for file I/O.
    /// </summary>
    public bool TryEnqueue(
        string category,
        string message,
        string level = "Info",
        ulong? sessionUid = null,
        int? lap = null,
        string? questionId = null,
        string? exception = null)
    {
        var timestamp = DateTimeOffset.Now;
        return _writer.TryEnqueue(new AppFileLogRecord
        {
            SchemaVersion = SchemaVersion,
            RunId = _runContext.RunId,
            Timestamp = timestamp,
            ElapsedMsSinceRunStart = _runContext.GetElapsedMilliseconds(timestamp),
            Category = NormalizeCategory(category, message),
            Level = NormalizeLevel(level),
            Message = SensitiveContentSanitizer.Sanitize(message),
            SessionUid = sessionUid,
            Lap = lap,
            QuestionId = string.IsNullOrWhiteSpace(questionId) ? null : SensitiveContentSanitizer.Sanitize(questionId),
            Exception = SensitiveContentSanitizer.SanitizeNullable(exception)
        });
    }

    /// <summary>
    /// Waits briefly for pending app log records to be written.
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

    private static string NormalizeLevel(string? level)
    {
        return string.Equals(level, "Warning", StringComparison.OrdinalIgnoreCase)
            ? "Warning"
            : string.Equals(level, "Error", StringComparison.OrdinalIgnoreCase)
                ? "Error"
                : "Info";
    }

    private static string NormalizeCategory(string? category, string? message)
    {
        var normalized = LogCategoryFormatter.Normalize(category, message);
        return normalized is "System" or "UDP" or "RaceEvent" or "AI" or "VoiceAI" or "RaceAssistant" or "TTS" or "Storage"
            ? normalized
            : "System";
    }

    private sealed record AppFileLogRecord
    {
        public int SchemaVersion { get; init; }

        public string RunId { get; init; } = string.Empty;

        public DateTimeOffset Timestamp { get; init; }

        public long ElapsedMsSinceRunStart { get; init; }

        public string Category { get; init; } = "System";

        public string Level { get; init; } = "Info";

        public string Message { get; init; } = string.Empty;

        public ulong? SessionUid { get; init; }

        public int? Lap { get; init; }

        public string? QuestionId { get; init; }

        public string? Exception { get; init; }
    }
}
