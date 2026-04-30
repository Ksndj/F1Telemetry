using F1Telemetry.Udp.Packets;

namespace F1Telemetry.RawLogAnalyzer;

/// <summary>
/// Summarizes decoded packets and chart-relevant samples for one UDP session uid.
/// </summary>
public sealed class RawLogSessionSummary
{
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
    private FinalClassificationData? _playerFinalClassification;

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
            var lap = GetOrCreateLap(car.CurrentLapNumber);
            lap.SampleCount++;
            lap.Position = car.CarPosition;
            lap.ResultStatus = car.ResultStatus;
            lap.IsValid = !car.IsCurrentLapInvalid;
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
    }

    internal void ApplyCarDamagePacket(CarDamagePacket packet, PacketHeader header)
    {
        if (!TryGetPlayerCar(packet.Cars, header.PlayerCarIndex, out var car))
        {
            return;
        }

        TyreWearSampleCount++;
        MaxPlayerTyreWear = Math.Max(MaxPlayerTyreWear, MaxWheelValue(car.TyreWear));
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

    private void AddTyreCompoundPair(byte visualCompound, byte actualCompound)
    {
        TyreCompoundPairs.Add($"visual {visualCompound} / actual {actualCompound}");
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
        public uint? LapTimeInMs { get; set; }

        public int? Position { get; set; }

        public bool? IsValid { get; set; }

        public int? ResultStatus { get; set; }

        public long SampleCount { get; set; }
    }
}
