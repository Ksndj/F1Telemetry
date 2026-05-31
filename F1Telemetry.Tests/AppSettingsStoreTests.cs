using System.IO;
using System.Text.Json;
using F1Telemetry.AI.Interfaces;
using F1Telemetry.AI.Models;
using F1Telemetry.AI.Services;
using F1Telemetry.Core.Models;
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
        Assert.Equal(RaceWeekendTyrePlan.DefaultInventoryText, settings.RaceWeekendTyrePlan.InventoryText);
        Assert.Equal(RaceWeekendTyrePlan.DefaultMaxRecommendedWearPercent, settings.RaceWeekendTyrePlan.MaxRecommendedWearPercent);
        Assert.False(settings.UdpRawLog.Enabled);
        Assert.Equal(string.Empty, settings.UdpRawLog.DirectoryPath);
        Assert.Equal(4096, settings.UdpRawLog.QueueCapacity);
        Assert.True(settings.Logs.EnableAppFileLog);
        Assert.True(settings.Logs.EnableRaceAssistantAuditLog);
        Assert.False(settings.Logs.RaceAssistantLogPromptSummary);
        Assert.Equal(20, settings.Logs.MaxLogFileSizeMB);
        Assert.Equal(14, settings.Logs.MaxLogRetentionDays);
        Assert.False(settings.VoiceAi.Enabled);
        Assert.Equal(VoiceAiInputBindingKind.None, settings.VoiceAi.InputBinding.Kind);
        Assert.Equal(VoiceAiTalkMode.HoldToTalk, settings.VoiceAi.TalkMode);
        Assert.Equal(string.Empty, settings.VoiceAi.MicrophoneDeviceId);
        Assert.Equal(string.Empty, settings.VoiceAi.MicrophoneDeviceName);
        Assert.Equal(VoiceAiOptions.NoHotkey, settings.VoiceAi.Hotkey);
        Assert.True(settings.VoiceAi.AudioSettings.EnableNoiseReduction);
        Assert.True(settings.VoiceAi.AudioSettings.EnableHighPassFilter);
        Assert.Equal(120d, settings.VoiceAi.AudioSettings.HighPassCutoffHz);
        Assert.True(settings.VoiceAi.AudioSettings.EnableNoiseGate);
        Assert.Equal(-40d, settings.VoiceAi.AudioSettings.NoiseGateThresholdDb);
        Assert.True(settings.VoiceAi.AudioSettings.EnableVad);
        Assert.Equal(150, settings.VoiceAi.AudioSettings.PreSpeechPaddingMs);
        Assert.Equal(250, settings.VoiceAi.AudioSettings.PostSpeechPaddingMs);
        Assert.True(settings.VoiceAi.AudioSettings.EnableAutoGain);
        Assert.Equal(8, settings.VoiceAi.AudioSettings.MaxRecordingSeconds);
        Assert.Equal(300, settings.VoiceAi.AudioSettings.MinSpeechDurationMs);
        Assert.Equal(0.35d, settings.VoiceAi.AudioSettings.MinRecognitionConfidence);
        Assert.False(settings.VoiceAssistantSettings.EnableVoiceAssistant);
        Assert.True(settings.VoiceAssistantSettings.EnableTtsAnswer);
        Assert.Equal(240, settings.VoiceAssistantSettings.MaxAnswerLength);
        Assert.Equal(12, settings.VoiceAssistantSettings.RepeatQuestionCooldownSeconds);
        Assert.Equal(20777, settings.Udp.ListenPort);
    }

    /// <summary>
    /// Verifies old settings files without a UDP block keep the default listen port.
    /// </summary>
    [Fact]
    public async Task LoadAsync_MissingUdpBlock_ReturnsDefaultUdpListenPort()
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

        Assert.Equal(20777, settings.Udp.ListenPort);
    }

    /// <summary>
    /// Verifies invalid persisted UDP ports are ignored in favor of the default.
    /// </summary>
    [Fact]
    public async Task LoadAsync_InvalidUdpListenPort_ReturnsDefaultUdpListenPort()
    {
        var root = CreateRootPath();
        Directory.CreateDirectory(Path.Combine(root, "F1Telemetry"));

        await File.WriteAllTextAsync(
            Path.Combine(root, "F1Telemetry", "settings.json"),
            """
            {
              "udp": {
                "listenPort": 70000
              }
            }
            """);

        IAppSettingsStore store = new AppSettingsStore(root);

        var settings = await store.LoadAsync();

        Assert.Equal(20777, settings.Udp.ListenPort);
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
    /// Verifies saving the race-weekend tyre plan preserves all other settings blocks.
    /// </summary>
    [Fact]
    public async Task SaveRaceWeekendTyrePlanAsync_PreservesExistingBlocksAndWritesPlan()
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
              },
              "tts": {
                "enabled": true,
                "voiceName": "Voice A",
                "volume": 80,
                "rate": 1,
                "cooldownSeconds": 9
              },
              "udp": {
                "listenPort": 20778
              }
            }
            """);

        IAppSettingsStore store = new AppSettingsStore(root);

        await store.SaveRaceWeekendTyrePlanAsync(
            new RaceWeekendTyrePlan
            {
                InventoryText = "Soft=1; Medium=2; Hard=1; Intermediate=1; Wet=1",
                MaxRecommendedWearPercent = 58
            });

        var persisted = await store.LoadAsync();
        using var json = await ReadPersistedJsonAsync(root);

        Assert.Equal("configured", persisted.Ai.ApiKey);
        Assert.True(persisted.Tts.TtsEnabled);
        Assert.Equal(20778, persisted.Udp.ListenPort);
        Assert.Equal(1, persisted.RaceWeekendTyrePlan.SoftCount);
        Assert.Equal(2, persisted.RaceWeekendTyrePlan.MediumCount);
        Assert.Equal(1, persisted.RaceWeekendTyrePlan.HardCount);
        Assert.Equal(1, persisted.RaceWeekendTyrePlan.IntermediateCount);
        Assert.Equal(1, persisted.RaceWeekendTyrePlan.WetCount);
        Assert.Equal("Soft=1; Medium=2; Hard=1; Intermediate=1; Wet=1", persisted.RaceWeekendTyrePlan.InventoryText);
        Assert.Equal(58, persisted.RaceWeekendTyrePlan.MaxRecommendedWearPercent);
        Assert.Equal(1, json.RootElement.GetProperty("raceWeekendTyrePlan").GetProperty("softCount").GetInt32());
        Assert.Equal(2, json.RootElement.GetProperty("raceWeekendTyrePlan").GetProperty("mediumCount").GetInt32());
        Assert.Equal(1, json.RootElement.GetProperty("raceWeekendTyrePlan").GetProperty("hardCount").GetInt32());
        Assert.Equal(1, json.RootElement.GetProperty("raceWeekendTyrePlan").GetProperty("intermediateCount").GetInt32());
        Assert.Equal(1, json.RootElement.GetProperty("raceWeekendTyrePlan").GetProperty("wetCount").GetInt32());
        Assert.Equal(
            "Soft=1; Medium=2; Hard=1; Intermediate=1; Wet=1",
            json.RootElement.GetProperty("raceWeekendTyrePlan").GetProperty("inventoryText").GetString());
        Assert.Equal(58, json.RootElement.GetProperty("raceWeekendTyrePlan").GetProperty("maxRecommendedWearPercent").GetInt32());
    }

    /// <summary>
    /// Verifies legacy tyre inventory text is migrated into structured counts on load.
    /// </summary>
    [Fact]
    public async Task LoadAsync_LegacyTyreInventoryText_MigratesToStructuredCounts()
    {
        var root = CreateRootPath();
        Directory.CreateDirectory(Path.Combine(root, "F1Telemetry"));

        await File.WriteAllTextAsync(
            Path.Combine(root, "F1Telemetry", "settings.json"),
            """
            {
              "raceWeekendTyrePlan": {
                "inventoryText": "Soft=1; Medium=2; Hard=3; Intermediate=4; Wet=5",
                "maxRecommendedWearPercent": 62
              }
            }
            """);

        IAppSettingsStore store = new AppSettingsStore(root);

        var settings = await store.LoadAsync();

        Assert.Equal(1, settings.RaceWeekendTyrePlan.SoftCount);
        Assert.Equal(2, settings.RaceWeekendTyrePlan.MediumCount);
        Assert.Equal(3, settings.RaceWeekendTyrePlan.HardCount);
        Assert.Equal(4, settings.RaceWeekendTyrePlan.IntermediateCount);
        Assert.Equal(5, settings.RaceWeekendTyrePlan.WetCount);
        Assert.Equal(62, settings.RaceWeekendTyrePlan.MaxRecommendedWearPercent);
    }

    /// <summary>
    /// Verifies structured tyre inventory survives a save/load round trip.
    /// </summary>
    [Fact]
    public async Task SaveRaceWeekendTyrePlanAsync_StructuredCounts_RoundTrips()
    {
        var root = CreateRootPath();
        IAppSettingsStore store = new AppSettingsStore(root);

        await store.SaveRaceWeekendTyrePlanAsync(
            new RaceWeekendTyrePlan
            {
                SoftCount = 4,
                MediumCount = 3,
                HardCount = 2,
                IntermediateCount = 1,
                WetCount = 0,
                MaxRecommendedWearPercent = 63
            });

        var settings = await store.LoadAsync();

        Assert.Equal(4, settings.RaceWeekendTyrePlan.SoftCount);
        Assert.Equal(3, settings.RaceWeekendTyrePlan.MediumCount);
        Assert.Equal(2, settings.RaceWeekendTyrePlan.HardCount);
        Assert.Equal(1, settings.RaceWeekendTyrePlan.IntermediateCount);
        Assert.Equal(0, settings.RaceWeekendTyrePlan.WetCount);
        Assert.Equal(63, settings.RaceWeekendTyrePlan.MaxRecommendedWearPercent);
    }

    /// <summary>
    /// Verifies saving raw UDP log settings preserves AI and TTS settings.
    /// </summary>
    [Fact]
    public async Task SaveUdpRawLogOptionsAsync_PreservesExistingAiAndTtsBlocks()
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

        await store.SaveUdpRawLogOptionsAsync(
            new UdpRawLogOptions
            {
                Enabled = true,
                DirectoryPath = "C:\\Logs\\Udp",
                QueueCapacity = 64
            });

        var persisted = await store.LoadAsync();
        using var json = await ReadPersistedJsonAsync(root);

        Assert.Equal("configured", persisted.Ai.ApiKey);
        Assert.True(persisted.Tts.TtsEnabled);
        Assert.True(persisted.UdpRawLog.Enabled);
        Assert.Equal("C:\\Logs\\Udp", persisted.UdpRawLog.DirectoryPath);
        Assert.Equal(64, persisted.UdpRawLog.QueueCapacity);
        Assert.True(json.RootElement.GetProperty("udpRawLog").GetProperty("enabled").GetBoolean());
        Assert.Equal("C:\\Logs\\Udp", json.RootElement.GetProperty("udpRawLog").GetProperty("directoryPath").GetString());
    }

    /// <summary>
    /// Verifies saving the UDP settings block writes the listen port and preserves existing settings.
    /// </summary>
    [Fact]
    public async Task SaveUdpSettingsAsync_PreservesExistingBlocksAndWritesListenPort()
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
              },
              "tts": {
                "enabled": true,
                "voiceName": "Voice A",
                "volume": 80,
                "rate": 1,
                "cooldownSeconds": 9
              },
              "udpRawLog": {
                "enabled": true,
                "directoryPath": "C:\\Logs\\Udp",
                "queueCapacity": 64
              }
            }
            """);

        IAppSettingsStore store = new AppSettingsStore(root);

        await store.SaveUdpSettingsAsync(new UdpSettings { ListenPort = 20778 });

        var persisted = await store.LoadAsync();
        using var json = await ReadPersistedJsonAsync(root);

        Assert.Equal(20778, persisted.Udp.ListenPort);
        Assert.Equal("configured", persisted.Ai.ApiKey);
        Assert.True(persisted.Tts.TtsEnabled);
        Assert.True(persisted.UdpRawLog.Enabled);
        Assert.Equal(20778, json.RootElement.GetProperty("udp").GetProperty("listenPort").GetInt32());
        Assert.True(json.RootElement.GetProperty("ai").GetProperty("enabled").GetBoolean());
        Assert.True(json.RootElement.GetProperty("tts").GetProperty("enabled").GetBoolean());
        Assert.True(json.RootElement.GetProperty("udpRawLog").GetProperty("enabled").GetBoolean());
    }

    /// <summary>
    /// Verifies saving runtime log settings preserves existing settings blocks.
    /// </summary>
    [Fact]
    public async Task SaveLogSettingsAsync_PreservesExistingBlocksAndWritesLogSettings()
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
              },
              "udpRawLog": {
                "enabled": true,
                "directoryPath": "C:\\Logs\\Udp",
                "queueCapacity": 64
              }
            }
            """);

        IAppSettingsStore store = new AppSettingsStore(root);

        await store.SaveLogSettingsAsync(
            new LogSettings
            {
                EnableAppFileLog = false,
                EnableRaceAssistantAuditLog = true,
                RaceAssistantLogPromptSummary = true,
                MaxLogFileSizeMB = 32,
                MaxLogRetentionDays = 21
            });

        var persisted = await store.LoadAsync();
        using var json = await ReadPersistedJsonAsync(root);

        Assert.Equal("configured", persisted.Ai.ApiKey);
        Assert.True(persisted.UdpRawLog.Enabled);
        Assert.False(persisted.Logs.EnableAppFileLog);
        Assert.True(persisted.Logs.EnableRaceAssistantAuditLog);
        Assert.True(persisted.Logs.RaceAssistantLogPromptSummary);
        Assert.Equal(32, persisted.Logs.MaxLogFileSizeMB);
        Assert.Equal(21, persisted.Logs.MaxLogRetentionDays);
        Assert.False(json.RootElement.GetProperty("logs").GetProperty("enableAppFileLog").GetBoolean());
        Assert.True(json.RootElement.GetProperty("logs").GetProperty("raceAssistantLogPromptSummary").GetBoolean());
    }

    /// <summary>
    /// Verifies saving voice AI options preserves existing settings blocks and writes the input binding.
    /// </summary>
    [Fact]
    public async Task SaveVoiceAiOptionsAsync_PreservesExistingBlocksAndWritesInputBinding()
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
              },
              "tts": {
                "enabled": true,
                "voiceName": "Voice A",
                "volume": 80,
                "rate": 1,
                "cooldownSeconds": 9
              },
              "udp": {
                "listenPort": 20778
              }
            }
            """);

        IAppSettingsStore store = new AppSettingsStore(root);

        await store.SaveVoiceAiOptionsAsync(
            new VoiceAiOptions
            {
                Enabled = true,
                InputBinding = new VoiceAiInputBinding
                {
                    Kind = VoiceAiInputBindingKind.RawInputHidButton,
                    DeviceId = @"\\?\hid#vid_046d&pid_c29b",
                    DeviceName = "Logitech Wheel",
                    ButtonIndex = 7,
                    ButtonMask = 64,
                    DisplayText = "Logitech Wheel · 按钮 7"
                },
                TalkMode = VoiceAiTalkMode.ToggleToTalk,
                MicrophoneDeviceId = "1",
                MicrophoneDeviceName = "USB Microphone",
                Hotkey = "F13",
                AssistantSettings = new VoiceAssistantSettings
                {
                    EnableVoiceAssistant = true,
                    PushToTalkKey = "F13",
                    PushToTalkButton = "方向盘/手柄设备 · 按钮 7",
                    EnableTtsAnswer = false,
                    MaxAnswerLength = 180,
                    RepeatQuestionCooldownSeconds = 18
                },
                AudioSettings = new VoiceInputAudioSettings
                {
                    EnableNoiseReduction = false,
                    EnableHighPassFilter = false,
                    HighPassCutoffHz = 160d,
                    EnableNoiseGate = true,
                    NoiseGateThresholdDb = -45d,
                    EnableVad = true,
                    PreSpeechPaddingMs = 180,
                    PostSpeechPaddingMs = 300,
                    EnableAutoGain = false,
                    MaxRecordingSeconds = 6,
                    MinSpeechDurationMs = 250,
                    MinRecognitionConfidence = 0.42d
                }
            });

        var persisted = await store.LoadAsync();
        using var json = await ReadPersistedJsonAsync(root);

        Assert.True(persisted.VoiceAi.Enabled);
        Assert.Equal(VoiceAiInputBindingKind.RawInputHidButton, persisted.VoiceAi.InputBinding.Kind);
        Assert.Equal(7, persisted.VoiceAi.InputBinding.ButtonIndex);
        Assert.Equal(64UL, persisted.VoiceAi.InputBinding.ButtonMask);
        Assert.Equal("方向盘/手柄设备 · 按钮 7", persisted.VoiceAi.InputBinding.DisplayText);
        Assert.Equal(VoiceAiTalkMode.ToggleToTalk, persisted.VoiceAi.TalkMode);
        Assert.Equal("1", persisted.VoiceAi.MicrophoneDeviceId);
        Assert.Equal("USB Microphone", persisted.VoiceAi.MicrophoneDeviceName);
        Assert.Equal("F13", persisted.VoiceAi.Hotkey);
        Assert.True(persisted.VoiceAssistantSettings.EnableVoiceAssistant);
        Assert.False(persisted.VoiceAssistantSettings.EnableTtsAnswer);
        Assert.Equal(180, persisted.VoiceAssistantSettings.MaxAnswerLength);
        Assert.Equal(18, persisted.VoiceAssistantSettings.RepeatQuestionCooldownSeconds);
        Assert.False(persisted.VoiceAi.AssistantSettings.EnableTtsAnswer);
        Assert.False(persisted.VoiceAi.AudioSettings.EnableNoiseReduction);
        Assert.False(persisted.VoiceAi.AudioSettings.EnableHighPassFilter);
        Assert.Equal(160d, persisted.VoiceAi.AudioSettings.HighPassCutoffHz);
        Assert.Equal(-45d, persisted.VoiceAi.AudioSettings.NoiseGateThresholdDb);
        Assert.Equal(180, persisted.VoiceAi.AudioSettings.PreSpeechPaddingMs);
        Assert.Equal(300, persisted.VoiceAi.AudioSettings.PostSpeechPaddingMs);
        Assert.False(persisted.VoiceAi.AudioSettings.EnableAutoGain);
        Assert.Equal(6, persisted.VoiceAi.AudioSettings.MaxRecordingSeconds);
        Assert.Equal(250, persisted.VoiceAi.AudioSettings.MinSpeechDurationMs);
        Assert.Equal(0.42d, persisted.VoiceAi.AudioSettings.MinRecognitionConfidence);
        Assert.Equal("configured", persisted.Ai.ApiKey);
        Assert.True(persisted.Tts.TtsEnabled);
        Assert.Equal(20778, persisted.Udp.ListenPort);
        var voiceAiJson = json.RootElement.GetProperty("voiceAi");
        Assert.True(voiceAiJson.GetProperty("enabled").GetBoolean());
        Assert.Equal((int)VoiceAiTalkMode.ToggleToTalk, voiceAiJson.GetProperty("talkMode").GetInt32());
        Assert.Equal("1", voiceAiJson.GetProperty("microphoneDeviceId").GetString());
        Assert.Equal("USB Microphone", voiceAiJson.GetProperty("microphoneDeviceName").GetString());
        Assert.Equal("F13", voiceAiJson.GetProperty("hotkey").GetString());
        Assert.Equal((int)VoiceAiInputBindingKind.RawInputHidButton, voiceAiJson.GetProperty("inputBinding").GetProperty("kind").GetInt32());
        Assert.Equal(7, voiceAiJson.GetProperty("inputBinding").GetProperty("buttonIndex").GetInt32());
        Assert.Equal("方向盘/手柄设备 · 按钮 7", voiceAiJson.GetProperty("inputBinding").GetProperty("displayText").GetString());
        var audioJson = voiceAiJson.GetProperty("audioSettings");
        Assert.False(audioJson.GetProperty("enableNoiseReduction").GetBoolean());
        Assert.False(audioJson.GetProperty("enableHighPassFilter").GetBoolean());
        Assert.Equal(160d, audioJson.GetProperty("highPassCutoffHz").GetDouble());
        Assert.Equal(-45d, audioJson.GetProperty("noiseGateThresholdDb").GetDouble());
        Assert.Equal(6, audioJson.GetProperty("maxRecordingSeconds").GetInt32());
        Assert.Equal(0.42d, audioJson.GetProperty("minRecognitionConfidence").GetDouble());
        Assert.False(voiceAiJson.GetProperty("assistantSettings").GetProperty("enableTtsAnswer").GetBoolean());
        Assert.False(json.RootElement.GetProperty("voiceAssistantSettings").GetProperty("enableTtsAnswer").GetBoolean());
    }

    /// <summary>
    /// Verifies legacy Raw Input labels that persisted HID paths or unreadable text are rebuilt for display.
    /// </summary>
    [Fact]
    public async Task LoadAsync_WithLegacyRawInputPathDisplayText_RebuildsReadableButtonLabel()
    {
        var root = CreateRootPath();
        Directory.CreateDirectory(Path.Combine(root, "F1Telemetry"));

        await File.WriteAllTextAsync(
            Path.Combine(root, "F1Telemetry", "settings.json"),
            """
            {
              "voiceAi": {
                "enabled": true,
                "inputBinding": {
                  "kind": "RawInputHidButton",
                  "deviceId": "\\\\?\\HID#VID_346E&PID_0004#MOZA",
                  "deviceName": "\\\\?\\HID#VID_346E&PID_0004#MOZA",
                  "buttonIndex": 10,
                  "buttonMask": 512,
                  "displayText": "\\\\?\\HID#VID_346E&PID_0004#MOZA · 按钮 10"
                }
              }
            }
            """);

        IAppSettingsStore store = new AppSettingsStore(root);

        var settings = await store.LoadAsync();

        Assert.Equal("方向盘/手柄设备", settings.VoiceAi.InputBinding.DeviceName);
        Assert.Equal("方向盘/手柄设备 · 按钮 10", settings.VoiceAi.InputBinding.DisplayText);
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
