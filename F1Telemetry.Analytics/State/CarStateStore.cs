using System.Threading;
using F1Telemetry.Core.Models;

namespace F1Telemetry.Analytics.State;

/// <summary>
/// Stores the latest per-car snapshots with a single-writer, multi-reader update model.
/// </summary>
public sealed class CarStateStore
{
    private readonly CarSnapshot?[] _cars = new CarSnapshot?[22];
    private readonly bool[] _participantTelemetryRestricted = new bool[22];
    private int _playerCarIndex = -1;

    /// <summary>
    /// Returns the latest snapshot for the specified car index.
    /// </summary>
    /// <param name="carIndex">The in-session car index.</param>
    public CarSnapshot? GetCar(int carIndex)
    {
        ValidateCarIndex(carIndex);
        return Volatile.Read(ref _cars[carIndex]);
    }

    /// <summary>
    /// Returns a stable list of all tracked cars at the time of the call.
    /// </summary>
    public IReadOnlyList<CarSnapshot> CaptureAllCars()
    {
        var cars = new List<CarSnapshot>(_cars.Length);

        for (var index = 0; index < _cars.Length; index++)
        {
            var snapshot = Volatile.Read(ref _cars[index]);
            if (snapshot is not null)
            {
                cars.Add(snapshot);
            }
        }

        return cars;
    }

    /// <summary>
    /// Returns the latest player car snapshot when known.
    /// </summary>
    public CarSnapshot? CapturePlayerCar()
    {
        var playerCarIndex = Volatile.Read(ref _playerCarIndex);
        return playerCarIndex is >= 0 and < 22
            ? Volatile.Read(ref _cars[playerCarIndex])
            : null;
    }

    /// <summary>
    /// Returns the latest opponent snapshots ordered by position and car index.
    /// </summary>
    public IReadOnlyList<CarSnapshot> CaptureOpponents()
    {
        var playerCarIndex = Volatile.Read(ref _playerCarIndex);
        var opponents = new List<CarSnapshot>(_cars.Length);

        for (var index = 0; index < _cars.Length; index++)
        {
            if (index == playerCarIndex)
            {
                continue;
            }

            var snapshot = Volatile.Read(ref _cars[index]);
            if (snapshot is not null)
            {
                opponents.Add(snapshot);
            }
        }

        return opponents
            .OrderBy(snapshot => snapshot.Position ?? byte.MaxValue)
            .ThenBy(snapshot => snapshot.CarIndex)
            .ToArray();
    }

    /// <summary>
    /// Clears all tracked cars, telemetry restrictions, and player indexing for a new session.
    /// </summary>
    public void Reset()
    {
        Interlocked.Exchange(ref _playerCarIndex, -1);

        for (var index = 0; index < _cars.Length; index++)
        {
            _participantTelemetryRestricted[index] = false;
            Volatile.Write(ref _cars[index], null);
        }
    }

    internal void SetPlayerCarIndex(byte playerCarIndex, DateTimeOffset updatedAt)
    {
        if (playerCarIndex >= _cars.Length)
        {
            return;
        }

        var previousPlayerCarIndex = Interlocked.Exchange(ref _playerCarIndex, playerCarIndex);
        if (previousPlayerCarIndex == playerCarIndex)
        {
            UpdateCar(playerCarIndex, snapshot => snapshot with { IsPlayer = true }, updatedAt);
            return;
        }

        if (previousPlayerCarIndex is >= 0 and < 22)
        {
            UpdateCar(
                previousPlayerCarIndex,
                snapshot => snapshot with
                {
                    IsPlayer = false,
                    IsTelemetryRestricted = _participantTelemetryRestricted[previousPlayerCarIndex]
                },
                updatedAt);
        }

        UpdateCar(
            playerCarIndex,
            snapshot => snapshot with
            {
                IsPlayer = true,
                IsTelemetryRestricted = false
            },
            updatedAt);
    }

    internal void SetParticipantTelemetryRestriction(int carIndex, bool isRestricted, DateTimeOffset updatedAt)
    {
        ValidateCarIndex(carIndex);
        _participantTelemetryRestricted[carIndex] = isRestricted;

        UpdateCar(
            carIndex,
            snapshot => snapshot with
            {
                IsTelemetryRestricted = IsTelemetryRestricted(carIndex)
            },
            updatedAt);

        if (IsTelemetryRestricted(carIndex))
        {
            ClearRestrictedTelemetry(carIndex, updatedAt);
        }
    }

    internal bool HasTelemetryAccess(int carIndex)
    {
        ValidateCarIndex(carIndex);
        return !IsTelemetryRestricted(carIndex);
    }

    internal void UpdateCar(int carIndex, Func<CarSnapshot, CarSnapshot> updater, DateTimeOffset updatedAt)
    {
        ValidateCarIndex(carIndex);

        var current = Volatile.Read(ref _cars[carIndex]) ?? CreateEmptySnapshot(carIndex, updatedAt);
        var normalized = current with
        {
            IsPlayer = carIndex == Volatile.Read(ref _playerCarIndex),
            IsTelemetryRestricted = IsTelemetryRestricted(carIndex),
            UpdatedAt = updatedAt
        };

        var updated = updater(normalized) with
        {
            IsPlayer = carIndex == Volatile.Read(ref _playerCarIndex),
            IsTelemetryRestricted = IsTelemetryRestricted(carIndex),
            UpdatedAt = updatedAt
        };

        Volatile.Write(ref _cars[carIndex], updated);
    }

    internal void ClearRestrictedTelemetry(int carIndex, DateTimeOffset updatedAt)
    {
        UpdateCar(
            carIndex,
            snapshot => snapshot with
            {
                Telemetry = null,
                SteeringInput = null,
                Gear = null,
                EngineRpm = null,
                IsDrsEnabled = null,
                FuelInTank = null,
                FuelRemainingLaps = null,
                ErsStoreEnergy = null,
                ActualTyreCompound = null,
                VisualTyreCompound = null,
                TyresAgeLaps = null,
                TyreWear = null,
                TyreCondition = null,
                FrontLeftWingDamage = null,
                FrontRightWingDamage = null,
                RearWingDamage = null,
                Damage = null,
                WorldPositionX = null,
                WorldPositionY = null,
                WorldPositionZ = null
            },
            updatedAt);
    }

    private static CarSnapshot CreateEmptySnapshot(int carIndex, DateTimeOffset updatedAt)
    {
        return new CarSnapshot
        {
            CarIndex = carIndex,
            UpdatedAt = updatedAt
        };
    }

    private bool IsTelemetryRestricted(int carIndex)
    {
        return carIndex != Volatile.Read(ref _playerCarIndex) && _participantTelemetryRestricted[carIndex];
    }

    private static void ValidateCarIndex(int carIndex)
    {
        if (carIndex is < 0 or >= 22)
        {
            throw new ArgumentOutOfRangeException(nameof(carIndex), carIndex, "Car index must be between 0 and 21.");
        }
    }
}
