namespace F1Telemetry.Core;

/// <summary>
/// Provides centralized application data paths for persisted user data.
/// </summary>
public static class AppPaths
{
    private const string AppDirectoryName = "F1Telemetry";
    private const string SettingsFileName = "settings.json";
    private const string DatabaseFileName = "f1telemetry.db";
    private const string LogsDirectoryName = "logs";
    private const string StartupLogFileName = "startup.log";

    /// <summary>
    /// Gets the roaming application data directory used by F1Telemetry.
    /// </summary>
    public static string GetAppDataDir()
    {
        return GetAppDataDir(GetSpecialFolderPath(Environment.SpecialFolder.ApplicationData));
    }

    /// <summary>
    /// Gets the SQLite database file path used by F1Telemetry.
    /// </summary>
    public static string GetDatabasePath()
    {
        return GetDatabasePath(GetSpecialFolderPath(Environment.SpecialFolder.ApplicationData));
    }

    /// <summary>
    /// Gets the JSON settings file path used by F1Telemetry.
    /// </summary>
    public static string GetSettingsPath()
    {
        return GetSettingsPath(GetSpecialFolderPath(Environment.SpecialFolder.ApplicationData));
    }

    /// <summary>
    /// Gets the logs directory path used by F1Telemetry.
    /// </summary>
    public static string GetLogsDir()
    {
        return GetLogsDir(GetSpecialFolderPath(Environment.SpecialFolder.ApplicationData));
    }

    /// <summary>
    /// Ensures user data directories exist and migrates legacy LocalAppData files without overwriting.
    /// </summary>
    public static void InitializeUserData()
    {
        InitializeUserData(
            GetSpecialFolderPath(Environment.SpecialFolder.ApplicationData),
            GetSpecialFolderPath(Environment.SpecialFolder.LocalApplicationData));
    }

    internal static string GetAppDataDir(string appDataRoot)
    {
        return Path.Combine(appDataRoot, AppDirectoryName);
    }

    internal static string GetDatabasePath(string appDataRoot)
    {
        return Path.Combine(GetAppDataDir(appDataRoot), DatabaseFileName);
    }

    internal static string GetSettingsPath(string appDataRoot)
    {
        return Path.Combine(GetAppDataDir(appDataRoot), SettingsFileName);
    }

    internal static string GetLogsDir(string appDataRoot)
    {
        return Path.Combine(GetAppDataDir(appDataRoot), LogsDirectoryName);
    }

    internal static void InitializeUserData(string appDataRoot, string localAppDataRoot)
    {
        var appDataDir = GetAppDataDir(appDataRoot);
        var logsDir = GetLogsDir(appDataRoot);

        Directory.CreateDirectory(appDataDir);
        Directory.CreateDirectory(logsDir);

        MigrateLegacyData(appDataDir, logsDir, GetAppDataDir(localAppDataRoot));
        EnsureSettingsFile(GetSettingsPath(appDataRoot), logsDir);
    }

    private static string GetSpecialFolderPath(Environment.SpecialFolder specialFolder)
    {
        return Environment.GetFolderPath(specialFolder);
    }

    private static void MigrateLegacyData(string appDataDir, string logsDir, string legacyDataDir)
    {
        if (!Directory.Exists(legacyDataDir))
        {
            return;
        }

        CopyFileIfMissing(
            Path.Combine(legacyDataDir, SettingsFileName),
            Path.Combine(appDataDir, SettingsFileName),
            logsDir);
        CopyFileIfMissing(
            Path.Combine(legacyDataDir, DatabaseFileName),
            Path.Combine(appDataDir, DatabaseFileName),
            logsDir);
        CopyLegacyLogsIfMissing(
            Path.Combine(legacyDataDir, LogsDirectoryName),
            logsDir);
    }

    private static void CopyLegacyLogsIfMissing(string legacyLogsDir, string logsDir)
    {
        if (!Directory.Exists(legacyLogsDir))
        {
            return;
        }

        try
        {
            foreach (var sourcePath in Directory.EnumerateFiles(legacyLogsDir, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(legacyLogsDir, sourcePath);
                var destinationPath = Path.Combine(logsDir, relativePath);
                CopyFileIfMissing(sourcePath, destinationPath, logsDir);
            }
        }
        catch (Exception ex)
        {
            WriteStartupLog(logsDir, $"Failed to enumerate legacy logs from '{legacyLogsDir}': {ex.Message}");
        }
    }

    private static void CopyFileIfMissing(string sourcePath, string destinationPath, string logsDir)
    {
        if (!File.Exists(sourcePath) || File.Exists(destinationPath))
        {
            return;
        }

        try
        {
            var destinationDir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
            }

            File.Copy(sourcePath, destinationPath, overwrite: false);
        }
        catch (Exception ex)
        {
            WriteStartupLog(logsDir, $"Failed to migrate '{sourcePath}' to '{destinationPath}': {ex.Message}");
        }
    }

    private static void EnsureSettingsFile(string settingsPath, string logsDir)
    {
        if (File.Exists(settingsPath))
        {
            return;
        }

        try
        {
            File.WriteAllText(settingsPath, "{}");
        }
        catch (Exception ex)
        {
            WriteStartupLog(logsDir, $"Failed to create settings file '{settingsPath}': {ex.Message}");
        }
    }

    private static void WriteStartupLog(string logsDir, string message)
    {
        try
        {
            Directory.CreateDirectory(logsDir);
            File.AppendAllText(
                Path.Combine(logsDir, StartupLogFileName),
                $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
        }
        catch
        {
            // Startup diagnostics must never block application launch.
        }
    }
}
