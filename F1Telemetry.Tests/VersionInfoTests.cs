using F1Telemetry.App;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies application version text shown in the shell.
/// </summary>
public sealed class VersionInfoTests
{
    /// <summary>
    /// Verifies the UI version text is pinned to the current release version.
    /// </summary>
    [Fact]
    public void DisplayVersion_ReturnsCurrentReleaseVersion()
    {
        Assert.Equal("v1.0.1", VersionInfo.DisplayVersion);
    }

    /// <summary>
    /// Verifies the download entry point targets the project GitHub Releases page.
    /// </summary>
    [Fact]
    public void GitHubReleasesUrl_ReturnsProjectReleasesPage()
    {
        Assert.Equal("https://github.com/Ksndj/F1Telemetry/releases", VersionInfo.GitHubReleasesUrl);
    }
}
