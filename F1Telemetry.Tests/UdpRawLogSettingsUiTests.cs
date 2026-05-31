using System.IO;
using System.Xml.Linq;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies the Settings page exposes raw UDP log controls.
/// </summary>
public sealed class UdpRawLogSettingsUiTests
{
    private static readonly string[] RuntimeReadOnlyLogStatusProperties =
    [
        "AppLogDirectoryText",
        "AppLogLastFileSizeText",
        "AppLogLastWriteTimeText",
        "RaceAssistantLogDirectoryText",
        "RaceAssistantLogLastFileSizeText",
        "RaceAssistantLogLastWriteTimeText"
    ];

    /// <summary>
    /// Verifies the shell UDP port controls stay together as one non-wrapping top-status item.
    /// </summary>
    [Fact]
    public void MainWindow_UdpPortStatusChip_UsesNonWrappingGrid()
    {
        var document = XDocument.Load(FindRepositoryFile("F1Telemetry.App", "MainWindow.xaml"));
        var topStatusWrapPanel = FindElementByName(document, "TopStatusWrapPanel");
        var udpPortChip = Assert.Single(topStatusWrapPanel.Elements(), element => HasName(element, "UdpPortStatusChip"));
        var udpPortChipXaml = udpPortChip.ToString(SaveOptions.DisableFormatting);

        Assert.Equal("Border", udpPortChip.Name.LocalName);
        Assert.DoesNotContain("MaxWidth=", udpPortChipXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<WrapPanel", udpPortChipXaml, StringComparison.Ordinal);

        var layoutGrid = Assert.Single(udpPortChip.Elements(), element => element.Name.LocalName == "Grid");
        Assert.Equal("Center", layoutGrid.Attribute("VerticalAlignment")?.Value);
        Assert.Equal(7, layoutGrid.Descendants().Count(element => element.Name.LocalName == "ColumnDefinition"));

        Assert.Contains("Text=\"监听端口\"", udpPortChipXaml, StringComparison.Ordinal);
        Assert.Equal(3, CountOccurrences(udpPortChipXaml, "Width=\"8\""));

        var portTextBox = Assert.Single(layoutGrid.Descendants(), element => element.Name.LocalName == "TextBox");
        Assert.Equal("104", portTextBox.Attribute("Width")?.Value);
        Assert.Equal("30", portTextBox.Attribute("Height")?.Value);
        Assert.Equal("Center", portTextBox.Attribute("VerticalAlignment")?.Value);
        Assert.Equal(
            "{Binding PortText, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}",
            portTextBox.Attribute("Text")?.Value);

        var buttons = layoutGrid.Descendants()
            .Where(element => element.Name.LocalName == "Button")
            .ToArray();
        Assert.Equal(2, buttons.Length);
        Assert.All(buttons, button =>
        {
            Assert.Equal("96", button.Attribute("Width")?.Value);
            Assert.Equal("30", button.Attribute("Height")?.Value);
            Assert.Equal("Center", button.Attribute("VerticalAlignment")?.Value);
            Assert.Null(button.Attribute("IsEnabled"));
        });
        Assert.Contains(buttons, button => button.Attribute("Command")?.Value == "{Binding StartListeningCommand}");
        Assert.Contains(buttons, button => button.Attribute("Command")?.Value == "{Binding StopListeningCommand}");
    }

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
        Assert.Contains("UdpRawLogLastFileSizeText", xaml, StringComparison.Ordinal);
        Assert.Contains("UdpRawLogLastWriteTimeText", xaml, StringComparison.Ordinal);
        Assert.Contains("UdpRawLogStatusText", xaml, StringComparison.Ordinal);
        Assert.Contains("UdpRawLogWrittenPacketCount", xaml, StringComparison.Ordinal);
        Assert.Contains("UdpRawLogDroppedPacketCount", xaml, StringComparison.Ordinal);
        Assert.Contains("UdpRawLogLastErrorText", xaml, StringComparison.Ordinal);
        Assert.Contains("OpenUdpRawLogDirectoryCommand", xaml, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies SettingsView binds to App and RaceAssistant log controls.
    /// </summary>
    [Fact]
    public void SettingsView_BindsRuntimeLogControls()
    {
        var document = XDocument.Load(FindRepositoryFile("F1Telemetry.App", "Views", "SettingsView.xaml"));
        var xaml = document.ToString(SaveOptions.DisableFormatting);

        Assert.Contains("EnableAppFileLog", xaml, StringComparison.Ordinal);
        Assert.Contains("EnableRaceAssistantAuditLog", xaml, StringComparison.Ordinal);
        Assert.Contains("RaceAssistantLogPromptSummary", xaml, StringComparison.Ordinal);
        Assert.Contains("AppLogDirectoryText", xaml, StringComparison.Ordinal);
        Assert.Contains("RaceAssistantLogDirectoryText", xaml, StringComparison.Ordinal);
        Assert.Contains("AppLogLastFileSizeText", xaml, StringComparison.Ordinal);
        Assert.Contains("RaceAssistantLogLastWriteTimeText", xaml, StringComparison.Ordinal);
        Assert.Contains("MaxLogFileSizeMbText", xaml, StringComparison.Ordinal);
        Assert.Contains("MaxLogRetentionDaysText", xaml, StringComparison.Ordinal);
        Assert.Contains("OpenAppLogDirectoryCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("OpenRaceAssistantLogDirectoryCommand", xaml, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies read-only raw UDP counters use one-way bindings so Settings can load without WPF source updates.
    /// </summary>
    [Fact]
    public void SettingsView_UsesOneWayBindingsForReadOnlyRawUdpCounters()
    {
        var document = XDocument.Load(FindRepositoryFile("F1Telemetry.App", "Views", "SettingsView.xaml"));
        var xaml = document.ToString(SaveOptions.DisableFormatting);

        Assert.Contains("{Binding UdpRawLogWrittenPacketCount, Mode=OneWay}", xaml, StringComparison.Ordinal);
        Assert.Contains("{Binding UdpRawLogDroppedPacketCount, Mode=OneWay}", xaml, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies read-only runtime log status fields use one-way bindings so Settings can load without WPF source updates.
    /// </summary>
    [Fact]
    public void SettingsView_UsesOneWayBindingsForReadOnlyRuntimeLogStatusFields()
    {
        var document = XDocument.Load(FindRepositoryFile("F1Telemetry.App", "Views", "SettingsView.xaml"));
        var xaml = document.ToString(SaveOptions.DisableFormatting);

        foreach (var propertyName in RuntimeReadOnlyLogStatusProperties)
        {
            Assert.Contains($"{{Binding {propertyName}, Mode=OneWay}}", xaml, StringComparison.Ordinal);
        }

        Assert.DoesNotContain(
            "{Binding RaceAssistantLogLastFileSizeText, Mode=TwoWay",
            xaml,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "{Binding RaceAssistantLogLastFileSizeText, Mode=OneWayToSource",
            xaml,
            StringComparison.Ordinal);

        foreach (var textBox in document.Descendants().Where(element => element.Name.LocalName == "TextBox"))
        {
            var textBinding = textBox.Attributes()
                .FirstOrDefault(attribute => attribute.Name.LocalName == "Text")
                ?.Value;
            if (string.IsNullOrWhiteSpace(textBinding))
            {
                continue;
            }

            foreach (var propertyName in RuntimeReadOnlyLogStatusProperties)
            {
                if (textBinding.Contains(propertyName, StringComparison.Ordinal))
                {
                    Assert.Contains("Mode=OneWay", textBinding, StringComparison.Ordinal);
                }
            }
        }
    }

    /// <summary>
    /// Verifies SettingsView exposes the steering-wheel voice AI input and microphone controls.
    /// </summary>
    [Fact]
    public void SettingsView_BindsVoiceAiInputAndMicrophoneControls()
    {
        var document = XDocument.Load(FindRepositoryFile("F1Telemetry.App", "Views", "SettingsView.xaml"));
        var xaml = document.ToString(SaveOptions.DisableFormatting);

        Assert.Contains("VoiceAiEnabled", xaml, StringComparison.Ordinal);
        Assert.Contains("BindVoiceAiInputCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("ClearVoiceAiInputCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("VoiceAiBindingText", xaml, StringComparison.Ordinal);
        Assert.Contains("VoiceAiTalkModeOptions", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectedVoiceAiTalkModeOption", xaml, StringComparison.Ordinal);
        Assert.Contains("VoiceAiMicrophoneDevices", xaml, StringComparison.Ordinal);
        Assert.Contains("VoiceAiMicrophoneDeviceId", xaml, StringComparison.Ordinal);
        Assert.Contains("RefreshMicrophonesCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("TestMicrophoneCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("VoiceAiMicrophoneTestLevel", xaml, StringComparison.Ordinal);
        Assert.Contains("VoiceAiMicrophoneStatusText", xaml, StringComparison.Ordinal);
        Assert.Contains("VoiceAiStatusText", xaml, StringComparison.Ordinal);
        Assert.Contains("VoiceAiNoiseReductionEnabled", xaml, StringComparison.Ordinal);
        Assert.Contains("VoiceAiHighPassFilterEnabled", xaml, StringComparison.Ordinal);
        Assert.Contains("VoiceAiNoiseGateEnabled", xaml, StringComparison.Ordinal);
        Assert.Contains("VoiceAiVadEnabled", xaml, StringComparison.Ordinal);
        Assert.Contains("VoiceAiAutoGainEnabled", xaml, StringComparison.Ordinal);
        Assert.Contains("VoiceAiNoiseGateThresholdDbText", xaml, StringComparison.Ordinal);
        Assert.Contains("VoiceAiMaxRecordingSecondsText", xaml, StringComparison.Ordinal);
        Assert.Contains("VoiceAiMinRecognitionConfidenceText", xaml, StringComparison.Ordinal);
        Assert.Contains("VoiceAiRecognitionStatusDetailText", xaml, StringComparison.Ordinal);
        Assert.Contains("HorizontalScrollBarVisibility=\"Disabled\"", xaml, StringComparison.Ordinal);
        Assert.Contains("TextTrimming=\"CharacterEllipsis\"", xaml, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies read-only log status display bindings stay one-way on the Settings page.
    /// </summary>
    [Fact]
    public void SettingsView_UsesOneWayBindingsForReadOnlyRuntimeLogStatus()
    {
        var document = XDocument.Load(FindRepositoryFile("F1Telemetry.App", "Views", "SettingsView.xaml"));
        var xaml = document.ToString(SaveOptions.DisableFormatting);

        var readOnlyBindings = new[]
        {
            "AppLogDirectoryText",
            "AppLogLastFileSizeText",
            "AppLogLastWriteTimeText",
            "RaceAssistantLogDirectoryText",
            "RaceAssistantLogLastFileSizeText",
            "RaceAssistantLogLastWriteTimeText"
        };

        foreach (var binding in readOnlyBindings)
        {
            Assert.Contains($"{{Binding {binding}, Mode=OneWay}}", xaml, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("RaceAssistantLogLastFileSizeText, Mode=TwoWay", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("RaceAssistantLogLastFileSizeText, Mode=OneWayToSource", xaml, StringComparison.Ordinal);
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

    private static XElement FindElementByName(XDocument document, string name)
    {
        return Assert.Single(document.Descendants(), element => HasName(element, name));
    }

    private static bool HasName(XElement element, string name)
    {
        return element.Attributes().Any(attribute => attribute.Name.LocalName == "Name" && attribute.Value == name);
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
