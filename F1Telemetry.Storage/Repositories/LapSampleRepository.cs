using F1Telemetry.Storage.Interfaces;
using F1Telemetry.Storage.Internal;
using F1Telemetry.Storage.Models;

namespace F1Telemetry.Storage.Repositories;

/// <summary>
/// Stores high-frequency lap samples in SQLite.
/// </summary>
public sealed class LapSampleRepository : ILapSampleRepository
{
    private readonly IDatabaseService _databaseService;

    /// <summary>
    /// Initializes a new lap sample repository.
    /// </summary>
    public LapSampleRepository(IDatabaseService databaseService)
    {
        _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
    }

    /// <inheritdoc />
    public Task AddAsync(StoredLapSample sample, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sample);
        return AddRangeAsync([sample], cancellationToken);
    }

    /// <inheritdoc />
    public Task AddRangeAsync(IEnumerable<StoredLapSample> samples, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(samples);

        var sampleRows = samples.ToArray();
        if (sampleRows.Length == 0)
        {
            return Task.CompletedTask;
        }

        return _databaseService.ExecuteAsync(
            async (connection, innerCancellationToken) =>
            {
                await using var transaction = (Microsoft.Data.Sqlite.SqliteTransaction)await connection.BeginTransactionAsync(innerCancellationToken);
                try
                {
                    foreach (var sample in sampleRows)
                    {
                        using var command = connection.CreateCommand();
                        command.Transaction = transaction;
                        command.CommandText = """
                            INSERT INTO lap_samples (
                                session_id,
                                sample_index,
                                sampled_at,
                                frame_identifier,
                                lap_number,
                                lap_distance,
                                total_distance,
                                current_lap_time_ms,
                                last_lap_time_ms,
                                speed_kph,
                                throttle,
                                brake,
                                steering,
                                gear,
                                fuel_remaining_litres,
                                fuel_laps_remaining,
                                ers_store_energy,
                                tyre_wear,
                                tyre_wear_front_left,
                                tyre_wear_front_right,
                                tyre_wear_rear_left,
                                tyre_wear_rear_right,
                                position,
                                delta_front_ms,
                                delta_leader_ms,
                                pit_status,
                                is_valid,
                                visual_tyre_compound,
                                actual_tyre_compound,
                                created_at)
                            VALUES (
                                @session_id,
                                @sample_index,
                                @sampled_at,
                                @frame_identifier,
                                @lap_number,
                                @lap_distance,
                                @total_distance,
                                @current_lap_time_ms,
                                @last_lap_time_ms,
                                @speed_kph,
                                @throttle,
                                @brake,
                                @steering,
                                @gear,
                                @fuel_remaining_litres,
                                @fuel_laps_remaining,
                                @ers_store_energy,
                                @tyre_wear,
                                @tyre_wear_front_left,
                                @tyre_wear_front_right,
                                @tyre_wear_rear_left,
                                @tyre_wear_rear_right,
                                @position,
                                @delta_front_ms,
                                @delta_leader_ms,
                                @pit_status,
                                @is_valid,
                                @visual_tyre_compound,
                                @actual_tyre_compound,
                                @created_at);
                            """;
                        AddSampleParameters(command, sample);
                        await command.ExecuteNonQueryAsync(innerCancellationToken);
                    }

                    await transaction.CommitAsync(innerCancellationToken);
                }
                catch
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    throw;
                }
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<StoredLapSample>> GetForLapAsync(
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
                           sample_index,
                           sampled_at,
                           frame_identifier,
                           lap_number,
                           lap_distance,
                           total_distance,
                           current_lap_time_ms,
                           last_lap_time_ms,
                           speed_kph,
                           throttle,
                           brake,
                           steering,
                           gear,
                           fuel_remaining_litres,
                           fuel_laps_remaining,
                           ers_store_energy,
                           tyre_wear,
                           tyre_wear_front_left,
                           tyre_wear_front_right,
                           tyre_wear_rear_left,
                           tyre_wear_rear_right,
                           position,
                           delta_front_ms,
                           delta_leader_ms,
                           pit_status,
                           is_valid,
                           visual_tyre_compound,
                           actual_tyre_compound,
                           created_at
                    FROM lap_samples
                    WHERE session_id = @session_id
                      AND lap_number = @lap_number
                    ORDER BY sample_index ASC, sampled_at ASC, id ASC;
                    """;
                command.Parameters.AddWithValue("@session_id", sessionId);
                command.Parameters.AddWithValue("@lap_number", lapNumber);

                using var reader = await command.ExecuteReaderAsync(innerCancellationToken);
                var results = new List<StoredLapSample>();
                while (await reader.ReadAsync(innerCancellationToken))
                {
                    results.Add(
                        new StoredLapSample
                        {
                            Id = reader.GetInt64(0),
                            SessionId = reader.GetString(1),
                            SampleIndex = reader.GetInt32(2),
                            SampledAt = SqliteStorageConverters.FromStorageTimestamp(reader.GetString(3)),
                            FrameIdentifier = reader.GetInt64(4),
                            LapNumber = reader.GetInt32(5),
                            LapDistance = reader.IsDBNull(6) ? null : reader.GetFloat(6),
                            TotalDistance = reader.IsDBNull(7) ? null : reader.GetFloat(7),
                            CurrentLapTimeInMs = reader.IsDBNull(8) ? null : reader.GetInt32(8),
                            LastLapTimeInMs = reader.IsDBNull(9) ? null : reader.GetInt32(9),
                            SpeedKph = reader.IsDBNull(10) ? null : reader.GetDouble(10),
                            Throttle = reader.IsDBNull(11) ? null : reader.GetDouble(11),
                            Brake = reader.IsDBNull(12) ? null : reader.GetDouble(12),
                            Steering = reader.IsDBNull(13) ? null : reader.GetFloat(13),
                            Gear = reader.IsDBNull(14) ? null : reader.GetInt32(14),
                            FuelRemainingLitres = reader.IsDBNull(15) ? null : reader.GetFloat(15),
                            FuelLapsRemaining = reader.IsDBNull(16) ? null : reader.GetFloat(16),
                            ErsStoreEnergy = reader.IsDBNull(17) ? null : reader.GetFloat(17),
                            TyreWear = reader.IsDBNull(18) ? null : reader.GetFloat(18),
                            TyreWearFrontLeft = reader.IsDBNull(19) ? null : reader.GetFloat(19),
                            TyreWearFrontRight = reader.IsDBNull(20) ? null : reader.GetFloat(20),
                            TyreWearRearLeft = reader.IsDBNull(21) ? null : reader.GetFloat(21),
                            TyreWearRearRight = reader.IsDBNull(22) ? null : reader.GetFloat(22),
                            Position = reader.IsDBNull(23) ? null : reader.GetInt32(23),
                            DeltaFrontInMs = reader.IsDBNull(24) ? null : reader.GetInt32(24),
                            DeltaLeaderInMs = reader.IsDBNull(25) ? null : reader.GetInt32(25),
                            PitStatus = reader.IsDBNull(26) ? null : reader.GetInt32(26),
                            IsValid = SqliteStorageConverters.ReadBoolean(reader, 27),
                            VisualTyreCompound = reader.IsDBNull(28) ? null : reader.GetInt32(28),
                            ActualTyreCompound = reader.IsDBNull(29) ? null : reader.GetInt32(29),
                            CreatedAt = SqliteStorageConverters.FromStorageTimestamp(reader.GetString(30))
                        });
                }

                return (IReadOnlyList<StoredLapSample>)results;
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<StoredLapTyreWearTrendPoint>> GetTyreWearTrendAsync(
        string sessionId,
        int count,
        CancellationToken cancellationToken = default)
    {
        return _databaseService.ExecuteAsync(
            async (connection, innerCancellationToken) =>
            {
                using var command = connection.CreateCommand();
                command.CommandText = """
                    SELECT *
                    FROM (
                        SELECT lap_number,
                               sample_index,
                               sampled_at,
                               tyre_wear_front_left,
                               tyre_wear_front_right,
                               tyre_wear_rear_left,
                               tyre_wear_rear_right
                        FROM (
                            SELECT lap_number,
                                   sample_index,
                                   sampled_at,
                                   tyre_wear_front_left,
                                   tyre_wear_front_right,
                                   tyre_wear_rear_left,
                                   tyre_wear_rear_right,
                                   ROW_NUMBER() OVER (
                                       PARTITION BY lap_number
                                       ORDER BY sample_index DESC, sampled_at DESC, id DESC) AS row_number
                            FROM lap_samples
                            WHERE session_id = @session_id
                              AND tyre_wear_front_left IS NOT NULL
                              AND tyre_wear_front_right IS NOT NULL
                              AND tyre_wear_rear_left IS NOT NULL
                              AND tyre_wear_rear_right IS NOT NULL
                        )
                        WHERE row_number = 1
                        ORDER BY lap_number DESC
                        LIMIT @count
                    )
                    ORDER BY lap_number ASC;
                    """;
                command.Parameters.AddWithValue("@session_id", sessionId);
                command.Parameters.AddWithValue("@count", Math.Max(0, count));

                using var reader = await command.ExecuteReaderAsync(innerCancellationToken);
                var results = new List<StoredLapTyreWearTrendPoint>();
                while (await reader.ReadAsync(innerCancellationToken))
                {
                    results.Add(
                        new StoredLapTyreWearTrendPoint
                        {
                            LapNumber = reader.GetInt32(0),
                            SampleIndex = reader.GetInt32(1),
                            SampledAt = SqliteStorageConverters.FromStorageTimestamp(reader.GetString(2)),
                            FrontLeft = reader.GetFloat(3),
                            FrontRight = reader.GetFloat(4),
                            RearLeft = reader.GetFloat(5),
                            RearRight = reader.GetFloat(6)
                        });
                }

                return (IReadOnlyList<StoredLapTyreWearTrendPoint>)results;
            },
            cancellationToken);
    }

    private static void AddSampleParameters(Microsoft.Data.Sqlite.SqliteCommand command, StoredLapSample sample)
    {
        command.Parameters.AddWithValue("@session_id", sample.SessionId);
        command.Parameters.AddWithValue("@sample_index", sample.SampleIndex);
        command.Parameters.AddWithValue("@sampled_at", SqliteStorageConverters.ToStorageTimestamp(sample.SampledAt));
        command.Parameters.AddWithValue("@frame_identifier", sample.FrameIdentifier);
        command.Parameters.AddWithValue("@lap_number", sample.LapNumber);
        command.Parameters.AddWithValue("@lap_distance", (object?)sample.LapDistance ?? DBNull.Value);
        command.Parameters.AddWithValue("@total_distance", (object?)sample.TotalDistance ?? DBNull.Value);
        command.Parameters.AddWithValue("@current_lap_time_ms", (object?)sample.CurrentLapTimeInMs ?? DBNull.Value);
        command.Parameters.AddWithValue("@last_lap_time_ms", (object?)sample.LastLapTimeInMs ?? DBNull.Value);
        command.Parameters.AddWithValue("@speed_kph", (object?)sample.SpeedKph ?? DBNull.Value);
        command.Parameters.AddWithValue("@throttle", (object?)sample.Throttle ?? DBNull.Value);
        command.Parameters.AddWithValue("@brake", (object?)sample.Brake ?? DBNull.Value);
        command.Parameters.AddWithValue("@steering", (object?)sample.Steering ?? DBNull.Value);
        command.Parameters.AddWithValue("@gear", (object?)sample.Gear ?? DBNull.Value);
        command.Parameters.AddWithValue("@fuel_remaining_litres", (object?)sample.FuelRemainingLitres ?? DBNull.Value);
        command.Parameters.AddWithValue("@fuel_laps_remaining", (object?)sample.FuelLapsRemaining ?? DBNull.Value);
        command.Parameters.AddWithValue("@ers_store_energy", (object?)sample.ErsStoreEnergy ?? DBNull.Value);
        command.Parameters.AddWithValue("@tyre_wear", (object?)sample.TyreWear ?? DBNull.Value);
        command.Parameters.AddWithValue("@tyre_wear_front_left", (object?)sample.TyreWearFrontLeft ?? DBNull.Value);
        command.Parameters.AddWithValue("@tyre_wear_front_right", (object?)sample.TyreWearFrontRight ?? DBNull.Value);
        command.Parameters.AddWithValue("@tyre_wear_rear_left", (object?)sample.TyreWearRearLeft ?? DBNull.Value);
        command.Parameters.AddWithValue("@tyre_wear_rear_right", (object?)sample.TyreWearRearRight ?? DBNull.Value);
        command.Parameters.AddWithValue("@position", (object?)sample.Position ?? DBNull.Value);
        command.Parameters.AddWithValue("@delta_front_ms", (object?)sample.DeltaFrontInMs ?? DBNull.Value);
        command.Parameters.AddWithValue("@delta_leader_ms", (object?)sample.DeltaLeaderInMs ?? DBNull.Value);
        command.Parameters.AddWithValue("@pit_status", (object?)sample.PitStatus ?? DBNull.Value);
        command.Parameters.AddWithValue("@is_valid", sample.IsValid ? 1 : 0);
        command.Parameters.AddWithValue("@visual_tyre_compound", (object?)sample.VisualTyreCompound ?? DBNull.Value);
        command.Parameters.AddWithValue("@actual_tyre_compound", (object?)sample.ActualTyreCompound ?? DBNull.Value);
        command.Parameters.AddWithValue("@created_at", SqliteStorageConverters.ToStorageTimestamp(sample.CreatedAt));
    }
}
