using System.Globalization;
using F1Telemetry.AI.Models;
using F1Telemetry.Analytics.Events;
using F1Telemetry.Analytics.Laps;
using F1Telemetry.TTS;
using F1Telemetry.TTS.Models;

namespace F1Telemetry.AI.Services;

/// <summary>
/// Maps race events and AI analysis results into queue-ready TTS messages without exposing queue policy to the UI layer.
/// </summary>
public sealed class TtsMessageFactory
{
    /// <summary>
    /// Creates a queue-ready TTS message for a detected race event.
    /// </summary>
    /// <param name="raceEvent">The detected race event.</param>
    /// <param name="options">The current TTS options.</param>
    /// <returns>The mapped TTS message when the event should be spoken; otherwise <see langword="null"/>.</returns>
    public TtsMessage? CreateForRaceEvent(RaceEvent raceEvent, TtsOptions options)
    {
        ArgumentNullException.ThrowIfNull(raceEvent);
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(raceEvent.Message))
        {
            return null;
        }

        var type = MapEventType(raceEvent.EventType);
        var id = BuildEventIdentifier(raceEvent);

        return new TtsMessage
        {
            Source = "TTS",
            Type = type,
            Text = raceEvent.Message.Trim(),
            DedupKey = BuildDedupKey("event", type, id),
            Priority = raceEvent.Severity == EventSeverity.Warning ? TtsPriority.High : TtsPriority.Normal,
            Cooldown = TimeSpan.FromSeconds(Math.Max(1, options.CooldownSeconds))
        };
    }

    /// <summary>
    /// Creates a queue-ready TTS message for an AI lap analysis result.
    /// </summary>
    /// <param name="lastLap">The completed lap tied to the AI result.</param>
    /// <param name="result">The AI analysis result.</param>
    /// <param name="options">The current TTS options.</param>
    /// <returns>The mapped TTS message when the AI result should be spoken; otherwise <see langword="null"/>.</returns>
    public TtsMessage? CreateForAiResult(LapSummary lastLap, AIAnalysisResult result, TtsOptions options)
    {
        ArgumentNullException.ThrowIfNull(lastLap);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(options);

        if (!result.IsSuccess || string.IsNullOrWhiteSpace(result.TtsText) || result.TtsText == "-")
        {
            return null;
        }

        return new TtsMessage
        {
            Source = "AI",
            Type = "lap",
            Text = result.TtsText.Trim(),
            DedupKey = BuildDedupKey("ai", "lap", lastLap.LapNumber.ToString(CultureInfo.InvariantCulture)),
            Priority = TtsPriority.Low,
            Cooldown = TimeSpan.FromSeconds(Math.Max(Math.Max(1, options.CooldownSeconds), 10))
        };
    }

    private static string BuildEventIdentifier(RaceEvent raceEvent)
    {
        return raceEvent.EventType switch
        {
            EventType.FrontCarPitted or EventType.RearCarPitted =>
                $"car{FormatOptionalInt(raceEvent.VehicleIdx)}:lap{FormatOptionalInt(raceEvent.LapNumber)}",
            EventType.PlayerLapInvalidated =>
                $"lap{FormatOptionalInt(raceEvent.LapNumber)}",
            EventType.LowFuel =>
                $"lap{FormatOptionalInt(raceEvent.LapNumber)}",
            EventType.HighTyreWear =>
                $"car{FormatOptionalInt(raceEvent.VehicleIdx)}:lap{FormatOptionalInt(raceEvent.LapNumber)}",
            _ =>
                $"lap{FormatOptionalInt(raceEvent.LapNumber)}"
        };
    }

    private static string MapEventType(EventType eventType)
    {
        return eventType switch
        {
            EventType.FrontCarPitted => "front_pit",
            EventType.RearCarPitted => "rear_pit",
            EventType.PlayerLapInvalidated => "lap_invalid",
            EventType.LowFuel => "low_fuel",
            EventType.HighTyreWear => "high_tyre_wear",
            _ => "event"
        };
    }

    private static string BuildDedupKey(string source, string type, string id)
    {
        return $"{source}:{type}:{id}";
    }

    private static string FormatOptionalInt(int? value)
    {
        return value?.ToString(CultureInfo.InvariantCulture) ?? "unknown";
    }
}
