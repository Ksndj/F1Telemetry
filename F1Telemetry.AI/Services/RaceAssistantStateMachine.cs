using F1Telemetry.AI.Models;
using F1Telemetry.Core.Formatting;
using F1Telemetry.Core.Models;

namespace F1Telemetry.AI.Services;

/// <summary>
/// Resolves a race-assistant mode from compact session state.
/// </summary>
public sealed class RaceAssistantStateMachine
{
    /// <summary>
    /// Resolves the current assistant mode.
    /// </summary>
    /// <param name="sessionState">The current session state.</param>
    public RaceAssistantMode Resolve(SessionState sessionState)
    {
        ArgumentNullException.ThrowIfNull(sessionState);

        if (sessionState.HasFinalClassification)
        {
            return RaceAssistantMode.PostRace;
        }

        if (HasRedFlag(sessionState))
        {
            return RaceAssistantMode.RedFlag;
        }

        return sessionState.SafetyCarStatus switch
        {
            1 => RaceAssistantMode.SafetyCar,
            2 => RaceAssistantMode.VirtualSafetyCar,
            _ => ResolveGreenFlagMode(sessionState)
        };
    }

    private static RaceAssistantMode ResolveGreenFlagMode(SessionState sessionState)
    {
        var sessionMode = SessionModeFormatter.Resolve(
            sessionState.SessionType,
            sessionState.TotalLaps,
            sessionState.WeekendStructure);
        int? currentLap = sessionState.PlayerCar?.CurrentLapNumber is null
            ? null
            : sessionState.PlayerCar.CurrentLapNumber.Value;
        int? totalLaps = sessionState.TotalLaps is null ? null : sessionState.TotalLaps.Value;

        return sessionMode switch
        {
            SessionMode.Practice or SessionMode.TimeTrial => RaceAssistantMode.Practice,
            SessionMode.Qualifying or SessionMode.SprintQualifying => sessionState.PlayerCar?.CurrentLapTimeInMs is > 0
                ? RaceAssistantMode.QualifyingPush
                : RaceAssistantMode.QualifyingPrep,
            SessionMode.Race or SessionMode.SprintRace => ResolveRaceMode(sessionState, currentLap, totalLaps),
            _ => RaceAssistantMode.Practice
        };
    }

    private static RaceAssistantMode ResolveRaceMode(SessionState sessionState, int? currentLap, int? totalLaps)
    {
        if (currentLap is null or <= 2)
        {
            return RaceAssistantMode.RaceOpening;
        }

        if (totalLaps is not null && totalLaps.Value - currentLap.Value <= 3)
        {
            return RaceAssistantMode.FinalLaps;
        }

        int? pitWindowIdealLap = sessionState.PitStopWindowIdealLap is null ? null : sessionState.PitStopWindowIdealLap.Value;
        int? pitWindowLatestLap = sessionState.PitStopWindowLatestLap is null ? null : sessionState.PitStopWindowLatestLap.Value;
        if (pitWindowIdealLap is > 0 &&
            pitWindowLatestLap is > 0 &&
            currentLap >= pitWindowIdealLap &&
            currentLap <= pitWindowLatestLap)
        {
            return RaceAssistantMode.InPitWindow;
        }

        if (pitWindowIdealLap is > 0 &&
            currentLap >= pitWindowIdealLap - 2 &&
            currentLap < pitWindowIdealLap)
        {
            return RaceAssistantMode.PitWindowApproaching;
        }

        return RaceAssistantMode.RaceStintManagement;
    }

    private static bool HasRedFlag(SessionState sessionState)
    {
        return string.Equals(sessionState.LastEventCode, "RDFL", StringComparison.OrdinalIgnoreCase) ||
               sessionState.MarshalZoneFlags.Values.Any(flag => flag == 4);
    }
}
