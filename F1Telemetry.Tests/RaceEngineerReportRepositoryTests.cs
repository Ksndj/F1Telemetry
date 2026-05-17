using System.IO;
using F1Telemetry.Storage.Interfaces;
using F1Telemetry.Storage.Models;
using F1Telemetry.Storage.Repositories;
using F1Telemetry.Storage.Services;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies V3 race-engineer report persistence applies final redaction before storage.
/// </summary>
public sealed class RaceEngineerReportRepositoryTests
{
    /// <summary>
    /// Verifies report text and detail JSON are redacted before SQLite persistence.
    /// </summary>
    [Fact]
    public async Task AddAsync_WithSensitiveReportContent_RedactsStoredFields()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), "F1TelemetryTests", Guid.NewGuid().ToString("N"));
        await using IDatabaseService databaseService = new SqliteDatabaseService(rootPath);
        await databaseService.InitializeAsync();
        var repository = new RaceEngineerReportRepository(databaseService);

        await repository.AddAsync(
            new StoredRaceEngineerReport
            {
                SessionId = "session-report",
                LapNumber = 8,
                ReportType = "V3",
                Summary = "API Key should not persist. Authorization: Bearer summary-secret",
                SpokenText = "token=spoken-secret password=spoken-password raw.jsonl packetId m_header",
                DetailJson = """
                    {"apiKey":"detail-secret","Authorization":"Bearer detail-token","packetId":12,"m_header":{"sessionUid":99},"payloadBase64":"raw-payload"}
                    """,
                IsSuccess = true,
                ErrorMessage = "secret=error-secret",
                CreatedAt = DateTimeOffset.Parse("2026-05-17T10:08:00Z")
            });

        var stored = Assert.Single(await repository.GetRecentAsync("session-report", 10));
        var combined = string.Join(
            "\n",
            stored.Summary,
            stored.SpokenText,
            stored.DetailJson,
            stored.ErrorMessage);

        Assert.DoesNotContain("summary-secret", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("spoken-secret", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("spoken-password", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("detail-secret", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("detail-token", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("raw-payload", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("error-secret", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("packetId", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("m_header", combined, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[REDACTED]", combined, StringComparison.Ordinal);
    }
}
