using F1Telemetry.Analytics.Laps;
using F1Telemetry.Storage.Interfaces;
using F1Telemetry.Storage.Internal;
using F1Telemetry.Storage.Models;

namespace F1Telemetry.Storage.Repositories;

/// <summary>
/// Stores completed lap summaries in SQLite.
/// </summary>
public sealed class LapRepository : ILapRepository
{
    private readonly IDatabaseService _databaseService;

    /// <summary>
    /// Initializes a new lap repository.
    /// </summary>
    public LapRepository(IDatabaseService databaseService)
    {
        _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
    }

    /// <inheritdoc />
    public Task AddAsync(string sessionId, LapSummary lapSummary, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lapSummary);

        return _databaseService.ExecuteAsync(
            async (connection, innerCancellationToken) =>
            {
                using var command = connection.CreateCommand();
                command.CommandText = """
                    INSERT INTO laps (
                        session_id,
                        lap_number,
                        lap_time_ms,
                        sector1_ms,
                        sector2_ms,
                        sector3_ms,
                        is_valid,
                        avg_speed_kph,
                        fuel_used_litres,
                        ers_used,
                        start_tyre,
                        end_tyre,
                        created_at)
                    VALUES (
                        @session_id,
                        @lap_number,
                        @lap_time_ms,
                        @sector1_ms,
                        @sector2_ms,
                        @sector3_ms,
                        @is_valid,
                        @avg_speed_kph,
                        @fuel_used_litres,
                        @ers_used,
                        @start_tyre,
                        @end_tyre,
                        @created_at);
                    """;
                command.Parameters.AddWithValue("@session_id", sessionId);
                command.Parameters.AddWithValue("@lap_number", lapSummary.LapNumber);
                command.Parameters.AddWithValue("@lap_time_ms", (object?)lapSummary.LapTimeInMs is null ? DBNull.Value : (int)lapSummary.LapTimeInMs.Value);
                command.Parameters.AddWithValue("@sector1_ms", (object?)lapSummary.Sector1TimeInMs is null ? DBNull.Value : (int)lapSummary.Sector1TimeInMs.Value);
                command.Parameters.AddWithValue("@sector2_ms", (object?)lapSummary.Sector2TimeInMs is null ? DBNull.Value : (int)lapSummary.Sector2TimeInMs.Value);
                command.Parameters.AddWithValue("@sector3_ms", (object?)lapSummary.Sector3TimeInMs is null ? DBNull.Value : (int)lapSummary.Sector3TimeInMs.Value);
                command.Parameters.AddWithValue("@is_valid", lapSummary.IsValid ? 1 : 0);
                command.Parameters.AddWithValue("@avg_speed_kph", (object?)lapSummary.AverageSpeedKph ?? DBNull.Value);
                command.Parameters.AddWithValue("@fuel_used_litres", (object?)lapSummary.FuelUsedLitres ?? DBNull.Value);
                command.Parameters.AddWithValue("@ers_used", (object?)lapSummary.ErsUsed ?? DBNull.Value);
                command.Parameters.AddWithValue("@start_tyre", lapSummary.StartTyre);
                command.Parameters.AddWithValue("@end_tyre", lapSummary.EndTyre);
                command.Parameters.AddWithValue("@created_at", SqliteStorageConverters.ToStorageTimestamp(lapSummary.ClosedAt));
                await command.ExecuteNonQueryAsync(innerCancellationToken);
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<StoredLap>> GetRecentAsync(string sessionId, int count, CancellationToken cancellationToken = default)
    {
        return _databaseService.ExecuteAsync(
            async (connection, innerCancellationToken) =>
            {
                using var command = connection.CreateCommand();
                command.CommandText = """
                    SELECT id,
                           session_id,
                           lap_number,
                           lap_time_ms,
                           sector1_ms,
                           sector2_ms,
                           sector3_ms,
                           is_valid,
                           avg_speed_kph,
                           fuel_used_litres,
                           ers_used,
                           start_tyre,
                           end_tyre,
                           created_at
                    FROM laps
                    WHERE session_id = @session_id
                    ORDER BY created_at DESC
                    LIMIT @count;
                    """;
                command.Parameters.AddWithValue("@session_id", sessionId);
                command.Parameters.AddWithValue("@count", count);

                using var reader = await command.ExecuteReaderAsync(innerCancellationToken);
                var results = new List<StoredLap>();
                while (await reader.ReadAsync(innerCancellationToken))
                {
                    results.Add(
                        new StoredLap
                        {
                            Id = reader.GetInt64(0),
                            SessionId = reader.GetString(1),
                            LapNumber = reader.GetInt32(2),
                            LapTimeInMs = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                            Sector1TimeInMs = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                            Sector2TimeInMs = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                            Sector3TimeInMs = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                            IsValid = SqliteStorageConverters.ReadBoolean(reader, 7),
                            AverageSpeedKph = reader.IsDBNull(8) ? null : reader.GetDouble(8),
                            FuelUsedLitres = reader.IsDBNull(9) ? null : reader.GetFloat(9),
                            ErsUsed = reader.IsDBNull(10) ? null : reader.GetFloat(10),
                            StartTyre = reader.GetString(11),
                            EndTyre = reader.GetString(12),
                            CreatedAt = SqliteStorageConverters.FromStorageTimestamp(reader.GetString(13))
                        });
                }

                return (IReadOnlyList<StoredLap>)results;
            },
            cancellationToken);
    }
}
