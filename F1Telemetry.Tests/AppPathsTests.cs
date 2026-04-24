using System.IO;
using F1Telemetry.Core;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies user data path initialization and legacy data migration behavior.
/// </summary>
public sealed class AppPathsTests
{
    /// <summary>
    /// Verifies all user data paths are rooted under the application data directory.
    /// </summary>
    [Fact]
    public void GetPaths_WithTestRoot_ReturnsExpectedAppDataPaths()
    {
        var root = CreateRootPath();

        Assert.Equal(Path.Combine(root, "F1Telemetry"), AppPaths.GetAppDataDir(root));
        Assert.Equal(Path.Combine(root, "F1Telemetry", "settings.json"), AppPaths.GetSettingsPath(root));
        Assert.Equal(Path.Combine(root, "F1Telemetry", "f1telemetry.db"), AppPaths.GetDatabasePath(root));
        Assert.Equal(Path.Combine(root, "F1Telemetry", "logs"), AppPaths.GetLogsDir(root));
    }

    /// <summary>
    /// Verifies startup creates only the required directories and missing settings file.
    /// </summary>
    [Fact]
    public void InitializeUserData_CreatesDirectoriesAndMissingSettingsOnly()
    {
        var appDataRoot = CreateRootPath();
        var localAppDataRoot = CreateRootPath();

        AppPaths.InitializeUserData(appDataRoot, localAppDataRoot);

        Assert.True(Directory.Exists(Path.Combine(appDataRoot, "F1Telemetry")));
        Assert.True(Directory.Exists(Path.Combine(appDataRoot, "F1Telemetry", "logs")));
        Assert.Equal("{}", File.ReadAllText(Path.Combine(appDataRoot, "F1Telemetry", "settings.json")));
        Assert.False(File.Exists(Path.Combine(appDataRoot, "F1Telemetry", "f1telemetry.db")));
    }

    /// <summary>
    /// Verifies legacy data is copied once and existing AppData files are never overwritten.
    /// </summary>
    [Fact]
    public void InitializeUserData_MigratesLegacyDataWithoutOverwritingExistingFiles()
    {
        var appDataRoot = CreateRootPath();
        var localAppDataRoot = CreateRootPath();
        var newDir = Path.Combine(appDataRoot, "F1Telemetry");
        var oldDir = Path.Combine(localAppDataRoot, "F1Telemetry");
        Directory.CreateDirectory(Path.Combine(newDir, "logs"));
        Directory.CreateDirectory(Path.Combine(oldDir, "logs"));
        File.WriteAllText(Path.Combine(newDir, "settings.json"), "new-settings");
        File.WriteAllText(Path.Combine(newDir, "logs", "existing.log"), "new-log");
        File.WriteAllText(Path.Combine(oldDir, "settings.json"), "old-settings");
        File.WriteAllText(Path.Combine(oldDir, "f1telemetry.db"), "old-db");
        File.WriteAllText(Path.Combine(oldDir, "logs", "existing.log"), "old-log");
        File.WriteAllText(Path.Combine(oldDir, "logs", "legacy.log"), "legacy-log");

        AppPaths.InitializeUserData(appDataRoot, localAppDataRoot);

        Assert.Equal("new-settings", File.ReadAllText(Path.Combine(newDir, "settings.json")));
        Assert.Equal("old-db", File.ReadAllText(Path.Combine(newDir, "f1telemetry.db")));
        Assert.Equal("new-log", File.ReadAllText(Path.Combine(newDir, "logs", "existing.log")));
        Assert.Equal("legacy-log", File.ReadAllText(Path.Combine(newDir, "logs", "legacy.log")));
    }

    /// <summary>
    /// Verifies migration copy failures are logged and do not prevent startup initialization.
    /// </summary>
    [Fact]
    public void InitializeUserData_WhenLegacyCopyFails_LogsAndContinues()
    {
        var appDataRoot = CreateRootPath();
        var localAppDataRoot = CreateRootPath();
        var oldDir = Path.Combine(localAppDataRoot, "F1Telemetry");
        Directory.CreateDirectory(oldDir);
        var lockedSettingsPath = Path.Combine(oldDir, "settings.json");

        using var lockedSettings = new FileStream(
            lockedSettingsPath,
            FileMode.Create,
            FileAccess.ReadWrite,
            FileShare.None);
        using var writer = new StreamWriter(lockedSettings, leaveOpen: true);
        writer.Write("legacy-settings");
        writer.Flush();
        lockedSettings.Position = 0;

        var exception = Record.Exception(() => AppPaths.InitializeUserData(appDataRoot, localAppDataRoot));

        Assert.Null(exception);
        Assert.True(File.Exists(Path.Combine(appDataRoot, "F1Telemetry", "settings.json")));
        var startupLog = File.ReadAllText(Path.Combine(appDataRoot, "F1Telemetry", "logs", "startup.log"));
        Assert.Contains("settings.json", startupLog, StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateRootPath()
    {
        return Path.Combine(Path.GetTempPath(), "F1TelemetryTests", Guid.NewGuid().ToString("N"));
    }
}
