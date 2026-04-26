using System.IO;
using System.Xml.Linq;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies the Settings page exposes raw UDP log controls.
/// </summary>
public sealed class UdpRawLogSettingsUiTests
{
    /// <summary>
    /// Verifies SettingsView binds to the raw UDP log toggle and status fields.
    /// </summary>
    [Fact]
    public void SettingsView_BindsRawUdpLogControls()
    {
        var document = XDocument.Load(FindRepositoryFile("F1Telemetry.App", "Views", "SettingsView.xaml"));
        var xaml = document.ToString(SaveOptions.DisableFormatting);

        Assert.Contains("UdpRawLogEnabled", xaml, StringComparison.Ordinal);
        Assert.Contains("UdpRawLogDirectoryText", xaml, StringComparison.Ordinal);
        Assert.Contains("UdpRawLogLastFilePathText", xaml, StringComparison.Ordinal);
        Assert.Contains("UdpRawLogStatusText", xaml, StringComparison.Ordinal);
        Assert.Contains("UdpRawLogWrittenPacketCount", xaml, StringComparison.Ordinal);
        Assert.Contains("UdpRawLogDroppedPacketCount", xaml, StringComparison.Ordinal);
    }

    private static string FindRepositoryFile(params string[] pathParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(pathParts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find repository file: {Path.Combine(pathParts)}");
    }
}
