using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using F1Telemetry.AI.Interfaces;
using F1Telemetry.AI.Models;
using F1Telemetry.Core;
using F1Telemetry.Core.Models;
using F1Telemetry.TTS.Models;

namespace F1Telemetry.AI.Services;

/// <summary>
/// Persists app settings to a single JSON document under the user's application data profile.
/// </summary>
public sealed class AppSettingsStore : IAppSettingsStore
{
    private const string ProtectedApiKeyPrefix = "dpapi:";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string _settingsPath;

    /// <summary>
    /// Initializes a new settings store.
    /// </summary>
    /// <param name="appDataRootOverride">Optional application data root override used by tests.</param>
    public AppSettingsStore(string? appDataRootOverride = null)
    {
        _settingsPath = string.IsNullOrWhiteSpace(appDataRootOverride)
            ? AppPaths.GetSettingsPath()
            : Path.Combine(appDataRootOverride, "F1Telemetry", "settings.json");
    }

    /// <inheritdoc />
    public Task<AppSettingsDocument> LoadAsync(CancellationToken cancellationToken = default)
    {
        return LoadDocumentCoreAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task SaveAiSettingsAsync(AISettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var existing = await LoadDocumentCoreAsync(cancellationToken);
        await WriteDocumentAsync(
            existing with { Ai = settings with { ApiKey = ProtectApiKey(settings.ApiKey) } },
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task SaveTtsSettingsAsync(TtsOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var existing = await LoadDocumentCoreAsync(cancellationToken);
        await WriteDocumentAsync(existing with { Tts = options }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SaveRaceWeekendTyrePlanAsync(RaceWeekendTyrePlan plan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var existing = await LoadDocumentCoreAsync(cancellationToken);
        await WriteDocumentAsync(existing with { RaceWeekendTyrePlan = NormalizeRaceWeekendTyrePlan(plan) }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SaveUdpRawLogOptionsAsync(UdpRawLogOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var existing = await LoadDocumentCoreAsync(cancellationToken);
        await WriteDocumentAsync(existing with { UdpRawLog = NormalizeUdpRawLogOptions(options) }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SaveVoiceAiOptionsAsync(VoiceAiOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var existing = await LoadDocumentCoreAsync(cancellationToken);
        await WriteDocumentAsync(existing with { VoiceAi = NormalizeVoiceAiOptions(options) }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SaveUdpSettingsAsync(UdpSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var existing = await LoadDocumentCoreAsync(cancellationToken);
        await WriteDocumentAsync(existing with { Udp = NormalizeUdpSettings(settings) }, cancellationToken);
    }

    private async Task<AppSettingsDocument> LoadDocumentCoreAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_settingsPath))
        {
            return new AppSettingsDocument();
        }

        try
        {
            await using var stream = File.OpenRead(_settingsPath);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;

            return new AppSettingsDocument
            {
                Ai = ReadAiSettings(root),
                Tts = ReadTtsSettings(root),
                RaceWeekendTyrePlan = ReadRaceWeekendTyrePlan(root),
                UdpRawLog = ReadUdpRawLogOptions(root),
                VoiceAi = ReadVoiceAiOptions(root),
                Udp = ReadUdpSettings(root)
            };
        }
        catch
        {
            return new AppSettingsDocument();
        }
    }

    private async Task WriteDocumentAsync(AppSettingsDocument document, CancellationToken cancellationToken)
    {
        var directoryPath = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        var tempPath = $"{_settingsPath}.{Guid.NewGuid():N}.tmp";

        try
        {
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, document, SerializerOptions, cancellationToken);
            }

            if (File.Exists(_settingsPath))
            {
                File.Replace(tempPath, _settingsPath, destinationBackupFileName: null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tempPath, _settingsPath);
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static AISettings ReadAiSettings(JsonElement rootElement)
    {
        if (!TryGetAiElement(rootElement, out var aiElement))
        {
            return new AISettings();
        }

        try
        {
            return new AISettings
            {
                ApiKey = ReadApiKey(aiElement),
                BaseUrl = ReadString(aiElement, "baseUrl", "https://api.deepseek.com"),
                Model = ReadString(aiElement, "model", "deepseek-chat"),
                AiEnabled = ReadBool(aiElement, "enabled", ReadBool(aiElement, "aiEnabled")),
                RequestTimeoutSeconds = Math.Max(1, ReadInt(aiElement, "requestTimeoutSeconds", 10))
            };
        }
        catch
        {
            return new AISettings();
        }
    }

    private static TtsOptions ReadTtsSettings(JsonElement rootElement)
    {
        if (!rootElement.TryGetProperty("tts", out var ttsElement))
        {
            return new TtsOptions();
        }

        try
        {
            return new TtsOptions
            {
                TtsEnabled = ReadBool(ttsElement, "enabled", ReadBool(ttsElement, "ttsEnabled")),
                VoiceName = ReadString(ttsElement, "voiceName"),
                Volume = Math.Clamp(ReadInt(ttsElement, "volume", 100), 0, 100),
                Rate = Math.Clamp(ReadInt(ttsElement, "rate", 0), -10, 10),
                CooldownSeconds = Math.Max(1, ReadInt(ttsElement, "cooldownSeconds", 8))
            };
        }
        catch
        {
            return new TtsOptions();
        }
    }

    private static RaceWeekendTyrePlan ReadRaceWeekendTyrePlan(JsonElement rootElement)
    {
        if (!rootElement.TryGetProperty("raceWeekendTyrePlan", out var planElement))
        {
            return new RaceWeekendTyrePlan();
        }

        try
        {
            var maxWear = ReadInt(
                planElement,
                "maxRecommendedWearPercent",
                RaceWeekendTyrePlan.DefaultMaxRecommendedWearPercent);
            var hasStructuredInventory = planElement.TryGetProperty("softCount", out _) ||
                                         planElement.TryGetProperty("mediumCount", out _) ||
                                         planElement.TryGetProperty("hardCount", out _) ||
                                         planElement.TryGetProperty("intermediateCount", out _) ||
                                         planElement.TryGetProperty("wetCount", out _);

            if (hasStructuredInventory)
            {
                return NormalizeRaceWeekendTyrePlan(
                    new RaceWeekendTyrePlan
                    {
                        SoftCount = ReadInt(planElement, "softCount", 0),
                        MediumCount = ReadInt(planElement, "mediumCount", 0),
                        HardCount = ReadInt(planElement, "hardCount", 0),
                        IntermediateCount = ReadInt(planElement, "intermediateCount", 0),
                        WetCount = ReadInt(planElement, "wetCount", 0),
                        InventoryText = ReadString(planElement, "inventoryText", RaceWeekendTyrePlan.DefaultInventoryText),
                        MaxRecommendedWearPercent = maxWear
                    });
            }

            return RaceWeekendTyrePlan.FromLegacyInventoryText(
                ReadString(planElement, "inventoryText", RaceWeekendTyrePlan.DefaultInventoryText),
                maxWear);
        }
        catch
        {
            return new RaceWeekendTyrePlan();
        }
    }

    private static RaceWeekendTyrePlan NormalizeRaceWeekendTyrePlan(RaceWeekendTyrePlan plan)
    {
        var hasStructuredCounts = plan.SoftCount > 0 ||
                                  plan.MediumCount > 0 ||
                                  plan.HardCount > 0 ||
                                  plan.IntermediateCount > 0 ||
                                  plan.WetCount > 0;

        if (!hasStructuredCounts &&
            !string.IsNullOrWhiteSpace(plan.InventoryText) &&
            !string.Equals(plan.InventoryText.Trim(), RaceWeekendTyrePlan.DefaultInventoryText, StringComparison.Ordinal))
        {
            return RaceWeekendTyrePlan.FromLegacyInventoryText(
                plan.InventoryText,
                plan.MaxRecommendedWearPercent);
        }

        return plan.Normalize();
    }

    private static UdpRawLogOptions ReadUdpRawLogOptions(JsonElement rootElement)
    {
        if (!rootElement.TryGetProperty("udpRawLog", out var rawLogElement))
        {
            return new UdpRawLogOptions();
        }

        try
        {
            return NormalizeUdpRawLogOptions(
                new UdpRawLogOptions
                {
                    Enabled = ReadBool(rawLogElement, "enabled"),
                    DirectoryPath = ReadString(rawLogElement, "directoryPath"),
                    QueueCapacity = ReadInt(rawLogElement, "queueCapacity", 4096)
                });
        }
        catch
        {
            return new UdpRawLogOptions();
        }
    }

    private static UdpRawLogOptions NormalizeUdpRawLogOptions(UdpRawLogOptions options)
    {
        return options with
        {
            DirectoryPath = options.DirectoryPath?.Trim() ?? string.Empty,
            QueueCapacity = Math.Clamp(options.QueueCapacity, 0, 100_000)
        };
    }

    private static VoiceAiOptions ReadVoiceAiOptions(JsonElement rootElement)
    {
        if (!rootElement.TryGetProperty("voiceAi", out var voiceAiElement))
        {
            return new VoiceAiOptions();
        }

        try
        {
            return NormalizeVoiceAiOptions(
                new VoiceAiOptions
                {
                    Enabled = ReadBool(voiceAiElement, "enabled"),
                    InputBinding = ReadVoiceAiInputBinding(voiceAiElement),
                    TalkMode = ReadEnum(voiceAiElement, "talkMode", VoiceAiTalkMode.HoldToTalk),
                    MicrophoneDeviceId = ReadString(voiceAiElement, "microphoneDeviceId"),
                    MicrophoneDeviceName = ReadString(voiceAiElement, "microphoneDeviceName"),
                    Hotkey = ReadString(voiceAiElement, "hotkey", VoiceAiOptions.NoHotkey)
                });
        }
        catch
        {
            return new VoiceAiOptions();
        }
    }

    private static VoiceAiInputBinding ReadVoiceAiInputBinding(JsonElement voiceAiElement)
    {
        if (!voiceAiElement.TryGetProperty("inputBinding", out var bindingElement))
        {
            return new VoiceAiInputBinding();
        }

        return NormalizeVoiceAiInputBinding(
            new VoiceAiInputBinding
            {
                Kind = ReadEnum(bindingElement, "kind", VoiceAiInputBindingKind.None),
                DeviceId = ReadString(bindingElement, "deviceId"),
                DeviceName = ReadString(bindingElement, "deviceName"),
                ButtonIndex = Math.Max(0, ReadInt(bindingElement, "buttonIndex", 0)),
                ButtonMask = ReadUInt64(bindingElement, "buttonMask", 0),
                DisplayText = ReadString(bindingElement, "displayText")
            });
    }

    private static VoiceAiOptions NormalizeVoiceAiOptions(VoiceAiOptions options)
    {
        var hotkey = options.Hotkey?.Trim();
        return options with
        {
            InputBinding = NormalizeVoiceAiInputBinding(options.InputBinding),
            MicrophoneDeviceId = options.MicrophoneDeviceId?.Trim() ?? string.Empty,
            MicrophoneDeviceName = options.MicrophoneDeviceName?.Trim() ?? string.Empty,
            Hotkey = string.IsNullOrWhiteSpace(hotkey) ? VoiceAiOptions.NoHotkey : hotkey
        };
    }

    private static VoiceAiInputBinding NormalizeVoiceAiInputBinding(VoiceAiInputBinding binding)
    {
        if (binding.Kind == VoiceAiInputBindingKind.None || binding.ButtonIndex <= 0)
        {
            return new VoiceAiInputBinding();
        }

        var displayText = binding.Kind == VoiceAiInputBindingKind.RawInputHidButton ||
                          VoiceAiInputBinding.ShouldRegenerateDisplayText(binding.DisplayText)
            ? VoiceAiInputBinding.FormatDisplayText(binding.ButtonIndex)
            : binding.DisplayText.Trim();

        return binding with
        {
            DeviceId = binding.DeviceId?.Trim() ?? string.Empty,
            DeviceName = VoiceAiInputBinding.SanitizeDeviceName(binding.DeviceName),
            DisplayText = displayText
        };
    }

    private static UdpSettings ReadUdpSettings(JsonElement rootElement)
    {
        if (!rootElement.TryGetProperty("udp", out var udpElement))
        {
            return new UdpSettings();
        }

        try
        {
            return NormalizeUdpSettings(
                new UdpSettings
                {
                    ListenPort = ReadInt(udpElement, "listenPort", UdpSettings.DefaultListenPort)
                });
        }
        catch
        {
            return new UdpSettings();
        }
    }

    private static UdpSettings NormalizeUdpSettings(UdpSettings settings)
    {
        return settings.ListenPort is >= UdpSettings.MinListenPort and <= UdpSettings.MaxListenPort
            ? settings
            : new UdpSettings();
    }

    private static bool TryGetAiElement(JsonElement rootElement, out JsonElement aiElement)
    {
        if (rootElement.TryGetProperty("ai", out aiElement))
        {
            return true;
        }

        if (LooksLikeLegacyAiRoot(rootElement))
        {
            aiElement = rootElement;
            return true;
        }

        aiElement = default;
        return false;
    }

    private static bool LooksLikeLegacyAiRoot(JsonElement rootElement)
    {
        return rootElement.ValueKind == JsonValueKind.Object &&
               (rootElement.TryGetProperty("apiKey", out _) ||
                rootElement.TryGetProperty("baseUrl", out _) ||
                rootElement.TryGetProperty("model", out _) ||
                rootElement.TryGetProperty("enabled", out _) ||
                rootElement.TryGetProperty("aiEnabled", out _) ||
                rootElement.TryGetProperty("requestTimeoutSeconds", out _));
    }

    private static string ReadApiKey(JsonElement aiElement)
    {
        var candidate = ReadString(aiElement, "apiKey");
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return string.Empty;
        }

        if (TryUnprotect(candidate, out var decryptedApiKey))
        {
            return decryptedApiKey;
        }

        if (candidate.StartsWith(ProtectedApiKeyPrefix, StringComparison.Ordinal))
        {
            return string.Empty;
        }

        return candidate;
    }

    private static string ProtectApiKey(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey) || !OperatingSystem.IsWindows())
        {
            return apiKey;
        }

        try
        {
            var bytes = Encoding.UTF8.GetBytes(apiKey);
            var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            return $"{ProtectedApiKeyPrefix}{Convert.ToBase64String(encrypted)}";
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Windows DPAPI 不可用，已拒绝保存 API Key。", ex);
        }
    }

    private static bool TryUnprotect(string encryptedValue, out string apiKey)
    {
        apiKey = string.Empty;

        if (TryUnprotectProtectedValue(encryptedValue, out apiKey))
        {
            return true;
        }

        return TryUnprotectLegacyValue(encryptedValue, out apiKey);
    }

    private static bool TryUnprotectProtectedValue(string encryptedValue, out string apiKey)
    {
        apiKey = string.Empty;

        if (!encryptedValue.StartsWith(ProtectedApiKeyPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        return TryUnprotectBase64(
            encryptedValue[ProtectedApiKeyPrefix.Length..],
            out apiKey);
    }

    private static bool TryUnprotectLegacyValue(string encryptedValue, out string apiKey)
    {
        apiKey = string.Empty;

        return TryUnprotectBase64(encryptedValue, out apiKey);
    }

    private static bool TryUnprotectBase64(string encryptedValue, out string apiKey)
    {
        apiKey = string.Empty;

        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            var bytes = Convert.FromBase64String(encryptedValue);
            var decrypted = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
            apiKey = Encoding.UTF8.GetString(decrypted);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string ReadString(JsonElement element, string propertyName, string defaultValue = "")
    {
        if (element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String)
        {
            return property.GetString() ?? defaultValue;
        }

        return defaultValue;
    }

    private static int ReadInt(JsonElement element, string propertyName, int defaultValue)
    {
        if (element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Number)
        {
            return property.GetInt32();
        }

        return defaultValue;
    }

    private static bool ReadBool(JsonElement element, string propertyName, bool defaultValue = false)
    {
        if (element.TryGetProperty(propertyName, out var property) &&
            (property.ValueKind == JsonValueKind.True || property.ValueKind == JsonValueKind.False))
        {
            return property.GetBoolean();
        }

        return defaultValue;
    }

    private static ulong ReadUInt64(JsonElement element, string propertyName, ulong defaultValue)
    {
        if (element.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.Number &&
            property.TryGetUInt64(out var value))
        {
            return value;
        }

        return defaultValue;
    }

    private static TEnum ReadEnum<TEnum>(JsonElement element, string propertyName, TEnum defaultValue)
        where TEnum : struct, Enum
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return defaultValue;
        }

        if (property.ValueKind == JsonValueKind.String &&
            Enum.TryParse<TEnum>(property.GetString(), ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        if (property.ValueKind == JsonValueKind.Number &&
            property.TryGetInt32(out var numeric) &&
            Enum.IsDefined(typeof(TEnum), numeric))
        {
            return (TEnum)Enum.ToObject(typeof(TEnum), numeric);
        }

        return defaultValue;
    }
}
