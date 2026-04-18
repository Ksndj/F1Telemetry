using F1Telemetry.Storage.Interfaces;
using F1Telemetry.Storage.Services;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies SQLite database initialization and schema creation behavior.
/// </summary>
public sealed class SqliteDatabaseServiceTests
{
    /// <summary>
    /// Verifies that initialization creates the database file and required tables.
    /// </summary>
    [Fact]
    public async Task InitializeAsync_CreatesDatabaseFileAndTables()
    {
        var rootPath = CreateRootPath();
        await using IDatabaseService databaseService = new SqliteDatabaseService(rootPath);

        await databaseService.InitializeAsync();

        Assert.True(File.Exists(databaseService.DatabasePath));

        var tableNames = await databaseService.ExecuteAsync(
            async (connection, cancellationToken) =>
            {
                using var command = connection.CreateCommand();
                command.CommandText = """
                    SELECT name
                    FROM sqlite_master
                    WHERE type = 'table'
                      AND name IN ('sessions', 'laps', 'events', 'ai_reports', 'settings')
                    ORDER BY name;
                    """;

                using var reader = await command.ExecuteReaderAsync(cancellationToken);
                var results = new List<string>();
                while (await reader.ReadAsync(cancellationToken))
                {
                    results.Add(reader.GetString(0));
                }

                return results;
            });

        Assert.Equal(
            ["ai_reports", "events", "laps", "sessions", "settings"],
            tableNames);
    }

    /// <summary>
    /// Verifies that initialization creates the key recent-query indexes.
    /// </summary>
    [Fact]
    public async Task InitializeAsync_CreatesRecentQueryIndexes()
    {
        var rootPath = CreateRootPath();
        await using IDatabaseService databaseService = new SqliteDatabaseService(rootPath);

        await databaseService.InitializeAsync();

        var indexNames = await databaseService.ExecuteAsync(
            async (connection, cancellationToken) =>
            {
                using var command = connection.CreateCommand();
                command.CommandText = """
                    SELECT name
                    FROM sqlite_master
                    WHERE type = 'index'
                      AND name IN (
                          'idx_laps_session_created_at_desc',
                          'idx_events_session_created_at_desc',
                          'idx_ai_reports_session_created_at_desc')
                    ORDER BY name;
                    """;

                using var reader = await command.ExecuteReaderAsync(cancellationToken);
                var results = new List<string>();
                while (await reader.ReadAsync(cancellationToken))
                {
                    results.Add(reader.GetString(0));
                }

                return results;
            });

        Assert.Equal(
            [
                "idx_ai_reports_session_created_at_desc",
                "idx_events_session_created_at_desc",
                "idx_laps_session_created_at_desc"
            ],
            indexNames);
    }

    private static string CreateRootPath()
    {
        return Path.Combine(Path.GetTempPath(), "F1TelemetryTests", Guid.NewGuid().ToString("N"));
    }
}
