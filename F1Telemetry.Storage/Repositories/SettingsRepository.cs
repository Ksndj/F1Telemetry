using F1Telemetry.Storage.Interfaces;

namespace F1Telemetry.Storage.Repositories;

/// <summary>
/// Stores generic key-value settings in SQLite.
/// </summary>
public sealed class SettingsRepository : ISettingsRepository
{
    private readonly IDatabaseService _databaseService;

    /// <summary>
    /// Initializes a new settings repository.
    /// </summary>
    public SettingsRepository(IDatabaseService databaseService)
    {
        _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
    }

    /// <inheritdoc />
    public Task UpsertAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        return _databaseService.ExecuteAsync(
            async (connection, innerCancellationToken) =>
            {
                using var command = connection.CreateCommand();
                command.CommandText = """
                    INSERT INTO settings (key, value)
                    VALUES (@key, @value)
                    ON CONFLICT(key) DO UPDATE SET value = excluded.value;
                    """;
                command.Parameters.AddWithValue("@key", key);
                command.Parameters.AddWithValue("@value", value);
                await command.ExecuteNonQueryAsync(innerCancellationToken);
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        return _databaseService.ExecuteAsync(
            async (connection, innerCancellationToken) =>
            {
                using var command = connection.CreateCommand();
                command.CommandText = """
                    SELECT value
                    FROM settings
                    WHERE key = @key
                    LIMIT 1;
                    """;
                command.Parameters.AddWithValue("@key", key);
                var result = await command.ExecuteScalarAsync(innerCancellationToken);
                return result as string;
            },
            cancellationToken);
    }
}
