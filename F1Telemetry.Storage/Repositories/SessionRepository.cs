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
                    INSERT INTO sessions (id, session_uid, track_id, session_type, started_at, ended_at)
                    VALUES (@id, @session_uid, @track_id, @session_type, @started_at, @ended_at);
                    """;
                command.Parameters.AddWithValue("@id", session.Id);
                command.Parameters.AddWithValue("@session_uid", session.SessionUid);
                command.Parameters.AddWithValue("@track_id", (object?)session.TrackId ?? DBNull.Value);
                command.Parameters.AddWithValue("@session_type", (object?)session.SessionType ?? DBNull.Value);
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
                    SELECT id, session_uid, track_id, session_type, started_at, ended_at
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
                            StartedAt = SqliteStorageConverters.FromStorageTimestamp(reader.GetString(4)),
                            EndedAt = reader.IsDBNull(5) ? null : SqliteStorageConverters.FromStorageTimestamp(reader.GetString(5))
                        });
                }

                return (IReadOnlyList<StoredSession>)results;
            },
            cancellationToken);
    }
}
