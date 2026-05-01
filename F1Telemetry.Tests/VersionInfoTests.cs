using System.Runtime.CompilerServices;
using F1Telemetry.App;
using F1Telemetry.App.ViewModels;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies application version text shown in the shell.
/// </summary>
public sealed class VersionInfoTests
{
    /// <summary>
    /// Verifies the raw current version is pinned to the current release version.
    /// </summary>
    [Fact]
    public void CurrentVersion_ReturnsCurrentReleaseVersion()
    {
        Assert.Equal("1.5.0", VersionInfo.CurrentVersion);
    }

    /// <summary>
    /// Verifies the UI version text is pinned to the current release version.
    /// </summary>
    [Fact]
    public void DisplayVersion_ReturnsCurrentReleaseVersion()
    {
        Assert.Equal("v1.5.0", VersionInfo.DisplayVersion);
    }

    /// <summary>
    /// Verifies the dashboard title reflects the current release version.
    /// </summary>
    [Fact]
    public void DashboardViewModel_AppTitleText_ContainsCurrentReleaseVersion()
    {
        var viewModel = Assert.IsType<DashboardViewModel>(
            RuntimeHelpers.GetUninitializedObject(typeof(DashboardViewModel)));

        Assert.Contains("1.5.0", viewModel.AppTitleText, StringComparison.Ordinal);
        Assert.DoesNotContain(" V1", viewModel.AppTitleText, StringComparison.Ordinal);
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
