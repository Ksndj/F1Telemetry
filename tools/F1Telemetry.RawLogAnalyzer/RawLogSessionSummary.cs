using F1Telemetry.Udp.Packets;

namespace F1Telemetry.RawLogAnalyzer;

/// <summary>
/// Summarizes decoded packets and chart-relevant samples for one UDP session uid.
/// </summary>
public sealed class RawLogSessionSummary
{
    private const float JoulesPerMegajoule = 1_000_000f;
    private const float TyreWearDisagreementThresholdPercent = 5f;

    internal RawLogSessionSummary(ulong sessionUid)
    {
        SessionUid = sessionUid;
    }

    public ulong SessionUid { get; }

    public DateTimeOffset? FirstSeenUtc { get; private set; }

    public DateTimeOffset? LastSeenUtc { get; private set; }

    public long DatagramCount { get; private set; }

    public int PlayerCarIndex { get; private set; } = -1;

    public int TrackId { get; private set; } = -1;

    public int SessionType { get; private set; } = -1;

    public int TotalLaps { get; private set; } = -1;

    public long LapSampleCount { get; private set; }

    public int MaxPlayerLapNumber { get; private set; }

    public float MaxPlayerTotalDistance { get; private set; }

    public long SpeedSampleCount { get; private set; }

    public int MaxPlayerSpeed { get; private set; }

    public float MaxPlayerThrottle { get; private set; }

    public long FuelSampleCount { get; private set; }

    public float MinPlayerFuelInTank { get; private set; }

    public float MaxPlayerFuelInTank { get; private set; }

    public int MaxPlayerTyreAgeLaps { get; private set; }

    public long TyreWearSampleCount { get; private set; }

    public float MaxPlayerTyreWear { get; private set; }

    public long PacketParseFailureCount { get; internal set; }

    public long UnknownPacketIdCount { get; internal set; }

    public long UnsupportedPacketIdCount { get; internal set; }

    public Dictionary<PacketId, long> PacketCounts { get; } = new();

    public SortedDictionary<string, long> EventCodeCounts { get; } = new(StringComparer.Ordinal);

    public SortedSet<string> TyreCompoundPairs { get; } = new(StringComparer.Ordinal);

    private readonly SortedDictionary<int, RaceLapAccumulator> _lapSummaries = new();
    private readonly List<TyreStintSnapshot> _sessionHistoryTyreStints = [];
    private FinalClassificationData? _playerFinalClassification;
    private int _latestPlayerLapNumber;

    internal void ObserveDatagram(PacketHeader header, DateTimeOffset timestamp)
    {
        DatagramCount++;
        PlayerCarIndex = header.PlayerCarIndex;

        if (timestamp == DateTimeOffset.MinValue)
        {
            return;
        }

        FirstSeenUtc = FirstSeenUtc is null || timestamp < FirstSeenUtc
            ? timestamp
            : FirstSeenUtc;
        LastSeenUtc = LastSeenUtc is null || timestamp > LastSeenUtc
            ? timestamp
            : LastSeenUtc;
    }

    internal void ApplySessionPacket(SessionPacket packet)
    {
        TrackId = packet.TrackId;
        SessionType = packet.SessionType;
        TotalLaps = packet.TotalLaps;
    }

    internal void ApplyLapDataPacket(LapDataPacket packet, PacketHeader header)
    {
        if (!TryGetPlayerCar(packet.Cars, header.PlayerCarIndex, out var car))
        {
            return;
        }

        LapSampleCount++;
        MaxPlayerLapNumber = Math.Max(MaxPlayerLapNumber, car.CurrentLapNumber);
        MaxPlayerTotalDistance = Math.Max(MaxPlayerTotalDistance, car.TotalDistance);

        if (car.CurrentLapNumber > 0)
        {
            _latestPlayerLapNumber = car.CurrentLapNumber;
            var lap = GetOrCreateLap(car.CurrentLapNumber);
            lap.SampleCount++;
            lap.LastLapTimeInMs = car.LastLapTimeInMs == 0 ? null : car.LastLapTimeInMs;
            lap.CurrentLapTimeInMs = car.CurrentLapTimeInMs == 0 ? null : car.CurrentLapTimeInMs;
            lap.Position = car.CarPosition;
            lap.ResultStatus = car.ResultStatus;
            lap.IsValid = !car.IsCurrentLapInvalid;
            lap.PitStatus = car.PitStatus;
            lap.NumPitStops = car.NumPitStops;
            lap.IsPitLaneTimerActive = car.IsPitLaneTimerActive;
            lap.PitLaneTimeInLaneInMs = car.PitLaneTimeInLaneInMs == 0 ? null : car.PitLaneTimeInLaneInMs;
            lap.PitStopTimerInMs = car.PitStopTimerInMs == 0 ? null : car.PitStopTimerInMs;
        }
    }

    internal void ApplyCarTelemetryPacket(CarTelemetryPacket packet, PacketHeader header)
    {
        if (!TryGetPlayerCar(packet.Cars, header.PlayerCarIndex, out var car))
        {
            return;
        }

        SpeedSampleCount++;
        MaxPlayerSpeed = Math.Max(MaxPlayerSpeed, car.Speed);
        MaxPlayerThrottle = Math.Max(MaxPlayerThrottle, car.Throttle);
    }

    internal void ApplyCarStatusPacket(CarStatusPacket packet, PacketHeader header)
    {
        if (!TryGetPlayerCar(packet.Cars, header.PlayerCarIndex, out var car))
        {
            return;
        }

        if (FuelSampleCount == 0)
        {
            MinPlayerFuelInTank = car.FuelInTank;
            MaxPlayerFuelInTank = car.FuelInTank;
        }
        else
        {
            MinPlayerFuelInTank = Math.Min(MinPlayerFuelInTank, car.FuelInTank);
            MaxPlayerFuelInTank = Math.Max(MaxPlayerFuelInTank, car.FuelInTank);
        }

        FuelSampleCount++;
        MaxPlayerTyreAgeLaps = Math.Max(MaxPlayerTyreAgeLaps, car.TyresAgeLaps);
        AddTyreCompoundPair(car.VisualTyreCompound, car.ActualTyreCompound);

        if (_latestPlayerLapNumber > 0)
        {
            var lap = GetOrCreateLap(_latestPlayerLapNumber);
            lap.FuelKg = car.FuelInTank;
            lap.FuelRemainingLaps = car.FuelRemainingLaps;
            lap.ErsStoreEnergyJoules = car.ErsStoreEnergy;
            lap.ErsDeployMode = car.ErsDeployMode;
            lap.ErsHarvestedThisLapJoules = car.ErsHarvestedThisLapMguk + car.ErsHarvestedThisLapMguh;
            lap.ErsDeployedThisLapJoules = car.ErsDeployedThisLap;
            lap.ActualTyreCompound = car.ActualTyreCompound;
            lap.VisualTyreCompound = car.VisualTyreCompound;
            lap.TyreAgeLaps = car.TyresAgeLaps;
        }
    }

    internal void ApplyCarDamagePacket(CarDamagePacket packet, PacketHeader header)
    {
        if (!TryGetPlayerCar(packet.Cars, header.PlayerCarIndex, out var car))
        {
            return;
        }

        TyreWearSampleCount++;
        MaxPlayerTyreWear = Math.Max(MaxPlayerTyreWear, MaxWheelValue(car.TyreWear));
        if (_latestPlayerLapNumber > 0)
        {
            var lap = GetOrCreateLap(_latestPlayerLapNumber);
            lap.CarDamageTyreWearPercent = MaxWheelValue(car.TyreWear);
        }
    }

    internal void ApplyTyreSetsPacket(TyreSetsPacket packet, PacketHeader header)
    {
        if (packet.CarIndex != header.PlayerCarIndex)
        {
            return;
        }

        foreach (var tyreSet in packet.TyreSets)
        {
            if (!tyreSet.Fitted)
            {
                continue;
            }

            AddTyreCompoundPair(tyreSet.VisualTyreCompound, tyreSet.ActualTyreCompound);
            TyreWearSampleCount++;
            MaxPlayerTyreWear = Math.Max(MaxPlayerTyreWear, tyreSet.Wear);
            if (_latestPlayerLapNumber > 0)
            {
                var lap = GetOrCreateLap(_latestPlayerLapNumber);
                lap.ActualTyreCompound = tyreSet.ActualTyreCompound;
                lap.VisualTyreCompound = tyreSet.VisualTyreCompound;
                lap.TyreSetsWearPercent = tyreSet.Wear;
            }
        }
    }

    internal void ApplyEventPacket(EventPacket packet)
    {
        EventCodeCounts.TryGetValue(packet.RawEventCode, out var current);
        EventCodeCounts[packet.RawEventCode] = current + 1;
    }

    internal void ApplySessionHistoryPacket(SessionHistoryPacket packet, PacketHeader header)
    {
        if (packet.CarIndex != header.PlayerCarIndex)
        {
            return;
        }

        _sessionHistoryTyreStints.Clear();
        var stintCount = Math.Min(packet.NumTyreStints, packet.TyreStints.Length);
        for (var index = 0; index < stintCount; index++)
        {
            var stint = packet.TyreStints[index];
            if (stint.EndLap == 0 && stint.ActualTyreCompound == 0 && stint.VisualTyreCompound == 0)
            {
                continue;
            }

            _sessionHistoryTyreStints.Add(new TyreStintSnapshot(
                EndLap: stint.EndLap,
                ActualTyreCompound: stint.ActualTyreCompound,
                VisualTyreCompound: stint.VisualTyreCompound));
        }

        var lapCount = Math.Min(packet.NumLaps, packet.LapHistory.Length);
        for (var index = 0; index < lapCount; index++)
        {
            var lapNumber = index + 1;
            var lapHistory = packet.LapHistory[index];
            var lap = GetOrCreateLap(lapNumber);
            if (lapHistory.LapTimeInMs > 0)
            {
                lap.LapTimeInMs = lapHistory.LapTimeInMs;
            }

            lap.IsValid = lapHistory.IsLapValid;
        }
    }

    internal void ApplyFinalClassificationPacket(FinalClassificationPacket packet, PacketHeader header)
    {
        if (header.PlayerCarIndex >= packet.Cars.Length)
        {
            return;
        }

        _playerFinalClassification = packet.Cars[header.PlayerCarIndex];
    }

    internal PlayerRaceSummary BuildPlayerRaceSummary()
    {
        if (_playerFinalClassification is null)
        {
            return new PlayerRaceSummary(
                GridPosition: null,
                FinalPosition: null,
                CompletedLaps: null,
                Points: null,
                BestLapTimeInMs: null,
                PenaltiesTimeSeconds: null,
                NumPenalties: null);
        }

        return new PlayerRaceSummary(
            GridPosition: _playerFinalClassification.GridPosition,
            FinalPosition: _playerFinalClassification.Position,
            CompletedLaps: _playerFinalClassification.NumLaps,
            Points: _playerFinalClassification.Points,
            BestLapTimeInMs: _playerFinalClassification.BestLapTimeInMs == 0 ? null : _playerFinalClassification.BestLapTimeInMs,
            PenaltiesTimeSeconds: _playerFinalClassification.PenaltiesTime,
            NumPenalties: _playerFinalClassification.NumPenalties);
    }

    internal IReadOnlyList<RaceLapSummary> BuildRaceLapSummaries()
    {
        return _lapSummaries
            .Select(pair => new RaceLapSummary(
                LapNumber: pair.Key,
                LapTimeInMs: pair.Value.LapTimeInMs,
                Position: pair.Value.Position,
                IsValid: pair.Value.IsValid,
                ResultStatus: pair.Value.ResultStatus,
                SampleCount: pair.Value.SampleCount))
            .ToArray();
    }

    internal IReadOnlyList<StintSummary> BuildStintSummaries()
    {
        var completedLaps = GetCompletedLapCount();
        if (_sessionHistoryTyreStints.Count > 0)
        {
            return BuildStintsFromBoundaries(
                _sessionHistoryTyreStints,
                StintSummarySource.SessionHistory,
                RaceAnalysisConfidence.High,
                completedLaps);
        }

        var finalClassificationStints = BuildFinalClassificationStintSnapshots();
        if (finalClassificationStints.Count > 0)
        {
            return BuildStintsFromBoundaries(
                finalClassificationStints,
                StintSummarySource.FinalClassification,
                RaceAnalysisConfidence.High,
                completedLaps);
        }

        var compoundStints = BuildCompoundChangeStints(completedLaps);
        if (compoundStints.Count > 0)
        {
            return compoundStints;
        }

        return BuildPitStopInferredStints(completedLaps);
    }

    internal IReadOnlyList<PitStopSummary> BuildPitStopSummaries()
    {
        var summaries = new List<PitStopSummary>();
        var orderedLaps = _lapSummaries.OrderBy(pair => pair.Key).ToArray();
        int? previousPitStops = null;

        foreach (var pair in orderedLaps)
        {
            var lap = pair.Value;
            if (lap.NumPitStops is not null)
            {
                if (previousPitStops is not null && lap.NumPitStops.Value > previousPitStops.Value)
                {
                    var tyreChanged = HasTyreChangedAround(pair.Key);
                    var hasPitEvidence = lap.HasPitEvidence;
                    var confidence = hasPitEvidence && tyreChanged
                        ? RaceAnalysisConfidence.High
                        : RaceAnalysisConfidence.Medium;
                    var notes = confidence == RaceAnalysisConfidence.High
                        ? "confirmed from NumPitStops, pit lane evidence, and tyre change."
                        : "confirmed from NumPitStops or pit lane evidence; tyre change evidence is incomplete.";
                    summaries.Add(CreatePitStopSummary(pair.Key, confidence, notes));
                }

                previousPitStops = previousPitStops is null
                    ? lap.NumPitStops.Value
                    : Math.Max(previousPitStops.Value, lap.NumPitStops.Value);
            }
            else if (lap.HasPitEvidence && summaries.All(summary => summary.PitLap != pair.Key))
            {
                summaries.Add(CreatePitStopSummary(
                    pair.Key,
                    RaceAnalysisConfidence.Medium,
                    "confirmed from pit lane evidence; NumPitStops was unavailable."));
            }
        }

        if (summaries.Count == 0)
        {
            summaries.AddRange(BuildPossibleSlowLapPitStops(orderedLaps));
        }

        return summaries;
    }

    internal IReadOnlyList<TyreUsageSummary> BuildTyreUsageSummaries(IReadOnlyList<StintSummary> stintSummaries)
    {
        var summaries = new List<TyreUsageSummary>();
        foreach (var stint in stintSummaries)
        {
            var observed = _lapSummaries
                .Where(pair => pair.Key >= stint.StartLap && pair.Key <= stint.EndLap)
                .OrderBy(pair => pair.Key)
                .Select(pair => new
                {
                    Wear = pair.Value.PrimaryTyreWearPercent,
                    HasCarDamage = pair.Value.CarDamageTyreWearPercent is not null,
                    HasTyreSets = pair.Value.TyreSetsWearPercent is not null,
                    Disagrees = pair.Value.HasTyreWearDisagreement
                })
                .Where(sample => sample.Wear is not null)
                .ToArray();

            var startWear = observed.FirstOrDefault()?.Wear;
            var endWear = observed.LastOrDefault()?.Wear;
            var maxWear = observed.Length == 0 ? null : observed.Max(sample => sample.Wear);
            var delta = startWear is not null && endWear is not null
                ? endWear.Value - startWear.Value
                : (float?)null;
            var averagePerLap = delta is not null && stint.LapCount > 0
                ? delta.Value / stint.LapCount
                : (float?)null;
            var hasCarDamage = observed.Any(sample => sample.HasCarDamage);
            var hasTyreSets = observed.Any(sample => sample.HasTyreSets);
            var hasDisagreement = observed.Any(sample => sample.Disagrees);

            summaries.Add(new TyreUsageSummary(
                StintIndex: stint.StintIndex,
                StartLap: stint.StartLap,
                EndLap: stint.EndLap,
                LapCount: stint.LapCount,
                ActualTyreCompound: stint.ActualTyreCompound,
                VisualTyreCompound: stint.VisualTyreCompound,
                StartTyreAge: stint.StartTyreAge,
                EndTyreAge: stint.EndTyreAge,
                StartWearPercent: startWear,
                EndWearPercent: endWear,
                MaxWearPercent: maxWear,
                WearDeltaPercent: delta,
                AverageWearPerLapPercent: averagePerLap,
                ObservedLapCount: observed.Length,
                Risk: GetTyreRisk(maxWear, averagePerLap),
                Confidence: GetTyreConfidence(observed.Length, hasCarDamage),
                Notes: BuildTyreUsageNotes(observed.Length, hasCarDamage, hasTyreSets, hasDisagreement)));
        }

        return summaries;
    }

    internal FuelTrendSummary BuildFuelTrendSummary()
    {
        var observed = _lapSummaries
            .Where(IsCompletedRaceLap)
            .Where(pair => pair.Value.FuelKg is not null)
            .OrderBy(pair => pair.Key)
            .Select(pair => pair.Value)
            .ToArray();
        if (observed.Length == 0)
        {
            return new FuelTrendSummary(
                StartFuelKg: null,
                EndFuelKg: null,
                MinFuelKg: null,
                MaxFuelKg: null,
                FuelUsedKg: null,
                AverageFuelPerLapKg: null,
                StartFuelRemainingLaps: null,
                EndFuelRemainingLaps: null,
                MinFuelRemainingLaps: null,
                ObservedLapCount: 0,
                Risk: RaceTrendRisk.Unknown,
                Confidence: RaceAnalysisConfidence.Low,
                Notes: "No player CarStatus fuel samples were decoded.");
        }

        var startFuel = observed[0].FuelKg;
        var endFuel = observed[^1].FuelKg;
        var fuelUsed = startFuel is not null && endFuel is not null
            ? startFuel.Value - endFuel.Value
            : (float?)null;
        var averageFuelPerLap = fuelUsed is not null && observed.Length > 0
            ? fuelUsed.Value / observed.Length
            : (float?)null;
        var remainingLaps = observed
            .Where(lap => lap.FuelRemainingLaps is not null)
            .Select(lap => lap.FuelRemainingLaps!.Value)
            .ToArray();

        return new FuelTrendSummary(
            StartFuelKg: startFuel,
            EndFuelKg: endFuel,
            MinFuelKg: observed.Min(lap => lap.FuelKg),
            MaxFuelKg: observed.Max(lap => lap.FuelKg),
            FuelUsedKg: fuelUsed,
            AverageFuelPerLapKg: averageFuelPerLap,
            StartFuelRemainingLaps: observed[0].FuelRemainingLaps,
            EndFuelRemainingLaps: observed[^1].FuelRemainingLaps,
            MinFuelRemainingLaps: remainingLaps.Length == 0 ? null : remainingLaps.Min(),
            ObservedLapCount: observed.Length,
            Risk: GetFuelRisk(remainingLaps.Length == 0 ? null : remainingLaps.Min()),
            Confidence: RaceAnalysisConfidence.High,
            Notes: remainingLaps.Length == 0
                ? "CarStatus.FuelInTank is reported as kg; fuel remaining laps were unavailable."
                : "CarStatus.FuelInTank is reported as kg.");
    }

    internal ErsTrendSummary BuildErsTrendSummary()
    {
        var observed = _lapSummaries
            .Where(IsCompletedRaceLap)
            .Where(pair => pair.Value.HasErsData)
            .OrderBy(pair => pair.Key)
            .Select(pair => pair.Value)
            .ToArray();
        if (observed.Length == 0)
        {
            return new ErsTrendSummary(
                StartStoreEnergyMJ: null,
                EndStoreEnergyMJ: null,
                MinStoreEnergyMJ: null,
                MaxStoreEnergyMJ: null,
                NetStoreEnergyDeltaMJ: null,
                AverageHarvestedPerLapMJ: null,
                AverageDeployedPerLapMJ: null,
                LastDeployMode: null,
                LowErsLapCount: 0,
                HighUsageLaps: 0,
                RecoveryLaps: 0,
                ObservedLapCount: 0,
                Risk: RaceTrendRisk.Unknown,
                Confidence: RaceAnalysisConfidence.Low,
                Notes: "No player CarStatus ERS samples were decoded.");
        }

        var startStore = ToMegajoules(observed[0].ErsStoreEnergyJoules);
        var endStore = ToMegajoules(observed[^1].ErsStoreEnergyJoules);
        var storeValues = observed
            .Select(lap => ToMegajoules(lap.ErsStoreEnergyJoules))
            .Where(value => value is not null)
            .Select(value => value!.Value)
            .ToArray();
        var harvestedValues = observed
            .Select(lap => ToMegajoules(lap.ErsHarvestedThisLapJoules))
            .Where(value => value is not null)
            .Select(value => value!.Value)
            .ToArray();
        var deployedValues = observed
            .Select(lap => ToMegajoules(lap.ErsDeployedThisLapJoules))
            .Where(value => value is not null)
            .Select(value => value!.Value)
            .ToArray();

        return new ErsTrendSummary(
            StartStoreEnergyMJ: startStore,
            EndStoreEnergyMJ: endStore,
            MinStoreEnergyMJ: storeValues.Length == 0 ? null : storeValues.Min(),
            MaxStoreEnergyMJ: storeValues.Length == 0 ? null : storeValues.Max(),
            NetStoreEnergyDeltaMJ: startStore is not null && endStore is not null
                ? endStore.Value - startStore.Value
                : null,
            AverageHarvestedPerLapMJ: harvestedValues.Length == 0 ? null : harvestedValues.Average(),
            AverageDeployedPerLapMJ: deployedValues.Length == 0 ? null : deployedValues.Average(),
            LastDeployMode: observed.LastOrDefault(lap => lap.ErsDeployMode is not null)?.ErsDeployMode,
            LowErsLapCount: storeValues.Count(value => value < 1f),
            HighUsageLaps: observed.Count(lap => lap.ErsDeployedThisLapJoules is not null
                && lap.ErsHarvestedThisLapJoules is not null
                && lap.ErsDeployedThisLapJoules.Value > lap.ErsHarvestedThisLapJoules.Value),
            RecoveryLaps: observed.Count(lap => lap.ErsDeployedThisLapJoules is not null
                && lap.ErsHarvestedThisLapJoules is not null
                && lap.ErsHarvestedThisLapJoules.Value > lap.ErsDeployedThisLapJoules.Value),
            ObservedLapCount: observed.Length,
            Risk: GetErsRisk(storeValues.Length == 0 ? null : storeValues.Min()),
            Confidence: RaceAnalysisConfidence.High,
            Notes: "ERS summary is aggregate only; gap-context decisions are out of scope for M3.");
    }

    private void AddTyreCompoundPair(byte visualCompound, byte actualCompound)
    {
        TyreCompoundPairs.Add($"visual {visualCompound} / actual {actualCompound}");
    }

    private static RaceTrendRisk GetTyreRisk(float? maxWear, float? averageWearPerLap)
    {
        if (maxWear is null && averageWearPerLap is null)
        {
            return RaceTrendRisk.Unknown;
        }

        if (maxWear.GetValueOrDefault() >= 70f || averageWearPerLap.GetValueOrDefault() >= 5f)
        {
            return RaceTrendRisk.High;
        }

        if (maxWear.GetValueOrDefault() >= 50f || averageWearPerLap.GetValueOrDefault() >= 3f)
        {
            return RaceTrendRisk.Medium;
        }

        return RaceTrendRisk.Low;
    }

    private static RaceTrendRisk GetFuelRisk(float? minFuelRemainingLaps)
    {
        if (minFuelRemainingLaps is null)
        {
            return RaceTrendRisk.Unknown;
        }

        if (minFuelRemainingLaps.Value < 0.5f)
        {
            return RaceTrendRisk.High;
        }

        if (minFuelRemainingLaps.Value < 1.5f)
        {
            return RaceTrendRisk.Medium;
        }

        return RaceTrendRisk.Low;
    }

    private static RaceTrendRisk GetErsRisk(float? minStoreEnergyMJ)
    {
        if (minStoreEnergyMJ is null)
        {
            return RaceTrendRisk.Unknown;
        }

        if (minStoreEnergyMJ.Value < 0.5f)
        {
            return RaceTrendRisk.High;
        }

        if (minStoreEnergyMJ.Value < 1.0f)
        {
            return RaceTrendRisk.Medium;
        }

        return RaceTrendRisk.Low;
    }

    private static RaceAnalysisConfidence GetTyreConfidence(int observedLapCount, bool hasCarDamage)
    {
        if (observedLapCount == 0)
        {
            return RaceAnalysisConfidence.Low;
        }

        return hasCarDamage
            ? RaceAnalysisConfidence.High
            : RaceAnalysisConfidence.Medium;
    }

    private static string BuildTyreUsageNotes(
        int observedLapCount,
        bool hasCarDamage,
        bool hasTyreSets,
        bool hasDisagreement)
    {
        if (observedLapCount == 0)
        {
            return "No player tyre wear samples were decoded for this stint.";
        }

        var notes = hasCarDamage
            ? "Tyre wear uses player CarDamage.TyreWear as primary."
            : "Tyre wear uses fitted TyreSets.Wear fallback; CarDamage samples were unavailable.";
        if (hasCarDamage && hasTyreSets)
        {
            notes += " Fitted TyreSets.Wear was used only as supplemental evidence.";
        }

        if (hasDisagreement)
        {
            notes += $" TyreSets wear disagreed with CarDamage by at least {TyreWearDisagreementThresholdPercent:0.#} percentage points; CarDamage was kept as primary.";
        }

        return notes;
    }

    private static float? ToMegajoules(float? joules)
    {
        return joules is null ? null : joules.Value / JoulesPerMegajoule;
    }

    private RaceLapAccumulator GetOrCreateLap(int lapNumber)
    {
        if (!_lapSummaries.TryGetValue(lapNumber, out var lap))
        {
            lap = new RaceLapAccumulator();
            _lapSummaries[lapNumber] = lap;
        }

        return lap;
    }

    private IReadOnlyList<TyreStintSnapshot> BuildFinalClassificationStintSnapshots()
    {
        if (_playerFinalClassification is null || _playerFinalClassification.NumTyreStints == 0)
        {
            return [];
        }

        var count = Math.Min((int)_playerFinalClassification.NumTyreStints, UdpPacketConstants.MaxFinalClassificationTyreStints);
        var stints = new List<TyreStintSnapshot>();
        for (var index = 0; index < count; index++)
        {
            var endLap = _playerFinalClassification.TyreStintsEndLaps[index];
            var actual = _playerFinalClassification.TyreStintsActual[index];
            var visual = _playerFinalClassification.TyreStintsVisual[index];
            if (endLap == 0 && actual == 0 && visual == 0)
            {
                continue;
            }

            stints.Add(new TyreStintSnapshot(endLap, actual, visual));
        }

        return stints;
    }

    private IReadOnlyList<StintSummary> BuildStintsFromBoundaries(
        IReadOnlyList<TyreStintSnapshot> boundaries,
        StintSummarySource source,
        RaceAnalysisConfidence confidence,
        int completedLaps)
    {
        var stints = new List<StintSummary>();
        var previousEndLap = 0;
        foreach (var boundary in boundaries)
        {
            var startLap = previousEndLap + 1;
            var endLap = NormalizeStintEndLap(boundary.EndLap, completedLaps, startLap);
            if (endLap < startLap)
            {
                continue;
            }

            var notes = BuildStintBoundaryNote(source, boundary.EndLap, endLap);
            stints.Add(new StintSummary(
                StintIndex: stints.Count + 1,
                StartLap: startLap,
                EndLap: endLap,
                LapCount: endLap - startLap + 1,
                ActualTyreCompound: boundary.ActualTyreCompound,
                VisualTyreCompound: boundary.VisualTyreCompound,
                StartTyreAge: GetTyreAge(startLap),
                EndTyreAge: GetTyreAge(endLap),
                Source: source,
                Confidence: confidence,
                Notes: notes));
            previousEndLap = endLap;
        }

        return stints;
    }

    private IReadOnlyList<StintSummary> BuildCompoundChangeStints(int completedLaps)
    {
        var compoundLaps = _lapSummaries
            .Where(pair => pair.Value.HasCompound)
            .OrderBy(pair => pair.Key)
            .ToArray();
        if (compoundLaps.Length == 0)
        {
            return [];
        }

        var stints = new List<StintSummary>();
        var startLap = compoundLaps[0].Key;
        var currentActual = compoundLaps[0].Value.ActualTyreCompound;
        var currentVisual = compoundLaps[0].Value.VisualTyreCompound;

        for (var index = 1; index < compoundLaps.Length; index++)
        {
            var lapNumber = compoundLaps[index].Key;
            var lap = compoundLaps[index].Value;
            if (lap.ActualTyreCompound == currentActual && lap.VisualTyreCompound == currentVisual)
            {
                continue;
            }

            stints.Add(CreateInferredStint(
                stints.Count + 1,
                startLap,
                lapNumber - 1,
                currentActual,
                currentVisual,
                StintSummarySource.CompoundChangeInference,
                RaceAnalysisConfidence.Medium,
                "inferred from player compound changes; pit stop evidence is incomplete."));
            startLap = lapNumber;
            currentActual = lap.ActualTyreCompound;
            currentVisual = lap.VisualTyreCompound;
        }

        var finalEndLap = completedLaps > 0 ? completedLaps : compoundLaps[^1].Key;
        stints.Add(CreateInferredStint(
            stints.Count + 1,
            startLap,
            finalEndLap,
            currentActual,
            currentVisual,
            StintSummarySource.CompoundChangeInference,
            RaceAnalysisConfidence.Medium,
            "inferred from player compound changes; pit stop evidence is incomplete."));
        return stints;
    }

    private IReadOnlyList<StintSummary> BuildPitStopInferredStints(int completedLaps)
    {
        var pitStops = BuildPitStopSummaries()
            .Where(summary => summary.Confidence != RaceAnalysisConfidence.Low)
            .OrderBy(summary => summary.PitLap)
            .ToArray();
        if (pitStops.Length == 0 || completedLaps <= 0)
        {
            return [];
        }

        var stints = new List<StintSummary>();
        var startLap = 1;
        foreach (var pitStop in pitStops)
        {
            var endLap = Math.Max(startLap, pitStop.PitLap - 1);
            stints.Add(CreateInferredStint(
                stints.Count + 1,
                startLap,
                endLap,
                actualTyreCompound: null,
                visualTyreCompound: null,
                StintSummarySource.PitStopInference,
                RaceAnalysisConfidence.Medium,
                "inferred from pit stop evidence; tyre compound data is unavailable."));
            startLap = pitStop.PitLap;
        }

        stints.Add(CreateInferredStint(
            stints.Count + 1,
            startLap,
            completedLaps,
            actualTyreCompound: null,
            visualTyreCompound: null,
            StintSummarySource.PitStopInference,
            RaceAnalysisConfidence.Medium,
            "inferred from pit stop evidence; tyre compound data is unavailable."));
        return stints;
    }

    private StintSummary CreateInferredStint(
        int stintIndex,
        int startLap,
        int endLap,
        int? actualTyreCompound,
        int? visualTyreCompound,
        StintSummarySource source,
        RaceAnalysisConfidence confidence,
        string notes)
    {
        return new StintSummary(
            StintIndex: stintIndex,
            StartLap: startLap,
            EndLap: endLap,
            LapCount: endLap - startLap + 1,
            ActualTyreCompound: actualTyreCompound,
            VisualTyreCompound: visualTyreCompound,
            StartTyreAge: GetTyreAge(startLap),
            EndTyreAge: GetTyreAge(endLap),
            Source: source,
            Confidence: confidence,
            Notes: notes);
    }

    private PitStopSummary CreatePitStopSummary(
        int pitLap,
        RaceAnalysisConfidence confidence,
        string notes)
    {
        var before = FindLapAtOrBefore(pitLap - 1);
        var current = FindLapAtOrBefore(pitLap);
        var after = FindLapAtOrAfter(pitLap + 1) ?? current;
        var compoundBefore = FindLapWithCompoundAtOrBefore(pitLap - 1);
        var compoundAfter = FindLapWithCompoundAtOrAfter(pitLap) ?? compoundBefore;
        var tyreAgeBefore = compoundBefore?.TyreAgeLaps;
        var tyreAgeAfter = compoundAfter?.TyreAgeLaps;
        var positionBefore = before?.Position;
        var positionAfter = current?.Position ?? after?.Position;
        var positionLost = positionBefore is not null && positionAfter is not null
            ? positionAfter.Value - positionBefore.Value
            : (int?)null;

        return new PitStopSummary(
            PitLap: pitLap,
            EntryLapTimeInMs: current?.LapTimeInMs ?? current?.LastLapTimeInMs,
            ExitLapTimeInMs: after?.LapTimeInMs ?? after?.LastLapTimeInMs,
            CompoundBefore: FormatCompound(compoundBefore),
            CompoundAfter: FormatCompound(compoundAfter),
            TyreAgeBefore: tyreAgeBefore,
            TyreAgeAfter: tyreAgeAfter,
            PositionBefore: positionBefore,
            PositionAfter: positionAfter,
            PositionLost: positionLost,
            EstimatedPitLossInMs: null,
            Confidence: confidence,
            Notes: notes + " Estimated pit loss unavailable; no reliable baseline.");
    }

    private IReadOnlyList<PitStopSummary> BuildPossibleSlowLapPitStops(
        KeyValuePair<int, RaceLapAccumulator>[] orderedLaps)
    {
        var summaries = new List<PitStopSummary>();
        for (var index = 1; index < orderedLaps.Length - 1; index++)
        {
            var previous = orderedLaps[index - 1].Value;
            var current = orderedLaps[index].Value;
            var next = orderedLaps[index + 1].Value;
            if (previous.LapTimeInMs is null || current.LapTimeInMs is null || next.LapTimeInMs is null)
            {
                continue;
            }

            var neighborBaseline = Math.Max(previous.LapTimeInMs.Value, next.LapTimeInMs.Value);
            var positionLost = previous.Position is not null && current.Position is not null
                ? current.Position.Value - previous.Position.Value
                : 0;
            if (current.LapTimeInMs.Value <= neighborBaseline + 25000 || positionLost <= 0)
            {
                continue;
            }

            summaries.Add(CreatePitStopSummary(
                orderedLaps[index].Key,
                RaceAnalysisConfidence.Low,
                "possible pit stop inferred only from slow lap and position loss; not confirmed."));
            break;
        }

        return summaries;
    }

    private bool HasTyreChangedAround(int pitLap)
    {
        var before = FindLapWithCompoundAtOrBefore(pitLap - 1);
        var after = FindLapWithCompoundAtOrAfter(pitLap);
        if (before is null || after is null)
        {
            return false;
        }

        if (before.ActualTyreCompound != after.ActualTyreCompound ||
            before.VisualTyreCompound != after.VisualTyreCompound)
        {
            return true;
        }

        return before.TyreAgeLaps is not null &&
               after.TyreAgeLaps is not null &&
               after.TyreAgeLaps.Value < before.TyreAgeLaps.Value;
    }

    private int GetCompletedLapCount()
    {
        var finalLaps = _playerFinalClassification?.NumLaps ?? 0;
        return finalLaps > 0 ? finalLaps : MaxPlayerLapNumber;
    }

    private bool IsCompletedRaceLap(KeyValuePair<int, RaceLapAccumulator> pair)
    {
        var completedLaps = GetCompletedLapCount();
        return completedLaps <= 0 || pair.Key <= completedLaps;
    }

    private int NormalizeStintEndLap(byte rawEndLap, int completedLaps, int startLap)
    {
        var endLap = rawEndLap is 0 or 255
            ? completedLaps
            : rawEndLap;
        if (completedLaps > 0)
        {
            endLap = Math.Min(endLap, completedLaps);
        }

        return Math.Max(endLap, startLap);
    }

    private static string BuildStintBoundaryNote(StintSummarySource source, byte rawEndLap, int endLap)
    {
        var prefix = source switch
        {
            StintSummarySource.SessionHistory => "confirmed from SessionHistory.",
            StintSummarySource.FinalClassification => "confirmed from FinalClassification.",
            _ => "inferred."
        };

        return rawEndLap == 255
            ? $"{prefix} raw end lap 255 truncated to completed lap {endLap}."
            : prefix;
    }

    private int? GetTyreAge(int lapNumber)
    {
        return _lapSummaries.TryGetValue(lapNumber, out var lap)
            ? lap.TyreAgeLaps
            : null;
    }

    private RaceLapAccumulator? FindLapAtOrBefore(int lapNumber)
    {
        return _lapSummaries
            .Where(pair => pair.Key <= lapNumber)
            .OrderByDescending(pair => pair.Key)
            .Select(pair => pair.Value)
            .FirstOrDefault();
    }

    private RaceLapAccumulator? FindLapAtOrAfter(int lapNumber)
    {
        return _lapSummaries
            .Where(pair => pair.Key >= lapNumber)
            .OrderBy(pair => pair.Key)
            .Select(pair => pair.Value)
            .FirstOrDefault();
    }

    private RaceLapAccumulator? FindLapWithCompoundAtOrBefore(int lapNumber)
    {
        return _lapSummaries
            .Where(pair => pair.Key <= lapNumber && pair.Value.HasCompound)
            .OrderByDescending(pair => pair.Key)
            .Select(pair => pair.Value)
            .FirstOrDefault();
    }

    private RaceLapAccumulator? FindLapWithCompoundAtOrAfter(int lapNumber)
    {
        return _lapSummaries
            .Where(pair => pair.Key >= lapNumber && pair.Value.HasCompound)
            .OrderBy(pair => pair.Key)
            .Select(pair => pair.Value)
            .FirstOrDefault();
    }

    private static string? FormatCompound(RaceLapAccumulator? lap)
    {
        return lap?.HasCompound == true
            ? $"visual {lap.VisualTyreCompound} / actual {lap.ActualTyreCompound}"
            : null;
    }

    private static bool TryGetPlayerCar<T>(T[] cars, byte playerCarIndex, out T car)
    {
        if (playerCarIndex < cars.Length)
        {
            car = cars[playerCarIndex];
            return true;
        }

        car = default!;
        return false;
    }

    private static float MaxWheelValue(WheelSet<float> values)
    {
        return Math.Max(
            Math.Max(values.RearLeft, values.RearRight),
            Math.Max(values.FrontLeft, values.FrontRight));
    }

    private sealed class RaceLapAccumulator
    {
        public uint? LastLapTimeInMs { get; set; }

        public uint? CurrentLapTimeInMs { get; set; }

        public uint? LapTimeInMs { get; set; }

        public int? Position { get; set; }

        public bool? IsValid { get; set; }

        public int? ResultStatus { get; set; }

        public int? PitStatus { get; set; }

        public int? NumPitStops { get; set; }

        public bool IsPitLaneTimerActive { get; set; }

        public int? PitLaneTimeInLaneInMs { get; set; }

        public int? PitStopTimerInMs { get; set; }

        public int? ActualTyreCompound { get; set; }

        public int? VisualTyreCompound { get; set; }

        public int? TyreAgeLaps { get; set; }

        public float? FuelKg { get; set; }

        public float? FuelRemainingLaps { get; set; }

        public float? ErsStoreEnergyJoules { get; set; }

        public int? ErsDeployMode { get; set; }

        public float? ErsHarvestedThisLapJoules { get; set; }

        public float? ErsDeployedThisLapJoules { get; set; }

        public float? CarDamageTyreWearPercent { get; set; }

        public float? TyreSetsWearPercent { get; set; }

        public long SampleCount { get; set; }

        public bool HasCompound => ActualTyreCompound is not null || VisualTyreCompound is not null;

        public float? PrimaryTyreWearPercent => CarDamageTyreWearPercent ?? TyreSetsWearPercent;

        public bool HasErsData =>
            ErsStoreEnergyJoules is not null ||
            ErsHarvestedThisLapJoules is not null ||
            ErsDeployedThisLapJoules is not null ||
            ErsDeployMode is not null;

        public bool HasTyreWearDisagreement =>
            CarDamageTyreWearPercent is not null &&
            TyreSetsWearPercent is not null &&
            Math.Abs(CarDamageTyreWearPercent.Value - TyreSetsWearPercent.Value) >= TyreWearDisagreementThresholdPercent;

        public bool HasPitEvidence =>
            PitStatus.GetValueOrDefault() > 0 ||
            IsPitLaneTimerActive ||
            PitLaneTimeInLaneInMs.GetValueOrDefault() > 0 ||
            PitStopTimerInMs.GetValueOrDefault() > 0;
    }

    private sealed record TyreStintSnapshot(
        byte EndLap,
        byte ActualTyreCompound,
        byte VisualTyreCompound);
}
