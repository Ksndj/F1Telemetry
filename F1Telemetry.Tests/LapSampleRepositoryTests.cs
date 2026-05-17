using System.IO;
using F1Telemetry.Storage.Interfaces;
using F1Telemetry.Storage.Models;
using F1Telemetry.Storage.Repositories;
using F1Telemetry.Storage.Services;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies V3 lap-sample persistence and schema initialization.
/// </summary>
public sealed class LapSampleRepositoryTests
{
    /// <summary>
    /// Verifies that high-frequency lap samples retain offline-analysis fields and query order.
    /// </summary>
    [Fact]
    public async Task AddRangeAsync_PreservesLapSamplesForOfflineAnalysis()
    {
        var rootPath = CreateRootPath();
        await using IDatabaseService databaseService = new SqliteDatabaseService(rootPath);
        await databaseService.InitializeAsync();
        await SeedSessionAsync(databaseService, "session-samples");
        ILapSampleRepository repository = new LapSampleRepository(databaseService);

        await repository.AddRangeAsync(
            [
                new StoredLapSample
                {
                    SessionId = "session-samples",
                    SampleIndex = 2,
                    SampledAt = DateTimeOffset.Parse("2026-05-17T10:00:02Z"),
                    FrameIdentifier = 102,
                    LapNumber = 7,
                    LapDistance = 410.5f,
                    TotalDistance = 5_210.5f,
                    CurrentLapTimeInMs = 20_200,
                    LastLapTimeInMs = 91_000,
                    SpeedKph = 286.4,
                    Throttle = 0.92,
                    Brake = 0.05,
                    Steering = -0.12f,
                    Gear = 7,
                    FuelRemainingLitres = 18.4f,
                    FuelLapsRemaining = 12.6f,
                    ErsStoreEnergy = 3_200_000f,
                    TyreWear = 18.5f,
                    TyreWearFrontLeft = 19.1f,
                    TyreWearFrontRight = 18.7f,
                    TyreWearRearLeft = 18.2f,
                    TyreWearRearRight = 17.9f,
                    Position = 4,
                    DeltaFrontInMs = 820,
                    DeltaLeaderInMs = 12_500,
                    PitStatus = 0,
                    IsValid = true,
                    VisualTyreCompound = 17,
                    ActualTyreCompound = 18,
                    CreatedAt = DateTimeOffset.Parse("2026-05-17T10:00:02Z")
                },
                new StoredLapSample
                {
                    SessionId = "session-samples",
                    SampleIndex = 1,
                    SampledAt = DateTimeOffset.Parse("2026-05-17T10:00:01Z"),
                    FrameIdentifier = 101,
                    LapNumber = 7,
                    LapDistance = 305.25f,
                    CurrentLapTimeInMs = 19_100,
                    SpeedKph = 274.2,
                    Throttle = 0.7,
                    Brake = 0.18,
                    Steering = 0.08f,
                    Gear = 6,
                    FuelRemainingLitres = 18.5f,
                    ErsStoreEnergy = 3_230_000f,
                    TyreWear = 18.3f,
                    Position = 4,
                    DeltaFrontInMs = 850,
                    PitStatus = 0,
                    IsValid = true,
                    VisualTyreCompound = 17,
                    ActualTyreCompound = 18,
                    CreatedAt = DateTimeOffset.Parse("2026-05-17T10:00:01Z")
                }
            ]);

        var samples = await repository.GetForLapAsync("session-samples", 7);

        Assert.Equal(2, samples.Count);
        Assert.Equal(new[] { 1, 2 }, samples.Select(sample => sample.SampleIndex));
        Assert.All(samples, sample => Assert.True(sample.Id > 0));

        var firstSample = samples[0];
        Assert.Equal("session-samples", firstSample.SessionId);
        Assert.Equal(DateTimeOffset.Parse("2026-05-17T10:00:01Z"), firstSample.SampledAt);
        Assert.Equal(101, firstSample.FrameIdentifier);
        Assert.Equal(305.25f, firstSample.LapDistance);
        Assert.Null(firstSample.TotalDistance);
        Assert.Equal(19_100, firstSample.CurrentLapTimeInMs);
        Assert.Equal(274.2, firstSample.SpeedKph);
        Assert.Equal(0.7, firstSample.Throttle);
        Assert.Equal(0.18, firstSample.Brake);
        Assert.Equal(0.08f, firstSample.Steering);
        Assert.Equal(6, firstSample.Gear);
        Assert.Equal(18.5f, firstSample.FuelRemainingLitres);
        Assert.Equal(3_230_000f, firstSample.ErsStoreEnergy);
        Assert.Equal(18.3f, firstSample.TyreWear);
        Assert.Equal(4, firstSample.Position);
        Assert.Equal(850, firstSample.DeltaFrontInMs);
        Assert.True(firstSample.IsValid);
        Assert.Equal(17, firstSample.VisualTyreCompound);
        Assert.Equal(18, firstSample.ActualTyreCompound);

        var secondSample = samples[1];
        Assert.Equal(5_210.5f, secondSample.TotalDistance);
        Assert.Equal(91_000, secondSample.LastLapTimeInMs);
        Assert.Equal(12.6f, secondSample.FuelLapsRemaining);
        Assert.Equal(19.1f, secondSample.TyreWearFrontLeft);
        Assert.Equal(18.7f, secondSample.TyreWearFrontRight);
        Assert.Equal(18.2f, secondSample.TyreWearRearLeft);
        Assert.Equal(17.9f, secondSample.TyreWearRearRight);
        Assert.Equal(12_500, secondSample.DeltaLeaderInMs);
    }

    /// <summary>
    /// Verifies that initialization creates the V3 persistence tables and lookup indexes.
    /// </summary>
    [Fact]
    public async Task InitializeAsync_CreatesV3TablesAndIndexes()
    {
        var rootPath = CreateRootPath();
        await using IDatabaseService databaseService = new SqliteDatabaseService(rootPath);

        await databaseService.InitializeAsync();

        var tableNames = await ReadSqliteNamesAsync(
            databaseService,
            "table",
            [
                "lap_samples",
                "corner_summaries",
                "strategy_advices",
                "race_engineer_reports"
            ]);
        var indexNames = await ReadSqliteNamesAsync(
            databaseService,
            "index",
            [
                "idx_lap_samples_session_lap_order",
                "idx_lap_samples_session_created_at_desc",
                "idx_corner_summaries_session_lap_corner",
                "idx_corner_summaries_session_created_at_desc",
                "idx_strategy_advices_session_lap_created_at_desc",
                "idx_strategy_advices_session_created_at_desc",
                "idx_race_engineer_reports_session_lap_created_at_desc",
                "idx_race_engineer_reports_session_created_at_desc"
            ]);

        Assert.Equal(
            [
                "corner_summaries",
                "lap_samples",
                "race_engineer_reports",
                "strategy_advices"
            ],
            tableNames);
        Assert.Equal(
            [
                "idx_corner_summaries_session_created_at_desc",
                "idx_corner_summaries_session_lap_corner",
                "idx_lap_samples_session_created_at_desc",
                "idx_lap_samples_session_lap_order",
                "idx_race_engineer_reports_session_created_at_desc",
                "idx_race_engineer_reports_session_lap_created_at_desc",
                "idx_strategy_advices_session_created_at_desc",
                "idx_strategy_advices_session_lap_created_at_desc"
            ],
            indexNames);
    }

    private static string CreateRootPath()
    {
        return Path.Combine(Path.GetTempPath(), "F1TelemetryTests", Guid.NewGuid().ToString("N"));
    }

    private static async Task SeedSessionAsync(IDatabaseService databaseService, string sessionId)
    {
        ISessionRepository repository = new SessionRepository(databaseService);
        await repository.CreateAsync(
            new StoredSession
            {
                Id = sessionId,
                SessionUid = $"uid-{sessionId}",
                TrackId = 10,
                SessionType = 12,
                StartedAt = DateTimeOffset.Parse("2026-05-17T10:00:00Z")
            });
    }

    private static Task<IReadOnlyList<string>> ReadSqliteNamesAsync(
        IDatabaseService databaseService,
        string type,
        IReadOnlyCollection<string> names)
    {
        return databaseService.ExecuteAsync(
            async (connection, cancellationToken) =>
            {
                using var command = connection.CreateCommand();
                command.CommandText = $"""
                    SELECT name
                    FROM sqlite_master
                    WHERE type = @type
                      AND name IN ({string.Join(", ", names.Select((_, index) => $"@name{index}"))})
                    ORDER BY name;
                    """;
                command.Parameters.AddWithValue("@type", type);
                var index = 0;
                foreach (var name in names)
                {
                    command.Parameters.AddWithValue($"@name{index}", name);
                    index++;
                }

                using var reader = await command.ExecuteReaderAsync(cancellationToken);
                var results = new List<string>();
                while (await reader.ReadAsync(cancellationToken))
                {
                    results.Add(reader.GetString(0));
                }

                return (IReadOnlyList<string>)results;
            });
    }
}
