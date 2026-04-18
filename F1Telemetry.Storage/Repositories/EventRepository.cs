using F1Telemetry.Analytics.Events;
using F1Telemetry.Storage.Interfaces;
using F1Telemetry.Storage.Internal;
using F1Telemetry.Storage.Models;

namespace F1Telemetry.Storage.Repositories;

/// <summary>
/// Stores race events in SQLite.
/// </summary>
public sealed class EventRepository : IEventRepository
{
    private readonly IDatabaseService _databaseService;

    /// <summary>
    /// Initializes a new event repository.
    /// </summary>
    public EventRepository(IDatabaseService databaseService)
    {
        _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
    }

    /// <inheritdoc />
    public Task AddAsync(string sessionId, RaceEvent raceEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(raceEvent);

        return _databaseService.ExecuteAsync(
            async (connection, innerCancellationToken) =>
            {
                using var command = connection.CreateCommand();
                command.CommandText = """
                    INSERT INTO events (
                        session_id,
                        event_type,
                        severity,
                        lap_number,
                        vehicle_idx,
                        driver_name,
                        message,
                        payload_json,
                        created_at)
                    VALUES (
                        @session_id,
                        @event_type,
                        @severity,
                        @lap_number,
                        @vehicle_idx,
                        @driver_name,
                        @message,
                        @payload_json,
                        @created_at);
                    """;
                command.Parameters.AddWithValue("@session_id", sessionId);
                command.Parameters.AddWithValue("@event_type", raceEvent.EventType.ToString());
                command.Parameters.AddWithValue("@severity", raceEvent.Severity.ToString());
                command.Parameters.AddWithValue("@lap_number", (object?)raceEvent.LapNumber ?? DBNull.Value);
                command.Parameters.AddWithValue("@vehicle_idx", (object?)raceEvent.VehicleIdx ?? DBNull.Value);
                command.Parameters.AddWithValue("@driver_name", (object?)raceEvent.DriverName ?? DBNull.Value);
                command.Parameters.AddWithValue("@message", raceEvent.Message);
                command.Parameters.AddWithValue("@payload_json", (object?)raceEvent.PayloadJson ?? DBNull.Value);
                command.Parameters.AddWithValue("@created_at", SqliteStorageConverters.ToStorageTimestamp(raceEvent.Timestamp));
                await command.ExecuteNonQueryAsync(innerCancellationToken);
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<StoredEvent>> GetRecentAsync(string sessionId, int count, CancellationToken cancellationToken = default)
    {
        return _databaseService.ExecuteAsync(
            async (connection, innerCancellationToken) =>
            {
                using var command = connection.CreateCommand();
                command.CommandText = """
                    SELECT id,
                           session_id,
                           event_type,
                           severity,
                           lap_number,
                           vehicle_idx,
                           driver_name,
                           message,
                           payload_json,
                           created_at
                    FROM events
                    WHERE session_id = @session_id
                    ORDER BY created_at DESC
                    LIMIT @count;
                    """;
                command.Parameters.AddWithValue("@session_id", sessionId);
                command.Parameters.AddWithValue("@count", count);

                using var reader = await command.ExecuteReaderAsync(innerCancellationToken);
                var results = new List<StoredEvent>();
                while (await reader.ReadAsync(innerCancellationToken))
                {
                    results.Add(
                        new StoredEvent
                        {
                            Id = reader.GetInt64(0),
                            SessionId = reader.GetString(1),
                            EventType = Enum.Parse<EventType>(reader.GetString(2)),
                            Severity = Enum.Parse<EventSeverity>(reader.GetString(3)),
                            LapNumber = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                            VehicleIdx = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                            DriverName = reader.IsDBNull(6) ? null : reader.GetString(6),
                            Message = reader.GetString(7),
                            PayloadJson = reader.IsDBNull(8) ? null : reader.GetString(8),
                            CreatedAt = SqliteStorageConverters.FromStorageTimestamp(reader.GetString(9))
                        });
                }

                return (IReadOnlyList<StoredEvent>)results;
            },
            cancellationToken);
    }
}
