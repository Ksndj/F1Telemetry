using System.Runtime.Versioning;
using System.Speech.Synthesis;

namespace F1Telemetry.TTS.Services;

/// <summary>
/// Describes Windows speech voices that can be selected by the UI.
/// </summary>
/// <param name="VoiceNames">Installed voice names available for selection.</param>
/// <param name="DefaultVoiceName">The Windows default voice name when it can be detected.</param>
/// <param name="StatusMessage">A short user-visible status message for voice discovery.</param>
public sealed record WindowsVoiceCatalogResult(
    IReadOnlyList<string> VoiceNames,
    string DefaultVoiceName,
    string StatusMessage);

/// <summary>
/// Loads installed Windows speech voices without allowing discovery failures to crash the UI.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsVoiceCatalog
{
    private const string NoVoicesStatus =
        "\u672a\u53d1\u73b0\u53ef\u7528\u8bed\u97f3\uff0c\u8bf7\u68c0\u67e5 Windows \u8bed\u97f3\u8bbe\u7f6e\u3002";

    private readonly Func<WindowsVoiceCatalogResult> _loadVoicesCore;

    /// <summary>
    /// Initializes a new Windows voice catalog that reads from <see cref="SpeechSynthesizer"/>.
    /// </summary>
    public WindowsVoiceCatalog()
        : this(LoadInstalledVoices)
    {
    }

    /// <summary>
    /// Initializes a new Windows voice catalog with a testable loader.
    /// </summary>
    /// <param name="loadVoicesCore">The loader used to read available voices.</param>
    public WindowsVoiceCatalog(Func<WindowsVoiceCatalogResult> loadVoicesCore)
    {
        _loadVoicesCore = loadVoicesCore ?? throw new ArgumentNullException(nameof(loadVoicesCore));
    }

    /// <summary>
    /// Loads installed voice names and returns an empty result with a status message when discovery fails.
    /// </summary>
    public WindowsVoiceCatalogResult LoadVoices()
    {
        try
        {
            var result = _loadVoicesCore();
            var voiceNames = result.VoiceNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (voiceNames.Length == 0)
            {
                return new WindowsVoiceCatalogResult(Array.Empty<string>(), string.Empty, NoVoicesStatus);
            }

            var defaultVoiceName = voiceNames.Contains(result.DefaultVoiceName, StringComparer.Ordinal)
                ? result.DefaultVoiceName
                : string.Empty;
            var statusMessage = string.IsNullOrWhiteSpace(result.StatusMessage)
                ? $"\u5df2\u52a0\u8f7d {voiceNames.Length} \u4e2a Windows \u8bed\u97f3\u3002"
                : result.StatusMessage;

            return new WindowsVoiceCatalogResult(voiceNames, defaultVoiceName, statusMessage);
        }
        catch (Exception ex)
        {
            return new WindowsVoiceCatalogResult(
                Array.Empty<string>(),
                string.Empty,
                $"\u672a\u53d1\u73b0\u53ef\u7528\u8bed\u97f3\uff1a{ex.Message}");
        }
    }

    private static WindowsVoiceCatalogResult LoadInstalledVoices()
    {
        using var speechSynthesizer = new SpeechSynthesizer();
        var voiceNames = speechSynthesizer
            .GetInstalledVoices()
            .Where(voice => voice.Enabled)
            .Select(voice => voice.VoiceInfo.Name)
            .ToArray();
        var defaultVoiceName = speechSynthesizer.Voice?.Name ?? string.Empty;

        return new WindowsVoiceCatalogResult(voiceNames, defaultVoiceName, string.Empty);
    }
}
