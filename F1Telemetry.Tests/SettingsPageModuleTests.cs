using System.IO;
using System.Xml.Linq;
using F1Telemetry.App.ViewModels;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies the Settings page is routed through a dedicated page view model.
/// </summary>
public sealed class SettingsPageModuleTests
{
    /// <summary>
    /// Verifies the shell gives the Settings slot its dedicated page view model.
    /// </summary>
    [Fact]
    public void ShellStyles_SettingsTemplate_UsesSettingsPageViewModel()
    {
        var document = XDocument.Load(FindRepositoryFile("F1Telemetry.App", "Styles", "ShellStyles.xaml"));
        XNamespace xamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

        var settingsTemplate = document.Descendants(xamlNamespace + "DataTemplate")
            .Single(element => element.Attribute(xNamespace + "Key")?.Value == "SettingsContentTemplate");
        var settingsView = settingsTemplate.Descendants().Single(element => element.Name.LocalName == "SettingsView");
        var dataContextBinding = settingsView.Attribute("DataContext")?.Value;

        Assert.NotNull(dataContextBinding);
        Assert.Contains("DataContext.Settings", dataContextBinding, StringComparison.Ordinal);
        Assert.Contains("AncestorType={x:Type Window}", dataContextBinding, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies the dashboard exposes a dedicated page module for Settings state.
    /// </summary>
    [Fact]
    public void DashboardViewModel_ExposesSettingsPageViewModel()
    {
        var property = typeof(DashboardViewModel).GetProperty("Settings");

        Assert.NotNull(property);
        Assert.Equal("F1Telemetry.App.ViewModels.SettingsViewModel", property.PropertyType.FullName);
    }

    /// <summary>
    /// Verifies the Settings page view model owns the root page binding surface.
    /// </summary>
    [Fact]
    public void SettingsViewModel_DefinesPageBindingSurface()
    {
        var viewModelType = Type.GetType("F1Telemetry.App.ViewModels.SettingsViewModel, F1Telemetry.App");

        Assert.NotNull(viewModelType);

        string[] propertyNames =
        [
            "DownloadLatestVersionCommand",
            "PortText",
            "ConnectionStateText",
            "ListeningPortText",
            "ApplicationVersionText",
            "StatusMessage",
            "OpenAppLogDirectoryCommand",
            "OpenRaceAssistantLogDirectoryCommand",
            "EnableAppFileLog",
            "EnableRaceAssistantAuditLog",
            "RaceAssistantLogPromptSummary",
            "AppLogDirectoryText",
            "AppLogLastFilePathText",
            "AppLogLastFileSizeText",
            "AppLogLastWriteTimeText",
            "RaceAssistantLogDirectoryText",
            "RaceAssistantLogLastFilePathText",
            "RaceAssistantLogLastFileSizeText",
            "RaceAssistantLogLastWriteTimeText",
            "MaxLogFileSizeMbText",
            "MaxLogRetentionDaysText",
            "LogSettingsStatusText",
            "OpenUdpRawLogDirectoryCommand",
            "UdpRawLogEnabled",
            "UdpRawLogStatusText",
            "UdpRawLogDirectoryText",
            "UdpRawLogLastFilePathText",
            "UdpRawLogLastFileSizeText",
            "UdpRawLogLastWriteTimeText",
            "UdpRawLogWrittenPacketCount",
            "UdpRawLogDroppedPacketCount",
            "UdpRawLogLastErrorText",
            "VoiceAiEnabled",
            "BindVoiceAiInputCommand",
            "ClearVoiceAiInputCommand",
            "VoiceAiBindingText",
            "VoiceAiBindingCaptureActive",
            "VoiceAiTalkModeOptions",
            "SelectedVoiceAiTalkModeOption",
            "VoiceAiMicrophoneDevices",
            "VoiceAiMicrophoneDeviceId",
            "RefreshMicrophonesCommand",
            "TestMicrophoneCommand",
            "VoiceAiNoiseReductionEnabled",
            "VoiceAiHighPassFilterEnabled",
            "VoiceAiNoiseGateEnabled",
            "VoiceAiVadEnabled",
            "VoiceAiAutoGainEnabled",
            "VoiceAiHighPassCutoffHzText",
            "VoiceAiNoiseGateThresholdDbText",
            "VoiceAiMaxRecordingSecondsText",
            "VoiceAiMinRecognitionConfidenceText",
            "VoiceAiPreSpeechPaddingMsText",
            "VoiceAiPostSpeechPaddingMsText",
            "VoiceAiMinSpeechDurationMsText",
            "VoiceAiMicrophoneTestLevel",
            "VoiceAiMicrophoneStatusText",
            "VoiceAiStatusText",
            "VoiceAiRecognitionStatusDetailText"
        ];

        foreach (var propertyName in propertyNames)
        {
            Assert.NotNull(viewModelType.GetProperty(propertyName));
        }
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
