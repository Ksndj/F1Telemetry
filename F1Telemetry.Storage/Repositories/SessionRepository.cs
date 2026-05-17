using System.Text.Json;
using F1Telemetry.Storage.Interfaces;
using F1Telemetry.Storage.Internal;
using F1Telemetry.Storage.Models;

namespace F1Telemetry.Storage.Repositories;

/// <summary>
/// Stores session lifecycle rows in SQLite.
/// </summary>
public sealed class SessionRepository : ISessionRepository
{
    private readonly IDatabaseService _databaseService;

    /// <summary>
    /// Initializes a new session repository.
    /// </summary>
    public SessionRepository(IDatabaseService databaseService)
    {
        _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
    }

    /// <inheritdoc />
    public Task CreateAsync(StoredSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        return _databaseService.ExecuteAsync(
            async (connection, innerCancellationToken) =>
            {
                using var command = connection.CreateCommand();
                command.CommandText = """
                    INSERT INTO sessions (
                        id,
                        session_uid,
                        track_id,
                        session_type,
                        total_laps,
                        num_sessions_in_weekend,
                        weekend_structure,
                        started_at,
                        ended_at)
                    VALUES (
                        @id,
                        @session_uid,
                        @track_id,
                        @session_type,
                        @total_laps,
                        @num_sessions_in_weekend,
                        @weekend_structure,
                        @started_at,
                        @ended_at);
                    """;
                command.Parameters.AddWithValue("@id", session.Id);
                command.Parameters.AddWithValue("@session_uid", session.SessionUid);
                command.Parameters.AddWithValue("@track_id", (object?)session.TrackId ?? DBNull.Value);
                command.Parameters.AddWithValue("@session_type", (object?)session.SessionType ?? DBNull.Value);
                command.Parameters.AddWithValue("@total_laps", (object?)session.TotalLaps ?? DBNull.Value);
                command.Parameters.AddWithValue("@num_sessions_in_weekend", (object?)session.NumSessionsInWeekend ?? DBNull.Value);
                command.Parameters.AddWithValue("@weekend_structure", SerializeWeekendStructure(session.WeekendStructure));
                command.Parameters.AddWithValue("@started_at", SqliteStorageConverters.ToStorageTimestamp(session.StartedAt));
                command.Parameters.AddWithValue("@ended_at", session.EndedAt is null ? DBNull.Value : SqliteStorageConverters.ToStorageTimestamp(session.EndedAt.Value));
                await command.ExecuteNonQueryAsync(innerCancellationToken);
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task EndAsync(string sessionId, DateTimeOffset endedAt, CancellationToken cancellationToken = default)
    {
        return _databaseService.ExecuteAsync(
            async (connection, innerCancellationToken) =>
            {
                using var command = connection.CreateCommand();
                command.CommandText = """
                    UPDATE sessions
                    SET ended_at = @ended_at
                    WHERE id = @id;
                    """;
                command.Parameters.AddWithValue("@id", sessionId);
                command.Parameters.AddWithValue("@ended_at", SqliteStorageConverters.ToStorageTimestamp(endedAt));
                await command.ExecuteNonQueryAsync(innerCancellationToken);
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<StoredSession>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
    {
        return _databaseService.ExecuteAsync(
            async (connection, innerCancellationToken) =>
            {
                using var command = connection.CreateCommand();
                command.CommandText = """
                    SELECT id,
                           session_uid,
                           track_id,
                           session_type,
                           total_laps,
                           num_sessions_in_weekend,
                           weekend_structure,
                           started_at,
                           ended_at
                    FROM sessions
                    ORDER BY started_at DESC
                    LIMIT @count;
                    """;
                command.Parameters.AddWithValue("@count", count);

                using var reader = await command.ExecuteReaderAsync(innerCancellationToken);
                var results = new List<StoredSession>();
                while (await reader.ReadAsync(innerCancellationToken))
                {
                    results.Add(
                        new StoredSession
                        {
                            Id = reader.GetString(0),
                            SessionUid = reader.GetString(1),
                            TrackId = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                            SessionType = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                            TotalLaps = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                            NumSessionsInWeekend = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                            WeekendStructure = reader.IsDBNull(6) ? Array.Empty<byte>() : DeserializeWeekendStructure(reader.GetString(6)),
                            StartedAt = SqliteStorageConverters.FromStorageTimestamp(reader.GetString(7)),
                            EndedAt = reader.IsDBNull(8) ? null : SqliteStorageConverters.FromStorageTimestamp(reader.GetString(8))
                        });
                }

                return (IReadOnlyList<StoredSession>)results;
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return Task.FromResult(false);
        }

        return _databaseService.ExecuteAsync(
            async (connection, innerCancellationToken) =>
            {
                await using var transaction = (Microsoft.Data.Sqlite.SqliteTransaction)await connection.BeginTransactionAsync(innerCancellationToken);
                try
                {
                    var sessionExists = await SessionExistsAsync(connection, transaction, sessionId, innerCancellationToken);
                    if (!sessionExists)
                    {
                        await transaction.CommitAsync(innerCancellationToken);
                        return false;
                    }

                    await DeleteAssociatedRowsAsync(connection, transaction, "race_engineer_reports", sessionId, innerCancellationToken);
                    await DeleteAssociatedRowsAsync(connection, transaction, "strategy_advices", sessionId, innerCancellationToken);
                    await DeleteAssociatedRowsAsync(connection, transaction, "corner_summaries", sessionId, innerCancellationToken);
                    await DeleteAssociatedRowsAsync(connection, transaction, "lap_samples", sessionId, innerCancellationToken);
                    await DeleteAssociatedRowsAsync(connection, transaction, "ai_reports", sessionId, innerCancellationToken);
                    await DeleteAssociatedRowsAsync(connection, transaction, "events", sessionId, innerCancellationToken);
                    await DeleteAssociatedRowsAsync(connection, transaction, "laps", sessionId, innerCancellationToken);

                    using var deleteSessionCommand = connection.CreateCommand();
                    deleteSessionCommand.Transaction = transaction;
                    deleteSessionCommand.CommandText = """
                        DELETE FROM sessions
                        WHERE id = @id;
                        """;
                    deleteSessionCommand.Parameters.AddWithValue("@id", sessionId);
                    var deletedRows = await deleteSessionCommand.ExecuteNonQueryAsync(innerCancellationToken);
                    await transaction.CommitAsync(innerCancellationToken);
                    return deletedRows > 0;
                }
                catch
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    throw;
                }
            },
            cancellationToken);
    }

    private static async Task<bool> SessionExistsAsync(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        Microsoft.Data.Sqlite.SqliteTransaction transaction,
        string sessionId,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT 1
            FROM sessions
            WHERE id = @id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@id", sessionId);
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private static async Task DeleteAssociatedRowsAsync(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        Microsoft.Data.Sqlite.SqliteTransaction transaction,
        string tableName,
        string sessionId,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"DELETE FROM {tableName} WHERE session_id = @session_id;";
        command.Parameters.AddWithValue("@session_id", sessionId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string SerializeWeekendStructure(IReadOnlyList<byte> weekendStructure)
    {
        return weekendStructure.Count == 0
            ? "[]"
            : JsonSerializer.Serialize(weekendStructure.ToArray());
    }

    private static IReadOnlyList<byte> DeserializeWeekendStructure(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<byte>();
        }

        try
        {
            return JsonSerializer.Deserialize<byte[]>(json) ?? Array.Empty<byte>();
        }
        catch (JsonException)
        {
            return Array.Empty<byte>();
        }
    }
}
