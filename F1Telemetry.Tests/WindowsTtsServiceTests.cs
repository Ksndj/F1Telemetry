using F1Telemetry.TTS.Services;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies Windows TTS prompt construction before playback reaches the OS speech engine.
/// </summary>
public sealed class WindowsTtsServiceTests
{
    /// <summary>
    /// Verifies that spoken prompts request the loudest Windows speech volume style.
    /// </summary>
    [Fact]
    public void BuildSpeechPrompt_UsesExtraLoudPromptVolume()
    {
        var prompt = WindowsTtsService.BuildSpeechPrompt("节奏保持");
        var xml = prompt.ToXml();

        Assert.Contains("节奏保持", xml, StringComparison.Ordinal);
        Assert.Contains("volume", xml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("x-loud", xml, StringComparison.OrdinalIgnoreCase);
    }
}
