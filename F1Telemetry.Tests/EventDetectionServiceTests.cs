using F1Telemetry.Analytics.Events;
using F1Telemetry.Core.Models;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies that race events are detected from aggregate session state changes.
/// </summary>
public sealed class EventDetectionServiceTests
{
    /// <summary>
    /// Verifies that a front-car pit sequence emits a front-car pit event.
    /// </summary>
    [Fact]
    public void Observe_FrontCarPitSignals_EmitsFrontCarPittedEvent()
    {
        var service = new EventDetectionService(new EventDetectionOptions());

        service.Observe(CreateState(
            CreatePlayerCar(carIndex: 3, position: 2, lapNumber: 10, fuelLapsRemaining: 6.0f, tyreWear: 35f, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19),
            CreateOpponent(carIndex: 2, driverName: "Front Runner", position: 1, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19),
            CreateOpponent(carIndex: 4, driverName: "Rear Runner", position: 3, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19)));

        service.Observe(CreateState(
            CreatePlayerCar(carIndex: 3, position: 2, lapNumber: 10, fuelLapsRemaining: 5.8f, tyreWear: 36f, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19),
            CreateOpponent(carIndex: 2, driverName: "Front Runner", position: 1, pitStatus: 1, numPitStops: 1, visualTyreCompound: 16, actualTyreCompound: 19),
            CreateOpponent(carIndex: 4, driverName: "Rear Runner", position: 3, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19)));

        var detectedEvents = service.DrainPendingEvents();
        var raceEvent = Assert.Single(detectedEvents);

        Assert.Equal(EventType.FrontCarPitted, raceEvent.EventType);
        Assert.Equal(10, raceEvent.LapNumber);
        Assert.Equal(2, raceEvent.VehicleIdx);
        Assert.Equal("Front Runner", raceEvent.DriverName);
    }

    /// <summary>
    /// Verifies that crossing the configured low-fuel threshold emits an event.
    /// </summary>
    [Fact]
    public void Observe_FuelDropsBelowThreshold_EmitsLowFuelEvent()
    {
        var service = new EventDetectionService(new EventDetectionOptions
        {
            LowFuelLapsThreshold = 3.0f
        });

        service.Observe(CreateState(
            CreatePlayerCar(carIndex: 3, position: 5, lapNumber: 12, fuelLapsRemaining: 3.2f, tyreWear: 40f, pitStatus: 0, numPitStops: 1, visualTyreCompound: 17, actualTyreCompound: 20)));
        service.Observe(CreateState(
            CreatePlayerCar(carIndex: 3, position: 5, lapNumber: 12, fuelLapsRemaining: 2.8f, tyreWear: 41f, pitStatus: 0, numPitStops: 1, visualTyreCompound: 17, actualTyreCompound: 20)));

        var raceEvent = Assert.Single(service.DrainPendingEvents());
        Assert.Equal(EventType.LowFuel, raceEvent.EventType);
        Assert.Equal(12, raceEvent.LapNumber);
        Assert.Equal(3, raceEvent.VehicleIdx);
    }

    /// <summary>
    /// Verifies that tyre wear crossing the configured threshold emits an event.
    /// </summary>
    [Fact]
    public void Observe_TyreWearCrossesThreshold_EmitsHighTyreWearEvent()
    {
        var service = new EventDetectionService(new EventDetectionOptions
        {
            HighTyreWearThreshold = 70f
        });

        service.Observe(CreateState(
            CreatePlayerCar(carIndex: 3, position: 6, lapNumber: 15, fuelLapsRemaining: 8.0f, tyreWear: 69f, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19)));
        service.Observe(CreateState(
            CreatePlayerCar(carIndex: 3, position: 6, lapNumber: 15, fuelLapsRemaining: 7.9f, tyreWear: 70f, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19)));

        var raceEvent = Assert.Single(service.DrainPendingEvents());
        Assert.Equal(EventType.HighTyreWear, raceEvent.EventType);
        Assert.Equal(15, raceEvent.LapNumber);
        Assert.Equal(3, raceEvent.VehicleIdx);
    }

    /// <summary>
    /// Verifies that repeated low-fuel observations on the same lap do not emit duplicates.
    /// </summary>
    [Fact]
    public void Observe_RepeatedLowFuelOnSameLap_DeduplicatesEvent()
    {
        var service = new EventDetectionService(new EventDetectionOptions
        {
            LowFuelLapsThreshold = 3.0f
        });

        service.Observe(CreateState(
            CreatePlayerCar(carIndex: 3, position: 4, lapNumber: 18, fuelLapsRemaining: 3.1f, tyreWear: 50f, pitStatus: 0, numPitStops: 1, visualTyreCompound: 17, actualTyreCompound: 20)));
        service.Observe(CreateState(
            CreatePlayerCar(carIndex: 3, position: 4, lapNumber: 18, fuelLapsRemaining: 2.9f, tyreWear: 51f, pitStatus: 0, numPitStops: 1, visualTyreCompound: 17, actualTyreCompound: 20)));
        service.Observe(CreateState(
            CreatePlayerCar(carIndex: 3, position: 4, lapNumber: 18, fuelLapsRemaining: 2.5f, tyreWear: 52f, pitStatus: 0, numPitStops: 1, visualTyreCompound: 17, actualTyreCompound: 20)));

        var detectedEvents = service.DrainPendingEvents();

        var raceEvent = Assert.Single(detectedEvents);
        Assert.Equal(EventType.LowFuel, raceEvent.EventType);
    }

    /// <summary>
    /// Verifies that resetting the detector clears pending state and allows the same threshold crossing in a new session.
    /// </summary>
    [Fact]
    public void Reset_ClearsPendingStateAndAllowsFreshDetection()
    {
        var service = new EventDetectionService(new EventDetectionOptions
        {
            LowFuelLapsThreshold = 3.0f
        });

        service.Observe(CreateState(
            CreatePlayerCar(carIndex: 3, position: 4, lapNumber: 1, fuelLapsRemaining: 3.1f, tyreWear: 50f, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19)));
        service.Observe(CreateState(
            CreatePlayerCar(carIndex: 3, position: 4, lapNumber: 1, fuelLapsRemaining: 2.9f, tyreWear: 51f, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19)));

        service.Reset();

        Assert.Empty(service.DrainPendingEvents());

        service.Observe(CreateState(
            CreatePlayerCar(carIndex: 3, position: 4, lapNumber: 1, fuelLapsRemaining: 3.2f, tyreWear: 52f, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19)));
        service.Observe(CreateState(
            CreatePlayerCar(carIndex: 3, position: 4, lapNumber: 1, fuelLapsRemaining: 2.8f, tyreWear: 53f, pitStatus: 0, numPitStops: 0, visualTyreCompound: 16, actualTyreCompound: 19)));

        var raceEvent = Assert.Single(service.DrainPendingEvents());
        Assert.Equal(EventType.LowFuel, raceEvent.EventType);
        Assert.Equal(1, raceEvent.LapNumber);
    }

    private static SessionState CreateState(params CarSnapshot[] cars)
    {
        var playerCar = cars.Single(car => car.IsPlayer);
        var orderedCars = cars.OrderBy(car => car.Position ?? byte.MaxValue).ToArray();

        return new SessionState
        {
            PlayerCarIndex = (byte)playerCar.CarIndex,
            PlayerCar = playerCar,
            Cars = orderedCars,
            Opponents = orderedCars.Where(car => !car.IsPlayer).ToArray(),
            UpdatedAt = playerCar.UpdatedAt
        };
    }

    private static CarSnapshot CreatePlayerCar(
        int carIndex,
        byte position,
        byte lapNumber,
        float fuelLapsRemaining,
        float tyreWear,
        byte pitStatus,
        byte numPitStops,
        byte visualTyreCompound,
        byte actualTyreCompound)
    {
        return CreateCar(
            carIndex,
            driverName: "Player",
            isPlayer: true,
            position: position,
            lapNumber: lapNumber,
            fuelLapsRemaining: fuelLapsRemaining,
            tyreWear: tyreWear,
            pitStatus: pitStatus,
            numPitStops: numPitStops,
            visualTyreCompound: visualTyreCompound,
            actualTyreCompound: actualTyreCompound);
    }

    private static CarSnapshot CreateOpponent(
        int carIndex,
        string driverName,
        byte position,
        byte pitStatus,
        byte numPitStops,
        byte visualTyreCompound,
        byte actualTyreCompound)
    {
        return CreateCar(
            carIndex,
            driverName: driverName,
            isPlayer: false,
            position: position,
            lapNumber: 10,
            fuelLapsRemaining: 9.5f,
            tyreWear: 42f,
            pitStatus: pitStatus,
            numPitStops: numPitStops,
            visualTyreCompound: visualTyreCompound,
            actualTyreCompound: actualTyreCompound);
    }

    private static CarSnapshot CreateCar(
        int carIndex,
        string driverName,
        bool isPlayer,
        byte position,
        byte lapNumber,
        float fuelLapsRemaining,
        float tyreWear,
        byte pitStatus,
        byte numPitStops,
        byte visualTyreCompound,
        byte actualTyreCompound)
    {
        return new CarSnapshot
        {
            CarIndex = carIndex,
            DriverName = driverName,
            IsPlayer = isPlayer,
            Position = position,
            CurrentLapNumber = lapNumber,
            FuelRemainingLaps = fuelLapsRemaining,
            TyreWear = tyreWear,
            PitStatus = pitStatus,
            NumPitStops = numPitStops,
            VisualTyreCompound = visualTyreCompound,
            ActualTyreCompound = actualTyreCompound,
            IsCurrentLapValid = true,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }
}
