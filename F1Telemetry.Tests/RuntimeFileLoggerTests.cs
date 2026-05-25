using System.IO;
using System.Text.Json;
using F1Telemetry.App.Logging;
using F1Telemetry.Core.Models;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies runtime app and RaceAssistant file loggers.
/// </summary>
public sealed class RuntimeFileLoggerTests
{
    /// <summary>
    /// Verifies categorized app logs write complete JSON lines with correlation fields.
    /// </summary>
    [Fact]
    public async Task AppFileLogger_WritesCategorizedJsonLine()
    {
        var directory = Path.Combine(CreateTempDirectory(), "missing-app-logs");
        var runContext = new AppRunContext("run-test", DateTimeOffset.Now);
        await using var logger = new AppFileLogger(runContext, directory);
        logger.UpdateSettings(new LogSettings { EnableAppFileLog = true });

        Assert.True(logger.TryEnqueue("VoiceAI", "语音问答开始", questionId: "q-test"));
        Assert.True(logger.TryEnqueue("RaceAssistant", "问工程师完成", questionId: "q-test"));
        await logger.FlushAsync(TimeSpan.FromSeconds(2));

        var file = Assert.Single(Directory.EnumerateFiles(directory, "app-*.log"));
        Assert.True(Directory.Exists(directory));
        var lines = await ReadSharedLinesAsync(file);
        Assert.Equal(2, lines.Length);

        using var document = JsonDocument.Parse(lines[0]);
        Assert.Equal(1, document.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("run-test", document.RootElement.GetProperty("runId").GetString());
        Assert.Equal("q-test", document.RootElement.GetProperty("questionId").GetString());
        Assert.Equal("VoiceAI", document.RootElement.GetProperty("category").GetString());
        Assert.True(document.RootElement.GetProperty("timestamp").GetDateTimeOffset().Offset != TimeSpan.Zero ||
                    document.RootElement.GetProperty("timestamp").GetString()!.Contains("+00:00", StringComparison.Ordinal));
    }

    /// <summary>
    /// Verifies RaceAssistant audit logs write safe JSONL and summarize raw log file paths.
    /// </summary>
    [Fact]
    public async Task RaceAssistantAuditLogger_WritesSafeJsonl()
    {
        var directory = CreateTempDirectory();
        var runContext = new AppRunContext("run-audit", DateTimeOffset.Now);
        await using var logger = new RaceAssistantAuditLogger(runContext, directory);
        logger.UpdateSettings(new LogSettings { EnableRaceAssistantAuditLog = true });

        Assert.True(logger.TryEnqueue(new RaceAssistantAuditRecord
        {
            RunId = "run-audit",
            QuestionId = "q-audit",
            Timestamp = DateTimeOffset.Now,
            UdpRawLogFile = @"C:\Users\driver\AppData\Roaming\F1Telemetry\.logs\udp\f1telemetry-udp.jsonl",
            Question = "Authorization: Bearer secret",
            RecognizedText = "apiKey=secret",
            Intent = "PIT_DECISION",
            IntentDisplayName = "进站判断",
            Mode = "RaceStintManagement",
            ModeDisplayName = "正赛长段管理",
            MissingData = ["payloadBase64", "lap_samples"],
            Result = new RaceAssistantAuditResult
            {
                AdviceType = "PitWindow",
                Summary = "暂不进站",
                Reason = "轮胎还能撑",
                RecommendedAction = "继续保胎",
                Confidence = "High",
                RiskLevel = "Low",
                Tts = "暂不进，继续保胎。"
            },
            TtsQueued = true
        }));
        await logger.FlushAsync(TimeSpan.FromSeconds(2));

        var file = Assert.Single(Directory.EnumerateFiles(directory, "race-assistant-*.jsonl"));
        var line = Assert.Single(await ReadSharedLinesAsync(file));
        using var document = JsonDocument.Parse(line);

        Assert.Equal(1, document.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("run-audit", document.RootElement.GetProperty("runId").GetString());
        Assert.Equal("q-audit", document.RootElement.GetProperty("questionId").GetString());
        Assert.Equal("f1telemetry-udp.jsonl", document.RootElement.GetProperty("udpRawLogFile").GetString());
        Assert.DoesNotContain(@"C:\Users\driver", line, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", line, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Authorization", line, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("payloadBase64", line, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("lap_samples", line, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies writer failures do not throw back to the caller.
    /// </summary>
    [Fact]
    public async Task AppFileLogger_WhenWriteFails_StoresWarningWithoutThrowing()
    {
        var root = CreateTempDirectory();
        var filePathAsDirectory = Path.Combine(root, "not-a-directory");
        await File.WriteAllTextAsync(filePathAsDirectory, "block directory creation");
        var logger = new AppFileLogger(new AppRunContext("run-fail", DateTimeOffset.Now), filePathAsDirectory);

        var exception = Record.Exception(() => logger.TryEnqueue("RaceAssistant", "should not throw", questionId: "q-fail"));
        await logger.FlushAsync(TimeSpan.FromMilliseconds(500));
        await logger.DisposeAsync();

        Assert.Null(exception);
        Assert.Contains("日志", logger.Status.LastWarning, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies dispose flushes pending audit records within the safety timeout.
    /// </summary>
    [Fact]
    public async Task RaceAssistantAuditLogger_DisposeFlushesPendingRecords()
    {
        var directory = CreateTempDirectory();
        var logger = new RaceAssistantAuditLogger(new AppRunContext("run-dispose", DateTimeOffset.Now), directory);

        logger.TryEnqueue(new RaceAssistantAuditRecord
        {
            RunId = "run-dispose",
            QuestionId = "q-dispose",
            Timestamp = DateTimeOffset.Now,
            RecognizedText = "现在整体情况怎么样？"
        });

        await logger.DisposeAsync();

        var file = Assert.Single(Directory.EnumerateFiles(directory, "race-assistant-*.jsonl"));
        var line = Assert.Single(await File.ReadAllLinesAsync(file));
        using var document = JsonDocument.Parse(line);
        Assert.Equal("q-dispose", document.RootElement.GetProperty("questionId").GetString());
    }

    private static async Task<string[]> ReadSharedLinesAsync(string file)
    {
        await using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        return (await reader.ReadToEndAsync()).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "F1TelemetryTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
