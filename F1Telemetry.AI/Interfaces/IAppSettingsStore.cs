using F1Telemetry.AI.Models;
using F1Telemetry.Core.Models;
using F1Telemetry.TTS.Models;

namespace F1Telemetry.AI.Interfaces;

/// <summary>
/// Persists application settings blocks that are shared by runtime features.
/// </summary>
public interface IAppSettingsStore
{
    /// <summary>
    /// Loads the current application settings document.
    /// </summary>
    Task<AppSettingsDocument> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the AI settings block while preserving the TTS block.
    /// </summary>
    Task SaveAiSettingsAsync(AISettings settings, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the TTS settings block while preserving the AI block.
    /// </summary>
    Task SaveTtsSettingsAsync(TtsOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the raw UDP log settings block while preserving other blocks.
    /// </summary>
    Task SaveUdpRawLogOptionsAsync(UdpRawLogOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the UDP listener settings block while preserving other blocks.
    /// </summary>
    Task SaveUdpSettingsAsync(UdpSettings settings, CancellationToken cancellationToken = default);
}
