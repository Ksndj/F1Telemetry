using System.IO;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
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
        Assert.Equal("3.1.4", VersionInfo.CurrentVersion);
    }

    /// <summary>
    /// Verifies the UI version text is pinned to the current release version.
    /// </summary>
    [Fact]
    public void DisplayVersion_ReturnsCurrentReleaseVersion()
    {
        Assert.Equal("v3.1.4", VersionInfo.DisplayVersion);
    }

    /// <summary>
    /// Verifies the dashboard title reflects the current release version.
    /// </summary>
    [Fact]
    public void DashboardViewModel_AppTitleText_ContainsCurrentReleaseVersion()
    {
        var viewModel = Assert.IsType<DashboardViewModel>(
            RuntimeHelpers.GetUninitializedObject(typeof(DashboardViewModel)));

        Assert.Contains("3.1.4", viewModel.AppTitleText, StringComparison.Ordinal);
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
    /// Verifies application version metadata uses the current application version.
    /// </summary>
    [Fact]
    public void ApplicationVersionMetadata_UseCurrentApplicationVersion()
    {
        var root = FindRepositoryRoot();
        var document = XDocument.Load(Path.Combine(root, "Directory.Build.props"));
        var propertyGroup = Assert.Single(document.Root!.Elements("PropertyGroup"));

        Assert.Equal("3.1.4", propertyGroup.Element("VersionPrefix")?.Value);
        Assert.Equal("$(VersionPrefix)", propertyGroup.Element("Version")?.Value);
        Assert.Equal("$(VersionPrefix).0", propertyGroup.Element("AssemblyVersion")?.Value);
        Assert.Equal("$(VersionPrefix).0", propertyGroup.Element("FileVersion")?.Value);
        Assert.Equal("$(VersionPrefix)", propertyGroup.Element("InformationalVersion")?.Value);
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
