using F1Telemetry.AI.Models;
using F1Telemetry.Storage.Interfaces;
using F1Telemetry.Storage.Internal;
using F1Telemetry.Storage.Models;

namespace F1Telemetry.Storage.Repositories;

/// <summary>
/// Stores AI analysis results in SQLite.
/// </summary>
public sealed class AIReportRepository : IAIReportRepository
{
    private readonly IDatabaseService _databaseService;

    /// <summary>
    /// Initializes a new AI report repository.
    /// </summary>
    public AIReportRepository(IDatabaseService databaseService)
    {
        _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
    }

    /// <inheritdoc />
    public Task AddAsync(
        string sessionId,
        int lapNumber,
        AIAnalysisResult analysisResult,
        DateTimeOffset? createdAt = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(analysisResult);

        return _databaseService.ExecuteAsync(
            async (connection, innerCancellationToken) =>
            {
                using var command = connection.CreateCommand();
                command.CommandText = """
                    INSERT INTO ai_reports (
                        session_id,
                        lap_number,
                        summary,
                        tyre_advice,
                        fuel_advice,
                        traffic_advice,
                        tts_text,
                        is_success,
                        error_message,
                        created_at)
                    VALUES (
                        @session_id,
                        @lap_number,
                        @summary,
                        @tyre_advice,
                        @fuel_advice,
                        @traffic_advice,
                        @tts_text,
                        @is_success,
                        @error_message,
                        @created_at);
                    """;
                command.Parameters.AddWithValue("@session_id", sessionId);
                command.Parameters.AddWithValue("@lap_number", lapNumber);
                command.Parameters.AddWithValue("@summary", analysisResult.Summary);
                command.Parameters.AddWithValue("@tyre_advice", analysisResult.TyreAdvice);
                command.Parameters.AddWithValue("@fuel_advice", analysisResult.FuelAdvice);
                command.Parameters.AddWithValue("@traffic_advice", analysisResult.TrafficAdvice);
                command.Parameters.AddWithValue("@tts_text", analysisResult.TtsText);
                command.Parameters.AddWithValue("@is_success", analysisResult.IsSuccess ? 1 : 0);
                command.Parameters.AddWithValue("@error_message", analysisResult.ErrorMessage);
                command.Parameters.AddWithValue("@created_at", SqliteStorageConverters.ToStorageTimestamp(createdAt ?? DateTimeOffset.UtcNow));
                await command.ExecuteNonQueryAsync(innerCancellationToken);
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<StoredAiReport>> GetRecentAsync(string sessionId, int count, CancellationToken cancellationToken = default)
    {
        return _databaseService.ExecuteAsync(
            async (connection, innerCancellationToken) =>
            {
                using var command = connection.CreateCommand();
                command.CommandText = """
                    SELECT id,
                           session_id,
                           lap_number,
                           summary,
                           tyre_advice,
                           fuel_advice,
                           traffic_advice,
                           tts_text,
                           is_success,
                           error_message,
                           created_at
                    FROM ai_reports
                    WHERE session_id = @session_id
                    ORDER BY created_at DESC
                    LIMIT @count;
                    """;
                command.Parameters.AddWithValue("@session_id", sessionId);
                command.Parameters.AddWithValue("@count", count);

                using var reader = await command.ExecuteReaderAsync(innerCancellationToken);
                var results = new List<StoredAiReport>();
                while (await reader.ReadAsync(innerCancellationToken))
                {
                    results.Add(
                        new StoredAiReport
                        {
                            Id = reader.GetInt64(0),
                            SessionId = reader.GetString(1),
                            LapNumber = reader.GetInt32(2),
                            Summary = reader.GetString(3),
                            TyreAdvice = reader.GetString(4),
                            FuelAdvice = reader.GetString(5),
                            TrafficAdvice = reader.GetString(6),
                            TtsText = reader.GetString(7),
                            IsSuccess = SqliteStorageConverters.ReadBoolean(reader, 8),
                            ErrorMessage = reader.GetString(9),
                            CreatedAt = SqliteStorageConverters.FromStorageTimestamp(reader.GetString(10))
                        });
                }

                return (IReadOnlyList<StoredAiReport>)results;
            },
            cancellationToken);
    }
}
