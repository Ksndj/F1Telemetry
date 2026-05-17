using F1Telemetry.Storage.Interfaces;
using F1Telemetry.Storage.Internal;
using F1Telemetry.Storage.Models;

namespace F1Telemetry.Storage.Repositories;

/// <summary>
/// Stores strategy recommendations in SQLite.
/// </summary>
public sealed class StrategyAdviceRepository : IStrategyAdviceRepository
{
    private readonly IDatabaseService _databaseService;

    /// <summary>
    /// Initializes a new strategy advice repository.
    /// </summary>
    public StrategyAdviceRepository(IDatabaseService databaseService)
    {
        _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
    }

    /// <inheritdoc />
    public Task AddAsync(StoredStrategyAdvice advice, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(advice);

        return _databaseService.ExecuteAsync(
            async (connection, innerCancellationToken) =>
            {
                using var command = connection.CreateCommand();
                command.CommandText = """
                    INSERT INTO strategy_advices (
                        session_id,
                        lap_number,
                        advice_type,
                        priority,
                        message,
                        rationale,
                        expected_gain_ms,
                        risk_level,
                        payload_json,
                        created_at)
                    VALUES (
                        @session_id,
                        @lap_number,
                        @advice_type,
                        @priority,
                        @message,
                        @rationale,
                        @expected_gain_ms,
                        @risk_level,
                        @payload_json,
                        @created_at);
                    """;
                AddParameters(command, advice);
                await command.ExecuteNonQueryAsync(innerCancellationToken);
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<StoredStrategyAdvice>> GetRecentAsync(
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
    public Task<IReadOnlyList<StoredStrategyAdvice>> GetForLapAsync(
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

    private Task<IReadOnlyList<StoredStrategyAdvice>> QueryAsync(
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
                           advice_type,
                           priority,
                           message,
                           rationale,
                           expected_gain_ms,
                           risk_level,
                           payload_json,
                           created_at
                    FROM strategy_advices
                    {whereClause}
                    """;
                addParameters(command);

                using var reader = await command.ExecuteReaderAsync(innerCancellationToken);
                var results = new List<StoredStrategyAdvice>();
                while (await reader.ReadAsync(innerCancellationToken))
                {
                    results.Add(
                        new StoredStrategyAdvice
                        {
                            Id = reader.GetInt64(0),
                            SessionId = reader.GetString(1),
                            LapNumber = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                            AdviceType = reader.GetString(3),
                            Priority = reader.GetInt32(4),
                            Message = reader.GetString(5),
                            Rationale = reader.GetString(6),
                            ExpectedGainInMs = reader.IsDBNull(7) ? null : reader.GetDouble(7),
                            RiskLevel = reader.GetString(8),
                            PayloadJson = reader.IsDBNull(9) ? null : reader.GetString(9),
                            CreatedAt = SqliteStorageConverters.FromStorageTimestamp(reader.GetString(10))
                        });
                }

                return (IReadOnlyList<StoredStrategyAdvice>)results;
            },
            cancellationToken);
    }

    private static void AddParameters(Microsoft.Data.Sqlite.SqliteCommand command, StoredStrategyAdvice advice)
    {
        command.Parameters.AddWithValue("@session_id", advice.SessionId);
        command.Parameters.AddWithValue("@lap_number", (object?)advice.LapNumber ?? DBNull.Value);
        command.Parameters.AddWithValue("@advice_type", advice.AdviceType);
        command.Parameters.AddWithValue("@priority", advice.Priority);
        command.Parameters.AddWithValue("@message", advice.Message);
        command.Parameters.AddWithValue("@rationale", advice.Rationale);
        command.Parameters.AddWithValue("@expected_gain_ms", (object?)advice.ExpectedGainInMs ?? DBNull.Value);
        command.Parameters.AddWithValue("@risk_level", advice.RiskLevel);
        command.Parameters.AddWithValue("@payload_json", (object?)advice.PayloadJson ?? DBNull.Value);
        command.Parameters.AddWithValue("@created_at", SqliteStorageConverters.ToStorageTimestamp(advice.CreatedAt));
    }
}
