using F1Telemetry.Storage.Interfaces;
using F1Telemetry.Storage.Internal;
using F1Telemetry.Storage.Models;

namespace F1Telemetry.Storage.Repositories;

/// <summary>
/// Stores corner summaries in SQLite.
/// </summary>
public sealed class CornerSummaryRepository : ICornerSummaryRepository
{
    private readonly IDatabaseService _databaseService;

    /// <summary>
    /// Initializes a new corner summary repository.
    /// </summary>
    public CornerSummaryRepository(IDatabaseService databaseService)
    {
        _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
    }

    /// <inheritdoc />
    public Task AddAsync(StoredCornerSummary summary, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(summary);

        return _databaseService.ExecuteAsync(
            async (connection, innerCancellationToken) =>
            {
                using var command = connection.CreateCommand();
                command.CommandText = """
                    INSERT INTO corner_summaries (
                        session_id,
                        lap_number,
                        corner_number,
                        corner_name,
                        start_distance_m,
                        apex_distance_m,
                        end_distance_m,
                        entry_speed_kph,
                        apex_speed_kph,
                        exit_speed_kph,
                        min_speed_kph,
                        max_brake,
                        average_throttle,
                        average_steering,
                        time_loss_ms,
                        advice_text,
                        payload_json,
                        created_at)
                    VALUES (
                        @session_id,
                        @lap_number,
                        @corner_number,
                        @corner_name,
                        @start_distance_m,
                        @apex_distance_m,
                        @end_distance_m,
                        @entry_speed_kph,
                        @apex_speed_kph,
                        @exit_speed_kph,
                        @min_speed_kph,
                        @max_brake,
                        @average_throttle,
                        @average_steering,
                        @time_loss_ms,
                        @advice_text,
                        @payload_json,
                        @created_at);
                    """;
                AddParameters(command, summary);
                await command.ExecuteNonQueryAsync(innerCancellationToken);
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<StoredCornerSummary>> GetForLapAsync(
        string sessionId,
        int lapNumber,
        CancellationToken cancellationToken = default)
    {
        return _databaseService.ExecuteAsync(
            async (connection, innerCancellationToken) =>
            {
                using var command = connection.CreateCommand();
                command.CommandText = """
                    SELECT id,
                           session_id,
                           lap_number,
                           corner_number,
                           corner_name,
                           start_distance_m,
                           apex_distance_m,
                           end_distance_m,
                           entry_speed_kph,
                           apex_speed_kph,
                           exit_speed_kph,
                           min_speed_kph,
                           max_brake,
                           average_throttle,
                           average_steering,
                           time_loss_ms,
                           advice_text,
                           payload_json,
                           created_at
                    FROM corner_summaries
                    WHERE session_id = @session_id
                      AND lap_number = @lap_number
                    ORDER BY corner_number ASC, id ASC;
                    """;
                command.Parameters.AddWithValue("@session_id", sessionId);
                command.Parameters.AddWithValue("@lap_number", lapNumber);

                using var reader = await command.ExecuteReaderAsync(innerCancellationToken);
                var results = new List<StoredCornerSummary>();
                while (await reader.ReadAsync(innerCancellationToken))
                {
                    results.Add(
                        new StoredCornerSummary
                        {
                            Id = reader.GetInt64(0),
                            SessionId = reader.GetString(1),
                            LapNumber = reader.GetInt32(2),
                            CornerNumber = reader.GetInt32(3),
                            CornerName = reader.GetString(4),
                            StartDistanceMeters = reader.IsDBNull(5) ? null : reader.GetFloat(5),
                            ApexDistanceMeters = reader.IsDBNull(6) ? null : reader.GetFloat(6),
                            EndDistanceMeters = reader.IsDBNull(7) ? null : reader.GetFloat(7),
                            EntrySpeedKph = reader.IsDBNull(8) ? null : reader.GetDouble(8),
                            ApexSpeedKph = reader.IsDBNull(9) ? null : reader.GetDouble(9),
                            ExitSpeedKph = reader.IsDBNull(10) ? null : reader.GetDouble(10),
                            MinSpeedKph = reader.IsDBNull(11) ? null : reader.GetDouble(11),
                            MaxBrake = reader.IsDBNull(12) ? null : reader.GetDouble(12),
                            AverageThrottle = reader.IsDBNull(13) ? null : reader.GetDouble(13),
                            AverageSteering = reader.IsDBNull(14) ? null : reader.GetDouble(14),
                            TimeLossInMs = reader.IsDBNull(15) ? null : reader.GetDouble(15),
                            AdviceText = reader.GetString(16),
                            PayloadJson = reader.IsDBNull(17) ? null : reader.GetString(17),
                            CreatedAt = SqliteStorageConverters.FromStorageTimestamp(reader.GetString(18))
                        });
                }

                return (IReadOnlyList<StoredCornerSummary>)results;
            },
            cancellationToken);
    }

    private static void AddParameters(Microsoft.Data.Sqlite.SqliteCommand command, StoredCornerSummary summary)
    {
        command.Parameters.AddWithValue("@session_id", summary.SessionId);
        command.Parameters.AddWithValue("@lap_number", summary.LapNumber);
        command.Parameters.AddWithValue("@corner_number", summary.CornerNumber);
        command.Parameters.AddWithValue("@corner_name", summary.CornerName);
        command.Parameters.AddWithValue("@start_distance_m", (object?)summary.StartDistanceMeters ?? DBNull.Value);
        command.Parameters.AddWithValue("@apex_distance_m", (object?)summary.ApexDistanceMeters ?? DBNull.Value);
        command.Parameters.AddWithValue("@end_distance_m", (object?)summary.EndDistanceMeters ?? DBNull.Value);
        command.Parameters.AddWithValue("@entry_speed_kph", (object?)summary.EntrySpeedKph ?? DBNull.Value);
        command.Parameters.AddWithValue("@apex_speed_kph", (object?)summary.ApexSpeedKph ?? DBNull.Value);
        command.Parameters.AddWithValue("@exit_speed_kph", (object?)summary.ExitSpeedKph ?? DBNull.Value);
        command.Parameters.AddWithValue("@min_speed_kph", (object?)summary.MinSpeedKph ?? DBNull.Value);
        command.Parameters.AddWithValue("@max_brake", (object?)summary.MaxBrake ?? DBNull.Value);
        command.Parameters.AddWithValue("@average_throttle", (object?)summary.AverageThrottle ?? DBNull.Value);
        command.Parameters.AddWithValue("@average_steering", (object?)summary.AverageSteering ?? DBNull.Value);
        command.Parameters.AddWithValue("@time_loss_ms", (object?)summary.TimeLossInMs ?? DBNull.Value);
        command.Parameters.AddWithValue("@advice_text", summary.AdviceText);
        command.Parameters.AddWithValue("@payload_json", (object?)summary.PayloadJson ?? DBNull.Value);
        command.Parameters.AddWithValue("@created_at", SqliteStorageConverters.ToStorageTimestamp(summary.CreatedAt));
    }
}
