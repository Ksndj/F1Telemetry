using System.Reflection;

namespace F1Telemetry.App;

/// <summary>
/// Provides the application version displayed in the shell.
/// </summary>
public static class VersionInfo
{
    private const string FallbackVersion = "1.2.0";

    /// <summary>
    /// Gets the GitHub Releases page used for manual update downloads.
    /// </summary>
    public const string GitHubReleasesUrl = "https://github.com/Ksndj/F1Telemetry/releases";

    /// <summary>
    /// Gets the current semantic application version.
    /// </summary>
    public static string CurrentVersion => GetVersion(Assembly.GetExecutingAssembly());

    /// <summary>
    /// Gets the current application version formatted for UI display.
    /// </summary>
    public static string DisplayVersion => $"v{CurrentVersion}";

    private static string GetVersion(Assembly assembly)
    {
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        var normalizedInformationalVersion = NormalizeVersionText(informationalVersion);
        if (!string.IsNullOrWhiteSpace(normalizedInformationalVersion))
        {
            return normalizedInformationalVersion;
        }

        var assemblyVersion = assembly.GetName().Version;
        if (assemblyVersion is null)
        {
            return FallbackVersion;
        }

        return $"{assemblyVersion.Major}.{assemblyVersion.Minor}.{assemblyVersion.Build}";
    }

    private static string NormalizeVersionText(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return string.Empty;
        }

        var normalized = version.Trim();
        if (normalized.StartsWith('v'))
        {
            normalized = normalized[1..];
        }

        var metadataIndex = normalized.IndexOf('+', StringComparison.Ordinal);
        return metadataIndex >= 0 ? normalized[..metadataIndex] : normalized;
    }
}
