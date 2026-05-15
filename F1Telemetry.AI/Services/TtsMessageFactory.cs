using System.Globalization;
using F1Telemetry.AI.Models;
using F1Telemetry.Analytics.Events;
using F1Telemetry.Analytics.Laps;
using F1Telemetry.Core.Formatting;
using F1Telemetry.Core.Models;
using F1Telemetry.TTS;
using F1Telemetry.TTS.Models;

namespace F1Telemetry.AI.Services;

/// <summary>
/// Maps race events and AI analysis results into queue-ready TTS messages without exposing queue policy to the UI layer.
/// </summary>
public sealed class TtsMessageFactory
{
    private const int MaxAiTtsTextLength = 48;
    private const int MinimumAiCooldownSeconds = 20;
    private const int MinimumRaceRiskCooldownSeconds = 30;
    private readonly object _sync = new();
    private readonly Dictionary<string, DateTimeOffset> _lastCreatedAtByCooldownKey = new(StringComparer.Ordinal);
    private int? _lastAiLapNumber;

    /// <summary>
    /// Clears session-scoped TTS pacing state.
    /// </summary>
    public void Reset()
    {
        lock (_sync)
        {
            _lastCreatedAtByCooldownKey.Clear();
            _lastAiLapNumber = null;
        }
    }

    /// <summary>
    /// Creates a queue-ready TTS message for a detected race event.
    /// </summary>
    /// <param name="raceEvent">The detected race event.</param>
    /// <param name="options">The current TTS options.</param>
    /// <param name="sessionMode">The current high-level session mode.</param>
    /// <returns>The mapped TTS message when the event should be spoken; otherwise <see langword="null"/>.</returns>
    public TtsMessage? CreateForRaceEvent(
        RaceEvent raceEvent,
        TtsOptions options,
        SessionMode sessionMode = SessionMode.Unknown)
    {
        ArgumentNullException.ThrowIfNull(raceEvent);
        ArgumentNullException.ThrowIfNull(options);

        if (raceEvent.EventType == EventType.DataQualityWarning ||
            string.IsNullOrWhiteSpace(raceEvent.Message))
        {
            return null;
        }

        if (ShouldSuppressForSessionMode(raceEvent.EventType, sessionMode))
        {
            return null;
        }

        var type = MapEventType(raceEvent.EventType);
        var id = BuildEventIdentifier(raceEvent);
        var cooldown = BuildEventCooldown(raceEvent.EventType, options);
        var cooldownKey = BuildEventCooldownKey(raceEvent, type);
        if (ShouldApplyFactoryCooldown(raceEvent.EventType) &&
            IsCoolingDownOrMarkCreated(cooldownKey, cooldown, DateTimeOffset.UtcNow))
        {
            return null;
        }

        return new TtsMessage
        {
            Source = "TTS",
            Type = type,
            Text = raceEvent.Message.Trim(),
            DedupKey = BuildDedupKey("event", type, id),
            Priority = MapPriority(raceEvent.EventType, raceEvent.Severity),
            Cooldown = cooldown
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

        var speechText = FormatAiSpeechText(result.TtsText);
        if (!result.IsSuccess || string.IsNullOrWhiteSpace(speechText) || speechText == "-")
        {
            return null;
        }

        var cooldown = TimeSpan.FromSeconds(Math.Max(Math.Max(1, options.CooldownSeconds), MinimumAiCooldownSeconds));
        lock (_sync)
        {
            if (_lastAiLapNumber == lastLap.LapNumber ||
                IsCoolingDownUnsafe("ai:lap", cooldown, DateTimeOffset.UtcNow))
            {
                return null;
            }

            _lastAiLapNumber = lastLap.LapNumber;
            _lastCreatedAtByCooldownKey["ai:lap"] = DateTimeOffset.UtcNow;
        }

        return new TtsMessage
        {
            Source = "AI",
            Type = "lap",
            Text = speechText,
            DedupKey = BuildDedupKey("ai", "lap", lastLap.LapNumber.ToString(CultureInfo.InvariantCulture)),
            Priority = TtsPriority.Low,
            Cooldown = cooldown
        };
    }

    private static bool ShouldSuppressForSessionMode(EventType eventType, SessionMode sessionMode)
    {
        return eventType switch
        {
            EventType.FrontCarPitted
                or EventType.RearCarPitted
                or EventType.AttackWindow
                or EventType.DefenseWindow
                or EventType.FrontOldTyreRisk
                or EventType.RearNewTyrePressure
                or EventType.RacePitWindow =>
                !SessionModeFormatter.AllowsPitWindowSpeech(sessionMode),
            EventType.QualifyingCleanAirWindow =>
                sessionMode is not (SessionMode.Qualifying or SessionMode.SprintQualifying or SessionMode.TimeTrial),
            EventType.TrafficRisk =>
                sessionMode is not (SessionMode.Race or SessionMode.SprintRace or SessionMode.Qualifying or SessionMode.SprintQualifying or SessionMode.TimeTrial),
            EventType.SafetyCarRestart or EventType.RedFlagTyreChange =>
                sessionMode is not (SessionMode.Race or SessionMode.SprintRace or SessionMode.Unknown),
            _ => false
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
            EventType.AttackWindow or EventType.DefenseWindow or EventType.LowErs =>
                $"lap{FormatOptionalInt(raceEvent.LapNumber)}",
            EventType.FrontOldTyreRisk
                or EventType.RearNewTyrePressure
                or EventType.TrafficRisk
                or EventType.QualifyingCleanAirWindow
                or EventType.RacePitWindow
                or EventType.SafetyCarRestart =>
                $"lap{FormatOptionalInt(raceEvent.LapNumber)}",
            EventType.RedFlagTyreChange =>
                string.IsNullOrWhiteSpace(raceEvent.DedupKey) || raceEvent.DedupKey.Trim() == "-"
                    ? $"lap{FormatOptionalInt(raceEvent.LapNumber)}"
                    : raceEvent.DedupKey.Trim(),
            EventType.SafetyCar or EventType.VirtualSafetyCar =>
                raceEvent.EventType.ToString().ToLowerInvariant(),
            EventType.YellowFlag or EventType.RedFlag =>
                $"flag:lap{FormatOptionalInt(raceEvent.LapNumber)}",
            EventType.HighTyreWear =>
                $"car{FormatOptionalInt(raceEvent.VehicleIdx)}:lap{FormatOptionalInt(raceEvent.LapNumber)}",
            EventType.HighTyreTemperature or EventType.LowTyreTemperature =>
                string.IsNullOrWhiteSpace(raceEvent.DedupKey) || raceEvent.DedupKey.Trim() == "-"
                    ? $"car{FormatOptionalInt(raceEvent.VehicleIdx)}:lap{FormatOptionalInt(raceEvent.LapNumber)}"
                    : raceEvent.DedupKey.Trim(),
            EventType.CarDamage or EventType.DrsFault or EventType.ErsFault or EventType.EngineFailure =>
                string.IsNullOrWhiteSpace(raceEvent.DedupKey)
                    ? $"car{FormatOptionalInt(raceEvent.VehicleIdx)}:lap{FormatOptionalInt(raceEvent.LapNumber)}"
                    : raceEvent.DedupKey.Trim(),
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
            EventType.HighTyreTemperature => "high_tyre_temperature",
            EventType.LowTyreTemperature => "low_tyre_temperature",
            EventType.SafetyCar => "safety_car",
            EventType.VirtualSafetyCar => "virtual_safety_car",
            EventType.YellowFlag => "yellow_flag",
            EventType.RedFlag => "red_flag",
            EventType.AttackWindow => "attack_window",
            EventType.DefenseWindow => "defense_window",
            EventType.LowErs => "low_ers",
            EventType.CarDamage => "car_damage",
            EventType.DrsFault => "drs_fault",
            EventType.ErsFault => "ers_fault",
            EventType.EngineFailure => "engine_failure",
            EventType.FrontOldTyreRisk => "front_old_tyre_risk",
            EventType.RearNewTyrePressure => "rear_new_tyre_pressure",
            EventType.TrafficRisk => "traffic_risk",
            EventType.QualifyingCleanAirWindow => "qualifying_clean_air_window",
            EventType.RacePitWindow => "race_pit_window",
            EventType.SafetyCarRestart => "safety_car_restart",
            EventType.RedFlagTyreChange => "red_flag_tyre_change",
            _ => "event"
        };
    }

    private static TtsPriority MapPriority(EventType eventType, EventSeverity severity)
    {
        return eventType switch
        {
            EventType.SafetyCar
                or EventType.VirtualSafetyCar
                or EventType.YellowFlag
                or EventType.RedFlag
                or EventType.SafetyCarRestart
                or EventType.RedFlagTyreChange =>
                TtsPriority.High,
            EventType.LowFuel
                or EventType.HighTyreWear
                or EventType.HighTyreTemperature
                or EventType.LowTyreTemperature
                or EventType.AttackWindow
                or EventType.DefenseWindow
                or EventType.FrontOldTyreRisk
                or EventType.RearNewTyrePressure
                or EventType.TrafficRisk
                or EventType.RacePitWindow =>
                TtsPriority.High,
            EventType.QualifyingCleanAirWindow =>
                TtsPriority.Normal,
            EventType.EngineFailure =>
                TtsPriority.High,
            EventType.CarDamage or EventType.DrsFault or EventType.ErsFault =>
                TtsPriority.Normal,
            EventType.FrontCarPitted or EventType.RearCarPitted or EventType.LowErs =>
                TtsPriority.Normal,
            _ => severity == EventSeverity.Warning ? TtsPriority.High : TtsPriority.Normal
        };
    }

    private static TimeSpan BuildEventCooldown(EventType eventType, TtsOptions options)
    {
        var baseCooldownSeconds = Math.Max(1, options.CooldownSeconds);
        var cooldownSeconds = ShouldApplyFactoryCooldown(eventType)
            ? Math.Max(baseCooldownSeconds, MinimumRaceRiskCooldownSeconds)
            : baseCooldownSeconds;

        return TimeSpan.FromSeconds(cooldownSeconds);
    }

    private static bool ShouldApplyFactoryCooldown(EventType eventType)
    {
        return eventType is EventType.LowFuel
            or EventType.HighTyreWear
            or EventType.HighTyreTemperature
            or EventType.LowTyreTemperature
            or EventType.LowErs
            or EventType.AttackWindow
            or EventType.DefenseWindow
            or EventType.FrontOldTyreRisk
            or EventType.RearNewTyrePressure
            or EventType.TrafficRisk
            or EventType.QualifyingCleanAirWindow
            or EventType.RacePitWindow
            or EventType.SafetyCarRestart
            or EventType.RedFlagTyreChange
            or EventType.CarDamage
            or EventType.DrsFault
            or EventType.ErsFault
            or EventType.EngineFailure;
    }

    private static string BuildEventCooldownKey(RaceEvent raceEvent, string mappedType)
    {
        return raceEvent.EventType is EventType.CarDamage or EventType.DrsFault or EventType.ErsFault or EventType.EngineFailure
            ? $"event:{mappedType}:{BuildEventIdentifier(raceEvent)}"
            : $"event:{mappedType}";
    }

    private bool IsCoolingDownOrMarkCreated(string cooldownKey, TimeSpan cooldown, DateTimeOffset now)
    {
        lock (_sync)
        {
            if (IsCoolingDownUnsafe(cooldownKey, cooldown, now))
            {
                return true;
            }

            _lastCreatedAtByCooldownKey[cooldownKey] = now;
            return false;
        }
    }

    private bool IsCoolingDownUnsafe(string cooldownKey, TimeSpan cooldown, DateTimeOffset now)
    {
        return _lastCreatedAtByCooldownKey.TryGetValue(cooldownKey, out var lastCreatedAt) &&
               now - lastCreatedAt < cooldown;
    }

    private static string BuildDedupKey(string source, string type, string id)
    {
        return $"{source}:{type}:{id}";
    }

    private static string FormatAiSpeechText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var trimmed = text.Trim();
        if (trimmed.Length <= MaxAiTtsTextLength)
        {
            return trimmed;
        }

        return trimmed[..(MaxAiTtsTextLength - 3)].TrimEnd() + "...";
    }

    private static string FormatOptionalInt(int? value)
    {
        return value?.ToString(CultureInfo.InvariantCulture) ?? "unknown";
    }
}
