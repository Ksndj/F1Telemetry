using F1Telemetry.Core;
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
    /// <param name="appDataRootOverride">Optional application data root override used by tests.</param>
    public SqliteDatabaseService(string? appDataRootOverride = null)
    {
        DatabasePath = string.IsNullOrWhiteSpace(appDataRootOverride)
            ? AppPaths.GetDatabasePath()
            : Path.Combine(appDataRootOverride, "F1Telemetry", "f1telemetry.db");
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

            CREATE TABLE IF NOT EXISTS lap_samples (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                session_id TEXT NOT NULL,
                sample_index INTEGER NOT NULL,
                sampled_at TEXT NOT NULL,
                frame_identifier INTEGER NOT NULL,
                lap_number INTEGER NOT NULL,
                lap_distance REAL NULL,
                total_distance REAL NULL,
                current_lap_time_ms INTEGER NULL,
                last_lap_time_ms INTEGER NULL,
                speed_kph REAL NULL,
                throttle REAL NULL,
                brake REAL NULL,
                steering REAL NULL,
                gear INTEGER NULL,
                fuel_remaining_litres REAL NULL,
                fuel_laps_remaining REAL NULL,
                ers_store_energy REAL NULL,
                tyre_wear REAL NULL,
                tyre_wear_front_left REAL NULL,
                tyre_wear_front_right REAL NULL,
                tyre_wear_rear_left REAL NULL,
                tyre_wear_rear_right REAL NULL,
                position INTEGER NULL,
                delta_front_ms INTEGER NULL,
                delta_leader_ms INTEGER NULL,
                pit_status INTEGER NULL,
                is_valid INTEGER NOT NULL,
                visual_tyre_compound INTEGER NULL,
                actual_tyre_compound INTEGER NULL,
                created_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS corner_summaries (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                session_id TEXT NOT NULL,
                lap_number INTEGER NOT NULL,
                corner_number INTEGER NOT NULL,
                corner_name TEXT NOT NULL,
                start_distance_m REAL NULL,
                apex_distance_m REAL NULL,
                end_distance_m REAL NULL,
                entry_speed_kph REAL NULL,
                apex_speed_kph REAL NULL,
                exit_speed_kph REAL NULL,
                min_speed_kph REAL NULL,
                max_brake REAL NULL,
                average_throttle REAL NULL,
                average_steering REAL NULL,
                time_loss_ms REAL NULL,
                advice_text TEXT NOT NULL,
                payload_json TEXT NULL,
                created_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS strategy_advices (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                session_id TEXT NOT NULL,
                lap_number INTEGER NULL,
                advice_type TEXT NOT NULL,
                priority INTEGER NOT NULL,
                message TEXT NOT NULL,
                rationale TEXT NOT NULL,
                expected_gain_ms REAL NULL,
                risk_level TEXT NOT NULL,
                payload_json TEXT NULL,
                created_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS race_engineer_reports (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                session_id TEXT NOT NULL,
                lap_number INTEGER NULL,
                report_type TEXT NOT NULL,
                summary TEXT NOT NULL,
                spoken_text TEXT NOT NULL,
                detail_json TEXT NULL,
                is_success INTEGER NOT NULL,
                error_message TEXT NOT NULL,
                created_at TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_laps_session_created_at_desc
                ON laps (session_id, created_at DESC);

            CREATE INDEX IF NOT EXISTS idx_events_session_created_at_desc
                ON events (session_id, created_at DESC);

            CREATE INDEX IF NOT EXISTS idx_ai_reports_session_created_at_desc
                ON ai_reports (session_id, created_at DESC);

            CREATE INDEX IF NOT EXISTS idx_lap_samples_session_lap_order
                ON lap_samples (session_id, lap_number, sample_index, sampled_at, id);

            CREATE INDEX IF NOT EXISTS idx_lap_samples_session_created_at_desc
                ON lap_samples (session_id, created_at DESC);

            CREATE INDEX IF NOT EXISTS idx_corner_summaries_session_lap_corner
                ON corner_summaries (session_id, lap_number, corner_number);

            CREATE INDEX IF NOT EXISTS idx_corner_summaries_session_created_at_desc
                ON corner_summaries (session_id, created_at DESC);

            CREATE INDEX IF NOT EXISTS idx_strategy_advices_session_lap_created_at_desc
                ON strategy_advices (session_id, lap_number, created_at DESC);

            CREATE INDEX IF NOT EXISTS idx_strategy_advices_session_created_at_desc
                ON strategy_advices (session_id, created_at DESC);

            CREATE INDEX IF NOT EXISTS idx_race_engineer_reports_session_lap_created_at_desc
                ON race_engineer_reports (session_id, lap_number, created_at DESC);

            CREATE INDEX IF NOT EXISTS idx_race_engineer_reports_session_created_at_desc
                ON race_engineer_reports (session_id, created_at DESC);
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
