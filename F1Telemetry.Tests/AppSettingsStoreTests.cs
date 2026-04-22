using System.IO;
using System.Text.Json;
using F1Telemetry.AI.Interfaces;
using F1Telemetry.AI.Models;
using F1Telemetry.AI.Services;
using F1Telemetry.TTS.Models;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies unified AI and TTS settings persistence behavior.
/// </summary>
public sealed class AppSettingsStoreTests
{
    /// <summary>
    /// Verifies that a missing settings file returns defaults for both blocks.
    /// </summary>
    [Fact]
    public async Task LoadAsync_MissingFile_ReturnsDefaultsForAiAndTts()
    {
        var root = CreateRootPath();
        IAppSettingsStore store = new AppSettingsStore(root);

        var settings = await store.LoadAsync();

        Assert.Equal("https://api.deepseek.com", settings.Ai.BaseUrl);
        Assert.Equal("deepseek-chat", settings.Ai.Model);
        Assert.False(settings.Ai.AiEnabled);
        Assert.Equal(10, settings.Ai.RequestTimeoutSeconds);
        Assert.False(settings.Tts.TtsEnabled);
        Assert.Equal(string.Empty, settings.Tts.VoiceName);
        Assert.Equal(100, settings.Tts.Volume);
        Assert.Equal(0, settings.Tts.Rate);
        Assert.Equal(8, settings.Tts.CooldownSeconds);
    }

    /// <summary>
    /// Verifies that a missing TTS block falls back to TTS defaults without losing AI settings.
    /// </summary>
    [Fact]
    public async Task LoadAsync_MissingTtsBlock_PreservesAiAndFallsBackTtsDefaults()
    {
        var root = CreateRootPath();
        Directory.CreateDirectory(Path.Combine(root, "F1Telemetry"));

        await File.WriteAllTextAsync(
            Path.Combine(root, "F1Telemetry", "settings.json"),
            """
            {
              "ai": {
                "apiKey": "configured",
                "baseUrl": "https://example.com/api",
                "model": "deepseek-chat",
                "enabled": true,
                "requestTimeoutSeconds": 18
              }
            }
            """);

        IAppSettingsStore store = new AppSettingsStore(root);

        var settings = await store.LoadAsync();

        Assert.Equal("configured", settings.Ai.ApiKey);
        Assert.Equal("https://example.com/api", settings.Ai.BaseUrl);
        Assert.True(settings.Ai.AiEnabled);
        Assert.Equal(18, settings.Ai.RequestTimeoutSeconds);
        Assert.False(settings.Tts.TtsEnabled);
        Assert.Equal(8, settings.Tts.CooldownSeconds);
    }

    /// <summary>
    /// Verifies that legacy root-level AI settings remain readable after the unified settings file was introduced.
    /// </summary>
    [Fact]
    public async Task LoadAsync_LegacyAiRoot_PreservesLegacyFields()
    {
        var root = CreateRootPath();
        Directory.CreateDirectory(Path.Combine(root, "F1Telemetry"));

        await File.WriteAllTextAsync(
            Path.Combine(root, "F1Telemetry", "settings.json"),
            """
            {
              "apiKey": "legacy-key",
              "baseUrl": "https://legacy.example.com",
              "model": "deepseek-chat",
              "aiEnabled": true,
              "requestTimeoutSeconds": 22
            }
            """);

        IAppSettingsStore store = new AppSettingsStore(root);

        var settings = await store.LoadAsync();

        Assert.Equal("legacy-key", settings.Ai.ApiKey);
        Assert.Equal("https://legacy.example.com", settings.Ai.BaseUrl);
        Assert.True(settings.Ai.AiEnabled);
        Assert.Equal(22, settings.Ai.RequestTimeoutSeconds);
        Assert.False(settings.Tts.TtsEnabled);
    }

    /// <summary>
    /// Verifies that saving the AI block preserves the current TTS block and writes the new JSON property names.
    /// </summary>
    [Fact]
    public async Task SaveAiSettingsAsync_PreservesExistingTtsBlockAndWritesUnifiedShape()
    {
        var root = CreateRootPath();
        Directory.CreateDirectory(Path.Combine(root, "F1Telemetry"));

        await File.WriteAllTextAsync(
            Path.Combine(root, "F1Telemetry", "settings.json"),
            """
            {
              "ai": {
                "apiKey": "",
                "baseUrl": "https://api.deepseek.com",
                "model": "deepseek-chat",
                "enabled": false,
                "requestTimeoutSeconds": 10
              },
              "tts": {
                "enabled": true,
                "voiceName": "Voice A",
                "volume": 80,
                "rate": 1,
                "cooldownSeconds": 9
              }
            }
            """);

        IAppSettingsStore store = new AppSettingsStore(root);

        await store.SaveAiSettingsAsync(
            new AISettings
            {
                ApiKey = "configured",
                BaseUrl = "https://example.com/api",
                Model = "deepseek-chat",
                AiEnabled = true,
                RequestTimeoutSeconds = 12
            });

        var persisted = await store.LoadAsync();
        using var json = await ReadPersistedJsonAsync(root);

        Assert.Equal("configured", persisted.Ai.ApiKey);
        Assert.Equal("https://example.com/api", persisted.Ai.BaseUrl);
        Assert.True(persisted.Ai.AiEnabled);
        Assert.True(persisted.Tts.TtsEnabled);
        Assert.Equal("Voice A", persisted.Tts.VoiceName);
        Assert.Equal(80, persisted.Tts.Volume);
        Assert.Equal(1, persisted.Tts.Rate);
        Assert.Equal(9, persisted.Tts.CooldownSeconds);
        Assert.True(json.RootElement.GetProperty("ai").GetProperty("enabled").GetBoolean());
        Assert.True(json.RootElement.GetProperty("tts").GetProperty("enabled").GetBoolean());
    }

    /// <summary>
    /// Verifies that saving the TTS block preserves the current AI block.
    /// </summary>
    [Fact]
    public async Task SaveTtsSettingsAsync_PreservesExistingAiBlock()
    {
        var root = CreateRootPath();
        Directory.CreateDirectory(Path.Combine(root, "F1Telemetry"));

        await File.WriteAllTextAsync(
            Path.Combine(root, "F1Telemetry", "settings.json"),
            """
            {
              "ai": {
                "apiKey": "configured",
                "baseUrl": "https://example.com/api",
                "model": "deepseek-chat",
                "enabled": true,
                "requestTimeoutSeconds": 18
              }
            }
            """);

        IAppSettingsStore store = new AppSettingsStore(root);

        await store.SaveTtsSettingsAsync(
            new TtsOptions
            {
                TtsEnabled = true,
                VoiceName = "Voice B",
                Volume = 72,
                Rate = -1,
                CooldownSeconds = 12
            });

        var persisted = await store.LoadAsync();

        Assert.Equal("configured", persisted.Ai.ApiKey);
        Assert.Equal("https://example.com/api", persisted.Ai.BaseUrl);
        Assert.True(persisted.Ai.AiEnabled);
        Assert.True(persisted.Tts.TtsEnabled);
        Assert.Equal("Voice B", persisted.Tts.VoiceName);
        Assert.Equal(72, persisted.Tts.Volume);
        Assert.Equal(-1, persisted.Tts.Rate);
        Assert.Equal(12, persisted.Tts.CooldownSeconds);
    }

    /// <summary>
    /// Verifies API key is read from a plain-text legacy file as before.
    /// </summary>
    [Fact]
    public async Task ReadLegacyPlainTextApiKey_WhenNotProtected()
    {
        var root = CreateRootPath();
        Directory.CreateDirectory(Path.Combine(root, "F1Telemetry"));

        await File.WriteAllTextAsync(
            Path.Combine(root, "F1Telemetry", "settings.json"),
            """
            {
              "ai": {
                "apiKey": "legacy-plain",
                "baseUrl": "https://api.deepseek.com",
                "model": "deepseek-chat",
                "enabled": true,
                "requestTimeoutSeconds": 10
              }
            }
            """);

        IAppSettingsStore store = new AppSettingsStore(root);

        var settings = await store.LoadAsync();

        Assert.Equal("legacy-plain", settings.Ai.ApiKey);
    }

    /// <summary>
    /// Verifies saved API key can be loaded back for the same platform and degrades safely on legacy values.
    /// </summary>
    [Fact]
    public async Task SaveAiSettingsAsync_SupportsSecureStorageAndLegacyFallback()
    {
        var root = CreateRootPath();
        Directory.CreateDirectory(Path.Combine(root, "F1Telemetry"));

        var store = new AppSettingsStore(root);
        await store.SaveAiSettingsAsync(
            new AISettings
            {
                ApiKey = "configured-secret-key",
                BaseUrl = "https://example.com/api",
                Model = "deepseek-chat",
                AiEnabled = true,
                RequestTimeoutSeconds = 10
            });

        var raw = await File.ReadAllTextAsync(Path.Combine(root, "F1Telemetry", "settings.json"));
        using var json = JsonDocument.Parse(raw);
        var persistedApiKey = json.RootElement.GetProperty("ai").GetProperty("apiKey").GetString();
        var loaded = await store.LoadAsync();

        Assert.Equal("configured-secret-key", loaded.Ai.ApiKey);
        Assert.NotNull(persistedApiKey);

        if (OperatingSystem.IsWindows())
        {
            Assert.NotEqual("configured-secret-key", persistedApiKey);
        }

        var legacyRoot = Path.Combine(root, "legacy");
        Directory.CreateDirectory(Path.Combine(legacyRoot, "F1Telemetry"));
        var legacySettingsPath = Path.Combine(legacyRoot, "F1Telemetry", "settings.json");
        await File.WriteAllTextAsync(
            legacySettingsPath,
            """
            {
              "apiKey": "legacy-recover",
              "baseUrl": "https://api.deepseek.com",
              "model": "deepseek-chat",
              "enabled": true,
              "requestTimeoutSeconds": 10
            }
            """);

        var legacyStore = new AppSettingsStore(legacyRoot);
        var legacySettings = await legacyStore.LoadAsync();
        Assert.Equal("legacy-recover", legacySettings.Ai.ApiKey);
    }

    private static string CreateRootPath()
    {
        return Path.Combine(Path.GetTempPath(), "F1TelemetryTests", Guid.NewGuid().ToString("N"));
    }

    private static async Task<JsonDocument> ReadPersistedJsonAsync(string root)
    {
        await using var stream = File.OpenRead(Path.Combine(root, "F1Telemetry", "settings.json"));
        return await JsonDocument.ParseAsync(stream);
    }
}
