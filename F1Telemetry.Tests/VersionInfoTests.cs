using System.IO;
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
        Assert.Equal("2.0.0-beta3", VersionInfo.CurrentVersion);
    }

    /// <summary>
    /// Verifies the UI version text is pinned to the current release version.
    /// </summary>
    [Fact]
    public void DisplayVersion_ReturnsCurrentReleaseVersion()
    {
        Assert.Equal("v2.0.0-beta3", VersionInfo.DisplayVersion);
    }

    /// <summary>
    /// Verifies the dashboard title reflects the current release version.
    /// </summary>
    [Fact]
    public void DashboardViewModel_AppTitleText_ContainsCurrentReleaseVersion()
    {
        var viewModel = Assert.IsType<DashboardViewModel>(
            RuntimeHelpers.GetUninitializedObject(typeof(DashboardViewModel)));

        Assert.Contains("2.0.0-beta3", viewModel.AppTitleText, StringComparison.Ordinal);
        Assert.DoesNotContain(" V1", viewModel.AppTitleText, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies the dashboard subtitle reuses the current UI version text.
    /// </summary>
    [Fact]
    public void DashboardViewModel_Subtitle_ContainsCurrentDisplayVersion()
    {
        var viewModel = Assert.IsType<DashboardViewModel>(
            RuntimeHelpers.GetUninitializedObject(typeof(DashboardViewModel)));

        Assert.Contains(VersionInfo.DisplayVersion, viewModel.Subtitle, StringComparison.Ordinal);
        Assert.Contains("实时遥测助手", viewModel.Subtitle, StringComparison.Ordinal);
        Assert.DoesNotContain("v1.2.0", viewModel.Subtitle, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies the download entry point targets the project GitHub Releases page.
    /// </summary>
    [Fact]
    public void GitHubReleasesUrl_ReturnsProjectReleasesPage()
    {
        Assert.Equal("https://github.com/Ksndj/F1Telemetry/releases", VersionInfo.GitHubReleasesUrl);
    }

    /// <summary>
    /// Verifies release scripts and installer metadata use the same application version.
    /// </summary>
    [Fact]
    public void ReleaseFiles_UseCurrentApplicationVersion()
    {
        var root = FindRepositoryRoot();
        var directoryBuildProps = File.ReadAllText(Path.Combine(root, "Directory.Build.props"));
        var publishScript = File.ReadAllText(Path.Combine(root, "build", "publish.ps1"));
        var innoScript = File.ReadAllText(Path.Combine(root, "build", "F1Telemetry.iss"));

        Assert.Contains("<Version>2.0.0-beta3</Version>", directoryBuildProps, StringComparison.Ordinal);
        Assert.Contains("<VersionPrefix>2.0.0</VersionPrefix>", directoryBuildProps, StringComparison.Ordinal);
        Assert.Contains("<AssemblyVersion>2.0.0.0</AssemblyVersion>", directoryBuildProps, StringComparison.Ordinal);
        Assert.Contains("<FileVersion>2.0.0.0</FileVersion>", directoryBuildProps, StringComparison.Ordinal);
        Assert.Contains("<InformationalVersion>2.0.0-beta3</InformationalVersion>", directoryBuildProps, StringComparison.Ordinal);
        Assert.Contains("/p:Version=2.0.0-beta3", publishScript, StringComparison.Ordinal);
        Assert.Contains("/p:AssemblyVersion=2.0.0.0", publishScript, StringComparison.Ordinal);
        Assert.Contains("/p:FileVersion=2.0.0.0", publishScript, StringComparison.Ordinal);
        Assert.Contains("/p:InformationalVersion=2.0.0-beta3", publishScript, StringComparison.Ordinal);
        Assert.Contains("#define MyAppVersion \"2.0.0-beta3\"", innoScript, StringComparison.Ordinal);
        Assert.Contains("OutputBaseFilename=F1Telemetry-2.0.0-beta3-win-x64-setup", innoScript, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Directory.Build.props")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root.");
    }
}
