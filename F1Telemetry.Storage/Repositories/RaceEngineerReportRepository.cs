using F1Telemetry.Storage.Interfaces;
using F1Telemetry.Storage.Internal;
using F1Telemetry.Storage.Models;

namespace F1Telemetry.Storage.Repositories;

/// <summary>
/// Stores race-engineer reports in SQLite.
/// </summary>
public sealed class RaceEngineerReportRepository : IRaceEngineerReportRepository
{
    private readonly IDatabaseService _databaseService;

    /// <summary>
    /// Initializes a new race-engineer report repository.
    /// </summary>
    public RaceEngineerReportRepository(IDatabaseService databaseService)
    {
        _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
    }

    /// <inheritdoc />
    public Task AddAsync(StoredRaceEngineerReport report, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);

        return _databaseService.ExecuteAsync(
            async (connection, innerCancellationToken) =>
            {
                using var command = connection.CreateCommand();
                command.CommandText = """
                    INSERT INTO race_engineer_reports (
                        session_id,
                        lap_number,
                        report_type,
                        summary,
                        spoken_text,
                        detail_json,
                        is_success,
                        error_message,
                        created_at)
                    VALUES (
                        @session_id,
                        @lap_number,
                        @report_type,
                        @summary,
                        @spoken_text,
                        @detail_json,
                        @is_success,
                        @error_message,
                        @created_at);
                    """;
                AddParameters(command, report);
                await command.ExecuteNonQueryAsync(innerCancellationToken);
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<StoredRaceEngineerReport>> GetRecentAsync(
        string sessionId,
        int count,
        CancellationToken cancellationToken = default)
    {
        return QueryAsync(
            """
            WHERE session_id = @session_id
            ORDER BY created_at DESC
            LIMIT @count;
            """,
            command =>
            {
                command.Parameters.AddWithValue("@session_id", sessionId);
                command.Parameters.AddWithValue("@count", count);
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<StoredRaceEngineerReport>> GetForLapAsync(
        string sessionId,
        int lapNumber,
        int count,
        CancellationToken cancellationToken = default)
    {
        return QueryAsync(
            """
            WHERE session_id = @session_id
              AND lap_number = @lap_number
            ORDER BY created_at DESC
            LIMIT @count;
            """,
            command =>
            {
                command.Parameters.AddWithValue("@session_id", sessionId);
                command.Parameters.AddWithValue("@lap_number", lapNumber);
                command.Parameters.AddWithValue("@count", count);
            },
            cancellationToken);
    }

    private Task<IReadOnlyList<StoredRaceEngineerReport>> QueryAsync(
        string whereClause,
        Action<Microsoft.Data.Sqlite.SqliteCommand> addParameters,
        CancellationToken cancellationToken)
    {
        return _databaseService.ExecuteAsync(
            async (connection, innerCancellationToken) =>
            {
                using var command = connection.CreateCommand();
                command.CommandText = $"""
                    SELECT id,
                           session_id,
                           lap_number,
                           report_type,
                           summary,
                           spoken_text,
                           detail_json,
                           is_success,
                           error_message,
                           created_at
                    FROM race_engineer_reports
                    {whereClause}
                    """;
                addParameters(command);

                using var reader = await command.ExecuteReaderAsync(innerCancellationToken);
                var results = new List<StoredRaceEngineerReport>();
                while (await reader.ReadAsync(innerCancellationToken))
                {
                    results.Add(
                        new StoredRaceEngineerReport
                        {
                            Id = reader.GetInt64(0),
                            SessionId = reader.GetString(1),
                            LapNumber = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                            ReportType = reader.GetString(3),
                            Summary = reader.GetString(4),
                            SpokenText = reader.GetString(5),
                            DetailJson = reader.IsDBNull(6) ? null : reader.GetString(6),
                            IsSuccess = SqliteStorageConverters.ReadBoolean(reader, 7),
                            ErrorMessage = reader.GetString(8),
                            CreatedAt = SqliteStorageConverters.FromStorageTimestamp(reader.GetString(9))
                        });
                }

                return (IReadOnlyList<StoredRaceEngineerReport>)results;
            },
            cancellationToken);
    }

    private static void AddParameters(Microsoft.Data.Sqlite.SqliteCommand command, StoredRaceEngineerReport report)
    {
        command.Parameters.AddWithValue("@session_id", report.SessionId);
        command.Parameters.AddWithValue("@lap_number", (object?)report.LapNumber ?? DBNull.Value);
        command.Parameters.AddWithValue("@report_type", report.ReportType);
        command.Parameters.AddWithValue("@summary", report.Summary);
        command.Parameters.AddWithValue("@spoken_text", report.SpokenText);
        command.Parameters.AddWithValue("@detail_json", (object?)report.DetailJson ?? DBNull.Value);
        command.Parameters.AddWithValue("@is_success", report.IsSuccess ? 1 : 0);
        command.Parameters.AddWithValue("@error_message", report.ErrorMessage);
        command.Parameters.AddWithValue("@created_at", SqliteStorageConverters.ToStorageTimestamp(report.CreatedAt));
    }
}
