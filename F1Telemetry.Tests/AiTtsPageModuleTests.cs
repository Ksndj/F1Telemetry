using System.IO;
using System.Xml.Linq;
using F1Telemetry.App.ViewModels;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies the AI/TTS page is routed through a dedicated page view model.
/// </summary>
public sealed class AiTtsPageModuleTests
{
    /// <summary>
    /// Verifies the shell gives the AI/TTS slot its dedicated page view model.
    /// </summary>
    [Fact]
    public void ShellStyles_AiTtsTemplate_UsesAiTtsPageViewModel()
    {
        var document = XDocument.Load(FindRepositoryFile("F1Telemetry.App", "Styles", "ShellStyles.xaml"));
        XNamespace xamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

        var aiTtsTemplate = document.Descendants(xamlNamespace + "DataTemplate")
            .Single(element => element.Attribute(xNamespace + "Key")?.Value == "AiTtsContentTemplate");
        var aiTtsView = aiTtsTemplate.Descendants().Single(element => element.Name.LocalName == "AiTtsView");
        var dataContextBinding = aiTtsView.Attribute("DataContext")?.Value;

        Assert.NotNull(dataContextBinding);
        Assert.Contains("DataContext.AiTts", dataContextBinding, StringComparison.Ordinal);
        Assert.Contains("AncestorType={x:Type Window}", dataContextBinding, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies the dashboard exposes a dedicated page module for AI/TTS state.
    /// </summary>
    [Fact]
    public void DashboardViewModel_ExposesAiTtsPageViewModel()
    {
        var property = typeof(DashboardViewModel).GetProperty("AiTts");

        Assert.NotNull(property);
        Assert.Equal("F1Telemetry.App.ViewModels.AiTtsViewModel", property.PropertyType.FullName);
    }

    /// <summary>
    /// Verifies the AI/TTS page view model owns the root page binding surface.
    /// </summary>
    [Fact]
    public void AiTtsViewModel_DefinesPageBindingSurface()
    {
        var viewModelType = Type.GetType("F1Telemetry.App.ViewModels.AiTtsViewModel, F1Telemetry.App");

        Assert.NotNull(viewModelType);

        string[] propertyNames =
        [
            "AiEnabled",
            "AiBaseUrl",
            "AiModel",
            "AiApiKey",
            "AiApiKeyStatusText",
            "AiSettingsSaveStatusText",
            "TtsEnabled",
            "HasAvailableVoices",
            "AvailableVoices",
            "TtsVoiceName",
            "TtsVoiceStatusText",
            "TtsVolume",
            "TtsRate",
            "RaceWeekendTyreSetsStatusText",
            "RaceWeekendTyrePlanStatusText",
            "RaceWeekendTyreInventoryItems",
            "RaceWeekendTyreMaxWearPercent",
            "ReadTyreSetsInventoryCommand",
            "ClearTyreInventoryCommand",
            "SaveTyreInventoryCommand",
            "VoiceAssistantEnabled",
            "VoiceAssistantEnableTtsAnswer",
            "VoiceAssistantQuestionText",
            "VoiceAssistantTelemetryNoticeText",
            "VoiceAssistantRecognizedText",
            "VoiceAssistantIntentText",
            "VoiceAssistantModeText",
            "VoiceAssistantStatusText",
            "VoiceAssistantAdviceTypeText",
            "VoiceAssistantSummaryText",
            "VoiceAssistantReasonText",
            "VoiceAssistantRecommendedActionText",
            "VoiceAssistantConfidenceText",
            "VoiceAssistantRiskLevelText",
            "VoiceAssistantMissingDataText",
            "VoiceAssistantMissingDataDetailText",
            "RaceAssistantHistory",
            "AskRaceAssistantQuestionCommand",
            "ToggleRaceAssistantVoiceCommand",
            "CancelRaceAssistantQuestionCommand",
            "AiTtsLogs"
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
