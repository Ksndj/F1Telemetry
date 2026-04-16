using F1Telemetry.Udp.Packets;

namespace F1Telemetry.Udp.Parsers;

public sealed class SessionPacketParser : FixedSizePacketParser<SessionPacket>
{
    public SessionPacketParser()
        : base(nameof(SessionPacket), UdpPacketConstants.SessionBodySize)
    {
    }

    protected override SessionPacket Parse(ref PacketBufferReader reader)
    {
        var marshalZones = new MarshalZoneData[UdpPacketConstants.MaxMarshalZones];
        var weatherForecastSamples = new WeatherForecastSampleData[UdpPacketConstants.MaxWeatherForecastSamples];

        var weather = reader.ReadByte();
        var trackTemperature = reader.ReadSByte();
        var airTemperature = reader.ReadSByte();
        var totalLaps = reader.ReadByte();
        var trackLength = reader.ReadUInt16();
        var sessionType = reader.ReadByte();
        var trackId = reader.ReadSByte();
        var formula = reader.ReadByte();
        var sessionTimeLeft = reader.ReadUInt16();
        var sessionDuration = reader.ReadUInt16();
        var pitSpeedLimit = reader.ReadByte();
        var gamePaused = reader.ReadBooleanByte();
        var isSpectating = reader.ReadBooleanByte();
        var spectatorCarIndex = reader.ReadByte();
        var sliProNativeSupport = reader.ReadBooleanByte();
        var numMarshalZones = reader.ReadByte();

        for (var index = 0; index < marshalZones.Length; index++)
        {
            marshalZones[index] = new MarshalZoneData(
                ZoneStart: reader.ReadSingle(),
                ZoneFlag: reader.ReadSByte());
        }

        var safetyCarStatus = reader.ReadByte();
        var networkGame = reader.ReadBooleanByte();
        var numWeatherForecastSamples = reader.ReadByte();

        for (var index = 0; index < weatherForecastSamples.Length; index++)
        {
            weatherForecastSamples[index] = new WeatherForecastSampleData(
                SessionType: reader.ReadByte(),
                TimeOffset: reader.ReadByte(),
                Weather: reader.ReadByte(),
                TrackTemperature: reader.ReadSByte(),
                TrackTemperatureChange: reader.ReadSByte(),
                AirTemperature: reader.ReadSByte(),
                AirTemperatureChange: reader.ReadSByte(),
                RainPercentage: reader.ReadByte());
        }

        var forecastAccuracy = reader.ReadByte();
        var aiDifficulty = reader.ReadByte();
        var seasonLinkIdentifier = reader.ReadUInt32();
        var weekendLinkIdentifier = reader.ReadUInt32();
        var sessionLinkIdentifier = reader.ReadUInt32();
        var pitStopWindowIdealLap = reader.ReadByte();
        var pitStopWindowLatestLap = reader.ReadByte();
        var pitStopRejoinPosition = reader.ReadByte();
        var steeringAssist = reader.ReadBooleanByte();
        var brakingAssist = reader.ReadByte();
        var gearboxAssist = reader.ReadByte();
        var pitAssist = reader.ReadBooleanByte();
        var pitReleaseAssist = reader.ReadBooleanByte();
        var ersAssist = reader.ReadBooleanByte();
        var drsAssist = reader.ReadBooleanByte();
        var dynamicRacingLine = reader.ReadByte();
        var dynamicRacingLineType = reader.ReadByte();
        var gameMode = reader.ReadByte();
        var ruleSet = reader.ReadByte();
        var timeOfDay = reader.ReadUInt32();
        var sessionLength = reader.ReadByte();
        var speedUnitsLeadPlayer = reader.ReadByte();
        var temperatureUnitsLeadPlayer = reader.ReadByte();
        var speedUnitsSecondaryPlayer = reader.ReadByte();
        var temperatureUnitsSecondaryPlayer = reader.ReadByte();
        var numSafetyCarPeriods = reader.ReadByte();
        var numVirtualSafetyCarPeriods = reader.ReadByte();
        var numRedFlagPeriods = reader.ReadByte();
        var equalCarPerformance = reader.ReadBooleanByte();
        var recoveryMode = reader.ReadByte();
        var flashbackLimit = reader.ReadByte();
        var surfaceType = reader.ReadByte();
        var lowFuelMode = reader.ReadBooleanByte();
        var raceStarts = reader.ReadBooleanByte();
        var tyreTemperature = reader.ReadBooleanByte();
        var pitLaneTyreSim = reader.ReadBooleanByte();
        var carDamage = reader.ReadByte();
        var carDamageRate = reader.ReadByte();
        var collisions = reader.ReadByte();
        var collisionsOffForFirstLapOnly = reader.ReadBooleanByte();
        var mpUnsafePitRelease = reader.ReadBooleanByte();
        var mpOffForGriefing = reader.ReadBooleanByte();
        var cornerCuttingStringency = reader.ReadByte();
        var parcFermeRules = reader.ReadBooleanByte();
        var pitStopExperience = reader.ReadByte();
        var safetyCar = reader.ReadByte();
        var safetyCarExperience = reader.ReadByte();
        var formationLap = reader.ReadBooleanByte();
        var formationLapExperience = reader.ReadBooleanByte();
        var redFlags = reader.ReadByte();
        var affectsLicenceLevelSolo = reader.ReadBooleanByte();
        var affectsLicenceLevelMp = reader.ReadBooleanByte();
        var numSessionsInWeekend = reader.ReadByte();
        var weekendStructure = reader.ReadBytes(UdpPacketConstants.MaxWeekendSessions);
        var sector2LapDistanceStart = reader.ReadSingle();
        var sector3LapDistanceStart = reader.ReadSingle();

        return new SessionPacket(
            Weather: weather,
            TrackTemperature: trackTemperature,
            AirTemperature: airTemperature,
            TotalLaps: totalLaps,
            TrackLength: trackLength,
            SessionType: sessionType,
            TrackId: trackId,
            Formula: formula,
            SessionTimeLeft: sessionTimeLeft,
            SessionDuration: sessionDuration,
            PitSpeedLimit: pitSpeedLimit,
            GamePaused: gamePaused,
            IsSpectating: isSpectating,
            SpectatorCarIndex: spectatorCarIndex,
            SliProNativeSupport: sliProNativeSupport,
            NumMarshalZones: numMarshalZones,
            MarshalZones: marshalZones,
            SafetyCarStatus: safetyCarStatus,
            NetworkGame: networkGame,
            NumWeatherForecastSamples: numWeatherForecastSamples,
            WeatherForecastSamples: weatherForecastSamples,
            ForecastAccuracy: forecastAccuracy,
            AiDifficulty: aiDifficulty,
            SeasonLinkIdentifier: seasonLinkIdentifier,
            WeekendLinkIdentifier: weekendLinkIdentifier,
            SessionLinkIdentifier: sessionLinkIdentifier,
            PitStopWindowIdealLap: pitStopWindowIdealLap,
            PitStopWindowLatestLap: pitStopWindowLatestLap,
            PitStopRejoinPosition: pitStopRejoinPosition,
            SteeringAssist: steeringAssist,
            BrakingAssist: brakingAssist,
            GearboxAssist: gearboxAssist,
            PitAssist: pitAssist,
            PitReleaseAssist: pitReleaseAssist,
            ErsAssist: ersAssist,
            DrsAssist: drsAssist,
            DynamicRacingLine: dynamicRacingLine,
            DynamicRacingLineType: dynamicRacingLineType,
            GameMode: gameMode,
            RuleSet: ruleSet,
            TimeOfDay: timeOfDay,
            SessionLength: sessionLength,
            SpeedUnitsLeadPlayer: speedUnitsLeadPlayer,
            TemperatureUnitsLeadPlayer: temperatureUnitsLeadPlayer,
            SpeedUnitsSecondaryPlayer: speedUnitsSecondaryPlayer,
            TemperatureUnitsSecondaryPlayer: temperatureUnitsSecondaryPlayer,
            NumSafetyCarPeriods: numSafetyCarPeriods,
            NumVirtualSafetyCarPeriods: numVirtualSafetyCarPeriods,
            NumRedFlagPeriods: numRedFlagPeriods,
            EqualCarPerformance: equalCarPerformance,
            RecoveryMode: recoveryMode,
            FlashbackLimit: flashbackLimit,
            SurfaceType: surfaceType,
            LowFuelMode: lowFuelMode,
            RaceStarts: raceStarts,
            TyreTemperature: tyreTemperature,
            PitLaneTyreSim: pitLaneTyreSim,
            CarDamage: carDamage,
            CarDamageRate: carDamageRate,
            Collisions: collisions,
            CollisionsOffForFirstLapOnly: collisionsOffForFirstLapOnly,
            MpUnsafePitRelease: mpUnsafePitRelease,
            MpOffForGriefing: mpOffForGriefing,
            CornerCuttingStringency: cornerCuttingStringency,
            ParcFermeRules: parcFermeRules,
            PitStopExperience: pitStopExperience,
            SafetyCar: safetyCar,
            SafetyCarExperience: safetyCarExperience,
            FormationLap: formationLap,
            FormationLapExperience: formationLapExperience,
            RedFlags: redFlags,
            AffectsLicenceLevelSolo: affectsLicenceLevelSolo,
            AffectsLicenceLevelMp: affectsLicenceLevelMp,
            NumSessionsInWeekend: numSessionsInWeekend,
            WeekendStructure: weekendStructure,
            Sector2LapDistanceStart: sector2LapDistanceStart,
            Sector3LapDistanceStart: sector3LapDistanceStart);
    }
}
