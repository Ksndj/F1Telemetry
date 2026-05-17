using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace F1Telemetry.Core.Security;

/// <summary>
/// Redacts secrets and raw telemetry-shaped content before AI prompts or persistence.
/// </summary>
public static partial class SensitiveContentSanitizer
{
    /// <summary>
    /// Gets the replacement token used for sensitive content.
    /// </summary>
    public const string RedactedToken = "[REDACTED]";

    private static readonly JsonSerializerOptions CompactJsonOptions = new()
    {
        WriteIndented = false
    };

    private static readonly HashSet<string> SensitivePropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "api_key",
        "apiKey",
        "apikey",
        "token",
        "access_token",
        "refresh_token",
        "secret",
        "password",
        "authorization",
        "cookie",
        "set-cookie",
        "x-api-key"
    };

    private static readonly HashSet<string> RawTelemetryPropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "m_header",
        "packetId",
        "packet",
        "rawPacket",
        "payload",
        "payloadBase64",
        "rawPayload",
        "jsonl",
        "rawFile"
    };

    /// <summary>
    /// Redacts sensitive values from free text or JSON-like content.
    /// </summary>
    /// <param name="content">The content to sanitize.</param>
    public static string Sanitize(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var trimmed = content.Trim();
        if (LooksLikeRawTelemetryPayload(trimmed))
        {
            return RedactedToken;
        }

        if (TrySanitizeJson(trimmed, out var sanitizedJson))
        {
            return sanitizedJson;
        }

        return SanitizeTextPatterns(trimmed);
    }

    /// <summary>
    /// Redacts sensitive values while preserving null input.
    /// </summary>
    /// <param name="content">The optional content to sanitize.</param>
    public static string? SanitizeNullable(string? content)
    {
        return content is null ? null : Sanitize(content);
    }

    private static bool TrySanitizeJson(string content, out string sanitized)
    {
        try
        {
            var node = JsonNode.Parse(content);
            if (node is null)
            {
                sanitized = RedactedToken;
                return true;
            }

            SanitizeJsonNode(parentPropertyName: null, node);
            sanitized = SanitizeTextPatterns(node.ToJsonString(CompactJsonOptions));
            return true;
        }
        catch (JsonException)
        {
            sanitized = string.Empty;
            return false;
        }
    }

    private static JsonNode? SanitizeJsonNode(string? parentPropertyName, JsonNode? node)
    {
        if (node is null)
        {
            return null;
        }

        if (IsSensitiveProperty(parentPropertyName) || IsRawTelemetryProperty(parentPropertyName))
        {
            return JsonValue.Create(RedactedToken);
        }

        if (node is JsonObject jsonObject)
        {
            foreach (var property in jsonObject.ToArray())
            {
                jsonObject[property.Key] = SanitizeJsonNode(property.Key, property.Value);
            }

            return jsonObject;
        }

        if (node is JsonArray jsonArray)
        {
            for (var index = 0; index < jsonArray.Count; index++)
            {
                jsonArray[index] = SanitizeJsonNode(parentPropertyName: null, jsonArray[index]);
            }

            return jsonArray;
        }

        if (node is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var text))
        {
            return JsonValue.Create(SanitizeTextPatterns(text));
        }

        return node;
    }

    private static string SanitizeTextPatterns(string text)
    {
        var sanitized = AuthorizationHeaderPattern().Replace(text, RedactedToken);
        sanitized = BearerTokenPattern().Replace(sanitized, "Bearer " + RedactedToken);
        sanitized = HttpHeaderPattern().Replace(sanitized, "${name}: " + RedactedToken);
        sanitized = SensitiveAssignmentPattern().Replace(sanitized, "${prefix}" + RedactedToken);
        sanitized = JsonlPathPattern().Replace(sanitized, RedactedToken);
        sanitized = RawTelemetryMarkerPattern().Replace(sanitized, RedactedToken);
        sanitized = ApiKeyPhrasePattern().Replace(sanitized, RedactedToken);
        return sanitized;
    }

    private static bool LooksLikeRawTelemetryPayload(string text)
    {
        if (text.Contains("\"m_header\"", StringComparison.OrdinalIgnoreCase)
            || text.Contains("\"packetId\"", StringComparison.OrdinalIgnoreCase)
            || text.Contains("\"payloadBase64\"", StringComparison.OrdinalIgnoreCase)
            || text.Contains("raw packet", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var jsonLineCount = text
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Count(line => line.StartsWith('{') && line.EndsWith('}'));

        return jsonLineCount > 1;
    }

    private static bool IsSensitiveProperty(string? propertyName)
    {
        return propertyName is not null && SensitivePropertyNames.Contains(propertyName);
    }

    private static bool IsRawTelemetryProperty(string? propertyName)
    {
        return propertyName is not null && RawTelemetryPropertyNames.Contains(propertyName);
    }

    [GeneratedRegex(@"(?im)(?<prefix>\bauthorization\s*:\s*)(?:bearer\s+)?[^\r\n,;""'}]+")]
    private static partial Regex AuthorizationHeaderPattern();

    [GeneratedRegex(@"(?i)\bbearer\s+[A-Za-z0-9._~+/=-]+")]
    private static partial Regex BearerTokenPattern();

    [GeneratedRegex(@"(?im)(?<name>\b(?:x-api-key|api-key|cookie|set-cookie)\b)\s*:\s*[^\r\n]+")]
    private static partial Regex HttpHeaderPattern();

    [GeneratedRegex(@"(?i)(?<prefix>\b(?:api[_\-\s]?key|apikey|token|secret|password)\b\s*[:=]\s*)[""']?[^""'\s,;}]+")]
    private static partial Regex SensitiveAssignmentPattern();

    [GeneratedRegex(@"(?i)\S+\.jsonl\b")]
    private static partial Regex JsonlPathPattern();

    [GeneratedRegex(@"(?i)\b(?:packetId|m_header|payloadBase64|raw packet)\b")]
    private static partial Regex RawTelemetryMarkerPattern();

    [GeneratedRegex(@"(?i)\bapi\s*key\b|\bapikey\b")]
    private static partial Regex ApiKeyPhrasePattern();
}
