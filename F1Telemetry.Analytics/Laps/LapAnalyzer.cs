using System.Threading;
using F1Telemetry.Analytics.Interfaces;
using F1Telemetry.Core.Models;
using F1Telemetry.Udp.Packets;

namespace F1Telemetry.Analytics.Laps;

/// <summary>
/// Maintains player-lap builders and a full in-memory lap summary history for the current session.
/// </summary>
public sealed class LapAnalyzer : ILapAnalyzer
{
    private readonly object _syncRoot = new();
    private LapBuilder? _currentLapBuilder;
    private SessionHistoryPacket? _latestPlayerHistory;
    private LapSummary[] _allLaps = Array.Empty<LapSummary>();
    private LapSummary? _bestLap;
    private LapSummary? _lastLap;
    private WheelSet<float>? _latestPlayerTyreWearPerWheel;
    private uint _lastFrameIdentifier;
    private bool _hasSeenFrame;

    /// <inheritdoc />
    public void Observe(ParsedPacket parsedPacket, SessionState sessionState)
    {
        ArgumentNullException.ThrowIfNull(parsedPacket);
        ArgumentNullException.ThrowIfNull(sessionState);

        if (sessionState.PlayerCarIndex is null)
        {
            return;
        }

        lock (_syncRoot)
        {
            if (parsedPacket.Packet is SessionHistoryPacket historyPacket)
            {
                ObserveSessionHistory(historyPacket, sessionState.PlayerCarIndex.Value);
                return;
            }

            if (parsedPacket.Packet is CarDamagePacket damagePacket
                && sessionState.PlayerCarIndex.Value < damagePacket.Cars.Length)
            {
                _latestPlayerTyreWearPerWheel = damagePacket.Cars[sessionState.PlayerCarIndex.Value].TyreWear;
            }

            if (!ShouldSamplePacket(parsedPacket.Packet))
            {
                return;
            }

            var sample = TryCreateSample(parsedPacket, sessionState.PlayerCar);
            if (sample is null)
            {
                return;
            }

            if (ShouldResetForRegression(sample))
            {
                _currentLapBuilder = new LapBuilder(sample, shouldEmitWhenClosed: false);
                _lastFrameIdentifier = sample.FrameIdentifier;
                _hasSeenFrame = true;
                return;
            }

            if (_currentLapBuilder is null)
            {
                _currentLapBuilder = new LapBuilder(sample, shouldEmitWhenClosed: IsCleanLapStart(sample));
                _lastFrameIdentifier = sample.FrameIdentifier;
                _hasSeenFrame = true;
                return;
            }

            if (sample.LapNumber == _currentLapBuilder.LapNumber)
            {
                _currentLapBuilder.AddSample(sample);
                _lastFrameIdentifier = sample.FrameIdentifier;
                _hasSeenFrame = true;
                return;
            }

            if (sample.LapNumber > _currentLapBuilder.LapNumber)
            {
                CloseCurrentLap(sample);
                _currentLapBuilder = new LapBuilder(sample, shouldEmitWhenClosed: IsCleanLapStart(sample));
                _lastFrameIdentifier = sample.FrameIdentifier;
                _hasSeenFrame = true;
                return;
            }

            _currentLapBuilder = new LapBuilder(sample, shouldEmitWhenClosed: false);
            _lastFrameIdentifier = sample.FrameIdentifier;
            _hasSeenFrame = true;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<LapSummary> CaptureAllLaps()
    {
        return Volatile.Read(ref _allLaps);
    }

    /// <inheritdoc />
    public IReadOnlyList<LapSample> CaptureCurrentLapSamples()
    {
        lock (_syncRoot)
        {
            return _currentLapBuilder?.CaptureSamples() ?? Array.Empty<LapSample>();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<LapSummary> CaptureRecentLaps(int maxCount)
    {
        if (maxCount <= 0)
        {
            return Array.Empty<LapSummary>();
        }

        var allLaps = Volatile.Read(ref _allLaps);
        if (allLaps.Length <= maxCount)
        {
            return allLaps.Reverse().ToArray();
        }

        var recent = new LapSummary[maxCount];
        var startIndex = allLaps.Length - 1;
        for (var index = 0; index < maxCount; index++)
        {
            recent[index] = allLaps[startIndex - index];
        }

        return recent;
    }

    /// <inheritdoc />
    public LapSummary? CaptureBestLap()
    {
        return Volatile.Read(ref _bestLap);
    }

    /// <inheritdoc />
    public LapSummary? CaptureLastLap()
    {
        return Volatile.Read(ref _lastLap);
    }

    private void ObserveSessionHistory(SessionHistoryPacket packet, byte playerCarIndex)
    {
        if (packet.CarIndex != playerCarIndex)
        {
            return;
        }

        _latestPlayerHistory = packet;

        var currentLaps = Volatile.Read(ref _allLaps);
        if (currentLaps.Length == 0)
        {
            return;
        }

        var updated = currentLaps
            .Select(summary => TryApplyOfficialTiming(summary, packet))
            .ToArray();

        PublishLaps(updated);
    }

    private void CloseCurrentLap(LapSample closingSample)
    {
        var builder = _currentLapBuilder;
        if (builder is null || !builder.ShouldEmitWhenClosed)
        {
            return;
        }

        var summary = builder.BuildSummary(closingSample.SampledAt, closingSample);
        if (_latestPlayerHistory is not null)
        {
            summary = TryApplyOfficialTiming(summary, _latestPlayerHistory);
        }

        var updated = Volatile.Read(ref _allLaps)
            .Concat(new[] { summary })
            .ToArray();

        PublishLaps(updated);
    }

    private void PublishLaps(LapSummary[] laps)
    {
        Volatile.Write(ref _allLaps, laps);
        Volatile.Write(ref _lastLap, laps.LastOrDefault());
        Volatile.Write(ref _bestLap, laps
            .Where(lap => lap.LapTimeInMs is not null)
            .OrderBy(lap => lap.LapTimeInMs)
            .ThenBy(lap => lap.LapNumber)
            .FirstOrDefault());
    }

    private bool ShouldResetForRegression(LapSample sample)
    {
        if (!_hasSeenFrame)
        {
            return false;
        }

        if (sample.FrameIdentifier < _lastFrameIdentifier)
        {
            return true;
        }

        var current = _currentLapBuilder;
        if (current is null)
        {
            return false;
        }

        if (sample.LapNumber < current.LapNumber)
        {
            return true;
        }

        if (sample.LapNumber != current.LapNumber)
        {
            return false;
        }

        var last = current.LastSample;
        if (sample.LapDistance is not null && last.LapDistance is not null && sample.LapDistance.Value + 250f < last.LapDistance.Value)
        {
            return true;
        }

        if (sample.CurrentLapTimeInMs is not null
            && last.CurrentLapTimeInMs is not null
            && sample.CurrentLapTimeInMs.Value + 3_000 < last.CurrentLapTimeInMs.Value)
        {
            return true;
        }

        return false;
    }

    private static bool ShouldSamplePacket(IUdpPacket packet)
    {
        return packet is LapDataPacket
            or CarTelemetryPacket
            or CarStatusPacket
            or CarDamagePacket
            or MotionPacket
            or TyreSetsPacket;
    }

    private static bool IsCleanLapStart(LapSample sample)
    {
        return (sample.LapDistance ?? float.MaxValue) <= 150f
            || (sample.CurrentLapTimeInMs ?? uint.MaxValue) <= 5_000u;
    }

    private LapSample? TryCreateSample(ParsedPacket parsedPacket, CarSnapshot? playerCar)
    {
        if (playerCar?.CurrentLapNumber is null)
        {
            return null;
        }

        return new LapSample
        {
            SampledAt = parsedPacket.Datagram.ReceivedAt,
            FrameIdentifier = parsedPacket.Header.FrameIdentifier,
            LapNumber = playerCar.CurrentLapNumber.Value,
            LapDistance = playerCar.LapDistance,
            TotalDistance = playerCar.TotalDistance,
            CurrentLapTimeInMs = playerCar.CurrentLapTimeInMs,
            LastLapTimeInMs = playerCar.LastLapTimeInMs,
            SpeedKph = playerCar.Telemetry?.SpeedKph,
            Throttle = playerCar.Telemetry?.Throttle,
            Brake = playerCar.Telemetry?.Brake,
            Steering = playerCar.SteeringInput,
            Gear = playerCar.Gear,
            FuelRemaining = playerCar.FuelInTank,
            FuelLapsRemaining = playerCar.FuelRemainingLaps,
            ErsStoreEnergy = playerCar.ErsStoreEnergy,
            TyreWear = playerCar.TyreWear,
            TyreWearPerWheel = _latestPlayerTyreWearPerWheel,
            Position = playerCar.Position,
            DeltaFrontInMs = playerCar.DeltaToCarInFrontInMs,
            DeltaLeaderInMs = playerCar.DeltaToRaceLeaderInMs,
            PitStatus = playerCar.PitStatus,
            IsValid = playerCar.IsCurrentLapValid ?? true,
            VisualTyreCompound = playerCar.VisualTyreCompound,
            ActualTyreCompound = playerCar.ActualTyreCompound
        };
    }

    private static LapSummary TryApplyOfficialTiming(LapSummary summary, SessionHistoryPacket packet)
    {
        var lapIndex = summary.LapNumber - 1;
        if (lapIndex < 0 || lapIndex >= packet.LapHistory.Length)
        {
            return summary;
        }

        var lapHistory = packet.LapHistory[lapIndex];
        return summary with
        {
            LapTimeInMs = lapHistory.LapTimeInMs == 0 ? summary.LapTimeInMs : lapHistory.LapTimeInMs,
            Sector1TimeInMs = ToSectorMilliseconds(lapHistory.Sector1TimeMinutesPart, lapHistory.Sector1TimeMsPart),
            Sector2TimeInMs = ToSectorMilliseconds(lapHistory.Sector2TimeMinutesPart, lapHistory.Sector2TimeMsPart),
            Sector3TimeInMs = ToSectorMilliseconds(lapHistory.Sector3TimeMinutesPart, lapHistory.Sector3TimeMsPart),
            IsValid = lapHistory.IsLapValid
        };
    }

    private static uint? ToSectorMilliseconds(byte minutesPart, ushort millisecondsPart)
    {
        if (minutesPart == 0 && millisecondsPart == 0)
        {
            return null;
        }

        return (uint)(minutesPart * 60_000 + millisecondsPart);
    }
}
