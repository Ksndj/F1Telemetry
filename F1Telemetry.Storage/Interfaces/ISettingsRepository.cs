namespace F1Telemetry.Storage.Interfaces;

/// <summary>
/// Persists arbitrary application settings as key-value pairs.
/// </summary>
public interface ISettingsRepository
{
    /// <summary>
    /// Inserts or updates a stored setting value.
    /// </summary>
    Task UpsertAsync(string key, string value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a stored setting value by key.
    /// </summary>
    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);
}
