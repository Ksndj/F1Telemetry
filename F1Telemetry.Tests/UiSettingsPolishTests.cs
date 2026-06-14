using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;
using F1Telemetry.App.AttachedProperties;
using F1Telemetry.App.ViewModels;
using F1Telemetry.App.Views;
using F1Telemetry.TTS.Services;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies UI polish for settings controls and resilient voice discovery.
/// </summary>
public sealed class UiSettingsPolishTests
{
    /// <summary>
    /// Verifies the API key password box pushes typed values into the bound source immediately.
    /// </summary>
    [Fact]
    public void PasswordBoxBinding_UpdatesSourceWhenPasswordChanges()
    {
        RunOnStaThread(() =>
        {
            var source = new PasswordBindingSource();
            var passwordBox = new PasswordBox();
            BindingOperations.SetBinding(
                passwordBox,
                PasswordBoxBinding.BoundPasswordProperty,
                new Binding(nameof(PasswordBindingSource.AiApiKey))
                {
                    Source = source,
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                });

            passwordBox.Password = "configured-secret";
            passwordBox.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);

            Assert.Equal("configured-secret", source.AiApiKey);
        });
    }

    /// <summary>
    /// Verifies voice discovery failure degrades to an empty catalog with a visible status message.
    /// </summary>
    [Fact]
    public void WindowsVoiceCatalog_LoadVoices_WhenProviderThrows_ReturnsEmptyCatalogWithStatus()
    {
        var catalog = new WindowsVoiceCatalog(() => throw new InvalidOperationException("speech unavailable"));

        var result = catalog.LoadVoices();

        Assert.Empty(result.VoiceNames);
        Assert.Equal(string.Empty, result.DefaultVoiceName);
        Assert.Contains("未发现可用语音", result.StatusMessage, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies an empty Windows voice result is surfaced as a safe empty catalog.
    /// </summary>
    [Fact]
    public void WindowsVoiceCatalog_LoadVoices_WhenProviderReturnsNoVoices_ReturnsEmptyCatalogWithStatus()
    {
        var catalog = new WindowsVoiceCatalog(() => new WindowsVoiceCatalogResult(Array.Empty<string>(), string.Empty, string.Empty));

        var result = catalog.LoadVoices();

        Assert.Empty(result.VoiceNames);
        Assert.Equal(string.Empty, result.DefaultVoiceName);
        Assert.Contains("未发现可用语音", result.StatusMessage, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies the dedicated AI/TTS page exposes voice selection as a Windows voice dropdown.
    /// </summary>
    [Fact]
    public void AiTtsView_UsesVoiceComboBoxBinding()
    {
        RunOnStaThread(() =>
        {
            var view = new AiTtsView
            {
                DataContext = new VoiceSelectionBindingSource()
            };
            var host = new Window { Content = view };

            try
            {
                host.Show();
                view.UpdateLayout();

                var comboBox = Assert.Single(FindDescendants<ComboBox>(view));
                AssertBindingPath(comboBox, ItemsControl.ItemsSourceProperty, nameof(VoiceSelectionBindingSource.AvailableVoices));
                AssertBindingPath(comboBox, Selector.SelectedItemProperty, nameof(VoiceSelectionBindingSource.TtsVoiceName));
            }
            finally
            {
                host.Close();
            }
        });
    }

    /// <summary>
    /// Verifies the dedicated AI/TTS page keeps key bindings while exposing the polished layout contract.
    /// </summary>
    [Fact]
    public void AiTtsView_DefinesPolishedLayoutContracts()
    {
        var document = XDocument.Load(FindRepositoryFile("F1Telemetry.App", "Views", "AiTtsView.xaml"));
        var source = document.ToString(SaveOptions.DisableFormatting);
        var scrollViewer = FindElementByName(document, "AiTtsScrollViewer");
        var voiceComboBox = FindElementByName(document, "TtsVoiceComboBox");
        var tyreInventory = FindElementByName(document, "RaceWeekendTyreInventoryItems");
        var historyItems = FindElementByName(document, "RaceAssistantHistoryItems");
        var logItems = FindElementByName(document, "AiTtsLogsItems");
        var logMessage = FindElementByName(document, "AiTtsLogMessageText");
        var decrementButton = FindElementByName(document, "TyreInventoryDecrementButton");
        var incrementButton = FindElementByName(document, "TyreInventoryIncrementButton");

        Assert.Equal("Disabled", scrollViewer.Attribute("HorizontalScrollBarVisibility")?.Value);
        Assert.Equal("Auto", scrollViewer.Attribute("VerticalScrollBarVisibility")?.Value);
        Assert.Equal("{StaticResource DarkComboBoxStyle}", voiceComboBox.Attribute("Style")?.Value);
        Assert.Equal("240", voiceComboBox.Attribute("MaxDropDownHeight")?.Value);
        Assert.Equal("{Binding HasAvailableVoices}", voiceComboBox.Attribute("IsEnabled")?.Value);
        Assert.Equal("{Binding AvailableVoices}", voiceComboBox.Attribute("ItemsSource")?.Value);
        Assert.Equal("{Binding TtsVoiceName, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}", voiceComboBox.Attribute("SelectedItem")?.Value);
        Assert.Equal("{Binding RaceWeekendTyreInventoryItems}", tyreInventory.Attribute("ItemsSource")?.Value);
        Assert.Equal("{Binding RaceAssistantHistory}", historyItems.Attribute("ItemsSource")?.Value);
        Assert.Equal("{Binding AiTtsLogs}", logItems.Attribute("ItemsSource")?.Value);
        Assert.Equal("CharacterEllipsis", logMessage.Attribute("TextTrimming")?.Value);
        Assert.Equal("{Binding Message}", logMessage.Attribute("ToolTip")?.Value);
        Assert.Equal("{Binding DecrementCommand}", decrementButton.Attribute("Command")?.Value);
        Assert.Equal("{Binding IncrementCommand}", incrementButton.Attribute("Command")?.Value);
        Assert.Equal("Center", decrementButton.Attribute("HorizontalContentAlignment")?.Value);
        Assert.Equal("Center", incrementButton.Attribute("HorizontalContentAlignment")?.Value);
        Assert.Contains("attached:PasswordBoxBinding.BoundPassword=\"{Binding AiApiKey, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"", source, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"AiSettingsPanel\"", source, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"TtsSettingsPanel\"", source, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"RaceAssistantPanel\"", source, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"TyreInventoryPanel\"", source, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"AiTtsLogsPanel\"", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies the legacy fallback dashboard keeps the same voice dropdown behavior.
    /// </summary>
    [Fact]
    public void LegacyDashboardView_UsesVoiceComboBoxBinding()
    {
        RunOnStaThread(() =>
        {
            var view = new LegacyDashboardView
            {
                DataContext = new VoiceSelectionBindingSource()
            };
            var host = new Window { Content = view };

            try
            {
                host.Show();
                view.UpdateLayout();

                var comboBox = Assert.Single(FindDescendants<ComboBox>(view));
                AssertBindingPath(comboBox, ItemsControl.ItemsSourceProperty, nameof(VoiceSelectionBindingSource.AvailableVoices));
                AssertBindingPath(comboBox, Selector.SelectedItemProperty, nameof(VoiceSelectionBindingSource.TtsVoiceName));
            }
            finally
            {
                host.Close();
            }
        });
    }

    /// <summary>
    /// Verifies the shell UDP port input writes edits back to the dashboard view model immediately.
    /// </summary>
    [Fact]
    public void TopStatusBar_UdpPortTextBox_UsesTwoWayPropertyChangedBinding()
    {
        var document = XDocument.Load(FindRepositoryFile("F1Telemetry.App", "Views", "Shell", "TopStatusBar.xaml"));
        var textBoxes = document.Descendants()
            .Where(element => element.Name.LocalName == "TextBox")
            .Select(element => element.Attribute("Text")?.Value)
            .Where(value => value is not null)
            .ToArray();

        Assert.Contains(
            "{Binding PortText, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}",
            textBoxes);
    }

    /// <summary>
    /// Verifies the voice AI settings rows are not collapsed into a shared final row.
    /// </summary>
    [Fact]
    public void SettingsView_DefinesAllVoiceAiRows()
    {
        var document = XDocument.Load(FindRepositoryFile("F1Telemetry.App", "Views", "SettingsView.xaml"));
        var voiceAiGrid = document.Descendants()
            .First(element =>
                element.Name.LocalName == "TextBlock"
                && element.Attribute("Text")?.Value == "方向盘语音 AI")
            .ElementsAfterSelf()
            .First(element => element.Name.LocalName == "Grid");
        var rowCount = voiceAiGrid.Descendants()
            .Count(element => element.Name.LocalName == "RowDefinition");

        Assert.True(rowCount >= 10);
    }

    /// <summary>
    /// Verifies section titles keep a readable foreground on dark card backgrounds.
    /// </summary>
    [Fact]
    public void SharedStyles_SectionTitleTextStyle_UsesReadableForeground()
    {
        var document = XDocument.Load(FindRepositoryFile("F1Telemetry.App", "Styles", "SharedStyles.xaml"));
        var style = document.Descendants()
            .First(element =>
                element.Name.LocalName == "Style"
                && element.Attributes().Any(attribute => attribute.Name.LocalName == "Key" && attribute.Value == "SectionTitleTextStyle"));
        var foregroundSetter = style.Elements()
            .First(element =>
                element.Name.LocalName == "Setter"
                && element.Attribute("Property")?.Value == "Foreground");

        Assert.Equal("{StaticResource FgPrimaryBrush}", foregroundSetter.Attribute("Value")?.Value);
    }

    /// <summary>
    /// Verifies the shared combo box chrome renders strings and DisplayMemberPath selections without object-name fallbacks.
    /// </summary>
    [Fact]
    public void SharedStyles_DarkComboBoxStyle_UsesSelectionBoxItemAndDefaultPopupInteraction()
    {
        var xaml = File.ReadAllText(FindRepositoryFile("F1Telemetry.App", "Styles", "SharedStyles.xaml"));

        Assert.Contains("x:Key=\"DarkComboBoxStyle\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectionBoxItem", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectionBoxItemTemplate", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectionBoxItemStringFormat", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectedItem.DisplayName", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ComboBoxPopupBehavior", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("PreviewMouseWheel", xaml, StringComparison.Ordinal);
        Assert.Contains("MaxDropDownHeight\" Value=\"240\"", xaml, StringComparison.Ordinal);
        Assert.Contains("TextTrimming\" Value=\"CharacterEllipsis\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ToolTip\" Value=\"{Binding Text, RelativeSource={RelativeSource Self}}\"", xaml, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies long combo box lists can scroll internally without an auto-close behavior taking over.
    /// </summary>
    [Fact]
    public void DarkComboBox_LongDropDown_StaysOpenWhenInternalListScrolls()
    {
        RunOnStaThread(() =>
        {
            var comboBox = new ComboBox
            {
                ItemsSource = Enumerable.Range(1, 80).Select(index => $"Microphone device {index}").ToArray(),
                SelectedIndex = 0,
                Style = (Style)Application.Current.FindResource("DarkComboBoxStyle")
            };
            var host = CreateOffscreenHost(comboBox);

            try
            {
                host.Show();
                host.UpdateLayout();
                comboBox.ApplyTemplate();
                comboBox.IsDropDownOpen = true;
                comboBox.UpdateLayout();

                var popup = Assert.IsType<Popup>(comboBox.Template.FindName("PART_Popup", comboBox));
                Assert.NotNull(popup.Child);
                var scrollViewer = Assert.Single(FindDescendants<ScrollViewer>(popup.Child!));
                scrollViewer.ScrollToEnd();
                scrollViewer.UpdateLayout();

                Assert.True(comboBox.IsDropDownOpen);
                comboBox.SelectedItem = "Microphone device 64";
                Assert.Equal("Microphone device 64", comboBox.SelectedItem);
            }
            finally
            {
                host.Close();
            }
        });
    }

    /// <summary>
    /// Verifies DisplayMemberPath selections render their user-facing label in the shared combo box chrome.
    /// </summary>
    [Fact]
    public void DarkComboBox_DisplayMemberPathSelection_RendersDisplayName()
    {
        RunOnStaThread(() =>
        {
            var comboBox = new ComboBox
            {
                DisplayMemberPath = "DisplayName",
                ItemsSource = new[]
                {
                    new PostRaceAiCompletionModeOptionViewModel
                    {
                        Mode = PostRaceAiCompletionMode.Auto,
                        DisplayName = "自动完成",
                        Description = "等待数据完整后生成。"
                    }
                },
                SelectedIndex = 0,
                Style = (Style)Application.Current.FindResource("DarkComboBoxStyle")
            };
            var host = CreateOffscreenHost(comboBox);

            try
            {
                host.Show();
                host.UpdateLayout();
                comboBox.ApplyTemplate();
                comboBox.UpdateLayout();

                var selectedText = FindDescendants<TextBlock>(comboBox)
                    .Select(textBlock => textBlock.Text)
                    .Where(text => !string.IsNullOrWhiteSpace(text))
                    .ToArray();

                Assert.Contains("自动完成", selectedText);
                Assert.DoesNotContain(
                    selectedText,
                    text => text.Contains(nameof(PostRaceAiCompletionModeOptionViewModel), StringComparison.Ordinal));
            }
            finally
            {
                host.Close();
            }
        });
    }

    /// <summary>
    /// Verifies the session comparison track filter shows user-facing names while preserving selection binding.
    /// </summary>
    [Fact]
    public void SessionComparisonView_TrackFilterComboBox_UsesDisplayNameWithoutChangingSelectionBinding()
    {
        var document = XDocument.Load(FindRepositoryFile("F1Telemetry.App", "Views", "SessionComparisonView.xaml"));
        var comboBox = document.Descendants()
            .First(element =>
                element.Name.LocalName == "ComboBox"
                && element.Attribute("ItemsSource")?.Value == "{Binding SessionComparison.TrackFilters}");

        Assert.Equal("DisplayName", comboBox.Attribute("DisplayMemberPath")?.Value);
        Assert.Equal("{Binding SessionComparison.SelectedTrackFilter, Mode=TwoWay}", comboBox.Attribute("SelectedItem")?.Value);
    }

    /// <summary>
    /// Verifies the corner analysis selectors keep their display contracts without changing selection bindings.
    /// </summary>
    [Fact]
    public void CornerAnalysisView_FilterComboBoxes_UseSharedDarkStyleAndPreserveSelectionBindings()
    {
        var document = XDocument.Load(FindRepositoryFile("F1Telemetry.App", "Views", "CornerAnalysisView.xaml"));
        var sessionComboBox = document.Descendants()
            .First(element =>
                element.Name.LocalName == "ComboBox"
                && element.Attribute("ItemsSource")?.Value == "{Binding CornerAnalysis.HistoryBrowser.HistorySessions}");
        var lapComboBox = document.Descendants()
            .First(element =>
                element.Name.LocalName == "ComboBox"
                && element.Attribute("SelectedItem")?.Value == "{Binding CornerAnalysis.SelectedLap, Mode=TwoWay}");
        var referenceComboBox = document.Descendants()
            .First(element =>
                element.Name.LocalName == "ComboBox"
                && element.Attribute("SelectedItem")?.Value == "{Binding CornerAnalysis.SelectedReferenceLap, Mode=TwoWay}");

        Assert.Equal("{StaticResource DarkComboBoxStyle}", sessionComboBox.Attribute("Style")?.Value);
        Assert.Equal("SummaryText", sessionComboBox.Attribute("DisplayMemberPath")?.Value);
        Assert.Equal("{Binding CornerAnalysis.HistoryBrowser.SelectedSession, Mode=TwoWay}", sessionComboBox.Attribute("SelectedItem")?.Value);

        Assert.Equal("{StaticResource DarkComboBoxStyle}", lapComboBox.Attribute("Style")?.Value);
        Assert.Equal("LapText", lapComboBox.Attribute("DisplayMemberPath")?.Value);
        Assert.Equal("{Binding CornerAnalysis.SelectedLap, Mode=TwoWay}", lapComboBox.Attribute("SelectedItem")?.Value);

        Assert.Equal("{StaticResource CornerAnalysisReferencePickerComboBoxStyle}", referenceComboBox.Attribute("Style")?.Value);
        Assert.Equal("LapText", referenceComboBox.Attribute("DisplayMemberPath")?.Value);
        Assert.Equal("True", referenceComboBox.Attribute("IsEditable")?.Value);
        Assert.Equal("True", referenceComboBox.Attribute("IsReadOnly")?.Value);
        Assert.Equal("{Binding CornerAnalysis.ReferencePickerText, Mode=OneWay}", referenceComboBox.Attribute("Text")?.Value);
        Assert.Equal("{Binding CornerAnalysis.HasReferenceLapChoices}", referenceComboBox.Attribute("IsEnabled")?.Value);

        var xaml = document.ToString(SaveOptions.DisableFormatting);
        Assert.DoesNotContain("Style=\"{StaticResource DarkComboBoxStyle}\" DisplayMemberPath=\"LapText\" IsEditable=\"True\"", xaml, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies the corner analysis page uses the same glass card and section header language as the baseline pages.
    /// </summary>
    [Fact]
    public void CornerAnalysisView_UsesBaselineGlassCardsAndSectionHeaders()
    {
        var xaml = File.ReadAllText(FindRepositoryFile("F1Telemetry.App", "Views", "CornerAnalysisView.xaml"));
        var document = XDocument.Parse(xaml);

        Assert.Equal("{StaticResource GlassCardStyle}", FindNamedElement(document, "CornerAnalysisHeader").Attribute("Style")?.Value);
        Assert.Equal("{StaticResource GlassCardStyle}", FindNamedElement(document, "CornerAnalysisFilterBar").Attribute("Style")?.Value);
        Assert.Equal("{StaticResource GlassCardStyle}", FindNamedElement(document, "CornerAnalysisListPanel").Attribute("Style")?.Value);
        Assert.Equal("{StaticResource GlassCardStyle}", FindNamedElement(document, "CornerAnalysisDetailPanel").Attribute("Style")?.Value);
        Assert.Equal("{StaticResource GlassCardStyle}", FindNamedElement(document, "CornerAnalysisTrackMapPanel").Attribute("Style")?.Value);
        Assert.Equal("{StaticResource GlassCardStyle}", FindNamedElement(document, "CornerAnalysisVisualEvidencePanel").Attribute("Style")?.Value);
        Assert.Equal("{StaticResource GlassCardStyle}", FindNamedElement(document, "CornerAnalysisEngineerAdvicePanel").Attribute("Style")?.Value);
        Assert.Equal("{StaticResource GlassCardStyle}", FindNamedElement(document, "CornerAnalysisNotesPanel").Attribute("Style")?.Value);

        Assert.Contains("Style=\"{StaticResource SectionHeaderStyle}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Style=\"{StaticResource IconBadgeStyle}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Style=\"{StaticResource IconTextStyle}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Style=\"{StaticResource SectionTitleTextStyle}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Style=\"{StaticResource PanelStyle}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Style=\"{StaticResource CardStyle}\"", xaml, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies Settings voice AI dropdowns show display names while preserving existing selection paths.
    /// </summary>
    [Fact]
    public void SettingsView_VoiceAiComboBoxes_UseDisplayNameWithoutChangingSelectionBindings()
    {
        var document = XDocument.Load(FindRepositoryFile("F1Telemetry.App", "Views", "SettingsView.xaml"));
        var talkModeComboBox = document.Descendants()
            .First(element =>
                element.Name.LocalName == "ComboBox"
                && element.Attribute("ItemsSource")?.Value == "{Binding VoiceAiTalkModeOptions}");
        var microphoneComboBox = document.Descendants()
            .First(element =>
                element.Name.LocalName == "ComboBox"
                && element.Attribute("ItemsSource")?.Value == "{Binding VoiceAiMicrophoneDevices}");

        Assert.Equal("DisplayName", talkModeComboBox.Attribute("DisplayMemberPath")?.Value);
        Assert.Equal(
            "{Binding SelectedVoiceAiTalkModeOption, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}",
            talkModeComboBox.Attribute("SelectedItem")?.Value);
        Assert.Equal("DisplayName", microphoneComboBox.Attribute("DisplayMemberPath")?.Value);
        Assert.Equal("{Binding VoiceAiMicrophoneDeviceId, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}", microphoneComboBox.Attribute("SelectedValue")?.Value);
        Assert.Equal("DeviceId", microphoneComboBox.Attribute("SelectedValuePath")?.Value);
    }

    /// <summary>
    /// Verifies the polished Settings page keeps binding contracts while adding dense layout anchors.
    /// </summary>
    [Fact]
    public void SettingsView_DefinesPolishedLayoutContracts()
    {
        var document = XDocument.Load(FindRepositoryFile("F1Telemetry.App", "Views", "SettingsView.xaml"));
        var scrollViewer = FindElementByName(document, "SettingsScrollViewer");

        Assert.Equal("Disabled", scrollViewer.Attribute("HorizontalScrollBarVisibility")?.Value);
        Assert.Equal("Auto", scrollViewer.Attribute("VerticalScrollBarVisibility")?.Value);

        foreach (var sectionName in new[]
                 {
                     "SettingsSummarySection",
                     "RuntimeLogSection",
                     "UdpRawLogSection",
                     "VoiceAiSection",
                     "VersionUpdateSection"
                 })
        {
            Assert.Equal("Border", FindElementByName(document, sectionName).Name.LocalName);
        }

        foreach (var textBlockName in new[]
                 {
                     "AppLogDirectoryTextBlock",
                     "AppLogLastFilePathTextBlock",
                     "RaceAssistantLogDirectoryTextBlock",
                     "RaceAssistantLogLastFilePathTextBlock",
                     "UdpRawLogDirectoryTextBlock",
                     "UdpRawLogLastFilePathTextBlock"
                 })
        {
            var textBlock = FindElementByName(document, textBlockName);

            Assert.Equal("CharacterEllipsis", textBlock.Attribute("TextTrimming")?.Value);
            Assert.Equal("NoWrap", textBlock.Attribute("TextWrapping")?.Value);
            Assert.Equal(textBlock.Attribute("Text")?.Value, textBlock.Attribute("ToolTip")?.Value);
        }

        var talkModeComboBox = FindElementByName(document, "VoiceAiTalkModeComboBox");
        Assert.Equal("{StaticResource DarkComboBoxStyle}", talkModeComboBox.Attribute("Style")?.Value);
        Assert.Equal("240", talkModeComboBox.Attribute("MaxDropDownHeight")?.Value);
        Assert.Equal("DisplayName", talkModeComboBox.Attribute("DisplayMemberPath")?.Value);
        Assert.Equal(
            "{Binding SelectedVoiceAiTalkModeOption, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}",
            talkModeComboBox.Attribute("SelectedItem")?.Value);

        var microphoneComboBox = FindElementByName(document, "VoiceAiMicrophoneComboBox");
        Assert.Equal("{StaticResource DarkComboBoxStyle}", microphoneComboBox.Attribute("Style")?.Value);
        Assert.Equal("240", microphoneComboBox.Attribute("MaxDropDownHeight")?.Value);
        Assert.Equal("DisplayName", microphoneComboBox.Attribute("DisplayMemberPath")?.Value);
        Assert.Equal(
            "{Binding VoiceAiMicrophoneDeviceId, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}",
            microphoneComboBox.Attribute("SelectedValue")?.Value);
        Assert.Equal("DeviceId", microphoneComboBox.Attribute("SelectedValuePath")?.Value);

        var maxSizeTextBox = FindElementByName(document, "MaxLogFileSizeMbTextBox");
        Assert.Equal(
            "{Binding MaxLogFileSizeMbText, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}",
            maxSizeTextBox.Attribute("Text")?.Value);
        var retentionTextBox = FindElementByName(document, "MaxLogRetentionDaysTextBox");
        Assert.Equal(
            "{Binding MaxLogRetentionDaysText, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}",
            retentionTextBox.Attribute("Text")?.Value);
    }

    /// <summary>
    /// Verifies the settings page lays out the voice AI status rows without visual overlap.
    /// </summary>
    [Fact]
    public void SettingsView_VisualLayout_DoesNotOverlapVoiceAiStatusRows()
    {
        RunOnStaThread(() =>
        {
            var view = new SettingsView();
            var host = CreateOffscreenHost(view);

            try
            {
                host.Show();
                host.UpdateLayout();
                view.UpdateLayout();

                var inputLevel = FindTextBlock(view, "输入电平");
                var runStatus = FindTextBlock(view, "运行状态");
                var recognitionStatus = FindTextBlock(view, "识别状态");
                var inputBounds = GetBounds(inputLevel, view);
                var runBounds = GetBounds(runStatus, view);
                var recognitionBounds = GetBounds(recognitionStatus, view);

                Assert.True(inputBounds.Bottom <= runBounds.Top);
                Assert.True(runBounds.Bottom <= recognitionBounds.Top);
            }
            finally
            {
                host.Close();
            }
        });
    }

    /// <summary>
    /// Verifies shared section titles render with the readable foreground in real WPF views.
    /// </summary>
    [Theory]
    [InlineData(typeof(OverviewView), "玩家状态")]
    [InlineData(typeof(ChartsView), "AI 状态")]
    public void SharedSectionTitles_VisualForeground_IsReadable(Type viewType, string titleText)
    {
        RunOnStaThread(() =>
        {
            var view = Assert.IsAssignableFrom<FrameworkElement>(Activator.CreateInstance(viewType));
            var host = CreateOffscreenHost(view);

            try
            {
                host.Show();
                host.UpdateLayout();
                view.UpdateLayout();

                var title = FindTextBlock(view, titleText);
                var expectedBrush = Assert.IsType<SolidColorBrush>(Application.Current.FindResource("FgPrimaryBrush"));
                var actualBrush = Assert.IsType<SolidColorBrush>(title.Foreground);

                Assert.Equal(expectedBrush.Color, actualBrush.Color);
            }
            finally
            {
                host.Close();
            }
        });
    }

    private static void AssertBindingPath(DependencyObject target, DependencyProperty property, string expectedPath)
    {
        var binding = BindingOperations.GetBinding(target, property);
        Assert.NotNull(binding);
        Assert.Equal(expectedPath, binding.Path.Path);
    }

    private static Window CreateOffscreenHost(FrameworkElement content)
    {
        return new Window
        {
            Content = content,
            Height = 1000,
            Left = -20000,
            ShowActivated = false,
            ShowInTaskbar = false,
            Top = -20000,
            Width = 1800,
            WindowStyle = WindowStyle.None
        };
    }

    private static TextBlock FindTextBlock(DependencyObject root, string text)
    {
        return FindDescendants<TextBlock>(root).First(textBlock => textBlock.Text == text);
    }

    private static XElement FindElementByName(XContainer document, string name)
    {
        return document.Descendants()
            .First(element => element.Attributes().Any(attribute => attribute.Name.LocalName == "Name" && attribute.Value == name));
    }

    private static Rect GetBounds(FrameworkElement element, Visual ancestor)
    {
        var transform = element.TransformToAncestor(ancestor);
        return transform.TransformBounds(new Rect(element.RenderSize));
    }

    private static IReadOnlyList<T> FindDescendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        var matches = new List<T>();
        CollectDescendants(root, matches);
        return matches;
    }

    private static void CollectDescendants<T>(DependencyObject root, ICollection<T> matches)
        where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T typedChild)
            {
                matches.Add(typedChild);
            }

            CollectDescendants(child, matches);
        }
    }

    private static void RunOnStaThread(Action action)
    {
        WpfApplicationHelper.RunOnStaThread(action);
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

    private static XElement FindNamedElement(XDocument document, string name)
    {
        return document.Descendants()
            .First(element => element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == name);
    }

    private sealed class PasswordBindingSource : INotifyPropertyChanged
    {
        private string _aiApiKey = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string AiApiKey
        {
            get => _aiApiKey;
            set
            {
                _aiApiKey = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AiApiKey)));
            }
        }
    }

    private sealed class VoiceSelectionBindingSource
    {
        public IReadOnlyList<string> AvailableVoices { get; } = new[] { "Microsoft David Desktop" };

        public string TtsVoiceName { get; set; } = "Microsoft David Desktop";

        public string TtsVoiceStatusText { get; } = "1 个可用语音";
    }
}
