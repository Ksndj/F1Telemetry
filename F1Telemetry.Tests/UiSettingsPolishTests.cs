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
    public void MainWindow_UdpPortTextBox_UsesTwoWayPropertyChangedBinding()
    {
        var document = XDocument.Load(FindRepositoryFile("F1Telemetry.App", "MainWindow.xaml"));
        var textBoxes = document.Descendants()
            .Where(element => element.Name.LocalName == "TextBox")
            .Select(element => element.Attribute("Text")?.Value)
            .Where(value => value is not null)
            .ToArray();

        Assert.Contains(
            "{Binding PortText, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}",
            textBoxes);
    }

    private static void AssertBindingPath(DependencyObject target, DependencyProperty property, string expectedPath)
    {
        var binding = BindingOperations.GetBinding(target, property);
        Assert.NotNull(binding);
        Assert.Equal(expectedPath, binding.Path.Path);
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
        Exception? capturedException = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                capturedException = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (capturedException is not null)
        {
            throw capturedException;
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
