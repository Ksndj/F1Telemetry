using F1Telemetry.Storage.Interfaces;
using Microsoft.Data.Sqlite;

namespace F1Telemetry.Storage.Services;

/// <summary>
/// Hosts the shared SQLite connection and ensures the persistence schema exists.
/// </summary>
public sealed class SqliteDatabaseService : IDatabaseService
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _connectionString;
    private SqliteConnection? _connection;
    private bool _initialized;
    private bool _disposed;

    /// <summary>
    /// Initializes a new SQLite database service.
    /// </summary>
    /// <param name="localAppDataRootOverride">Optional LocalAppData root override used by tests.</param>
    public SqliteDatabaseService(string? localAppDataRootOverride = null)
    {
        var localAppDataRoot = string.IsNullOrWhiteSpace(localAppDataRootOverride)
            ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            : localAppDataRootOverride;

        DatabasePath = Path.Combine(localAppDataRoot, "F1Telemetry", "f1telemetry.db");
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    /// <inheritdoc />
    public string DatabasePath { get; }

    /// <inheritdoc />
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            var directoryPath = Path.GetDirectoryName(DatabasePath);
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            _connection = new SqliteConnection(_connectionString);
            await _connection.OpenAsync(cancellationToken);
            await CreateSchemaAsync(_connection, cancellationToken);
            _initialized = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(
        Func<SqliteConnection, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await ExecuteAsync<object?>(
            async (connection, innerCancellationToken) =>
            {
                await operation(connection, innerCancellationToken);
                return null;
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<T> ExecuteAsync<T>(
        Func<SqliteConnection, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ThrowIfDisposed();
        await InitializeAsync(cancellationToken);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await operation(_connection!, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        await _gate.WaitAsync();
        try
        {
            if (_connection is not null)
            {
                await _connection.DisposeAsync();
                _connection = null;
            }
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }

    private static async Task CreateSchemaAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS sessions (
                id TEXT PRIMARY KEY,
                session_uid TEXT NOT NULL,
                track_id INTEGER,
                session_type INTEGER,
                started_at TEXT NOT NULL,
                ended_at TEXT NULL
            );

            CREATE TABLE IF NOT EXISTS laps (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                session_id TEXT NOT NULL,
                lap_number INTEGER NOT NULL,
                lap_time_ms INTEGER NULL,
                sector1_ms INTEGER NULL,
                sector2_ms INTEGER NULL,
                sector3_ms INTEGER NULL,
                is_valid INTEGER NOT NULL,
                avg_speed_kph REAL NULL,
                fuel_used_litres REAL NULL,
                ers_used REAL NULL,
                start_tyre TEXT NOT NULL,
                end_tyre TEXT NOT NULL,
                created_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS events (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                session_id TEXT NOT NULL,
                event_type TEXT NOT NULL,
                severity TEXT NOT NULL,
                lap_number INTEGER NULL,
                vehicle_idx INTEGER NULL,
                driver_name TEXT NULL,
                message TEXT NOT NULL,
                payload_json TEXT NULL,
                created_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS ai_reports (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                session_id TEXT NOT NULL,
                lap_number INTEGER NOT NULL,
                summary TEXT NOT NULL,
                tyre_advice TEXT NOT NULL,
                fuel_advice TEXT NOT NULL,
                traffic_advice TEXT NOT NULL,
                tts_text TEXT NOT NULL,
                is_success INTEGER NOT NULL,
                error_message TEXT NOT NULL,
                created_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS settings (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
