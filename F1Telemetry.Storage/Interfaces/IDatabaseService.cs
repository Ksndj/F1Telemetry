using Microsoft.Data.Sqlite;

namespace F1Telemetry.Storage.Interfaces;

/// <summary>
/// Provides serialized asynchronous access to the SQLite connection used by storage repositories.
/// </summary>
public interface IDatabaseService : IAsyncDisposable
{
    /// <summary>
    /// Gets the absolute database file path.
    /// </summary>
    string DatabasePath { get; }

    /// <summary>
    /// Creates the database file and schema when they do not already exist.
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a database operation against the shared SQLite connection.
    /// </summary>
    Task ExecuteAsync(
        Func<SqliteConnection, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a database operation against the shared SQLite connection and returns a value.
    /// </summary>
    Task<T> ExecuteAsync<T>(
        Func<SqliteConnection, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);
}
