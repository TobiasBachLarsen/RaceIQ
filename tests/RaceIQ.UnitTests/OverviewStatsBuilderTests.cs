using RaceIQ.Application;
using RaceIQ.Domain;
using Xunit;

namespace RaceIQ.UnitTests;

public class OverviewStatsBuilderTests
{
    private static readonly DateTime Now = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);

    private static Activity Ride(
        DateTime startedAt, double meters = 30000, double elevation = 100,
        int minutes = 60, double? power = 200, int? workoutType = null) => new()
    {
        UserId = "u", StravaActivityId = Guid.NewGuid().ToString(), Name = "Ride",
        StartedAt = startedAt, Duration = TimeSpan.FromMinutes(minutes),
        DistanceMeters = meters, ElevationGainMeters = elevation,
        AveragePowerWatts = power, WorkoutType = workoutType, StreamDataJson = "[]"
    };

    private static RaceResult Result(int? position = null, double? ratingChange = null) => new()
    {
        UserId = "u", Provider = ConnectedAccountProvider.ZwiftPower,
        ProviderResultId = Guid.NewGuid().ToString(), EventName = "Race",
        EventDate = Now, Position = position, RatingChange = ratingChange
    };

    [Fact]
    public void Build_NoData_ReturnsZerosAndNulls()
    {
        var stats = OverviewStatsBuilder.Build(Array.Empty<Activity>(), Array.Empty<RaceResult>(), Now);

        Assert.Equal(0, stats.RideCount);
        Assert.Equal(0, stats.TotalDistanceKm);
        Assert.Equal(TimeSpan.Zero, stats.TotalDuration);
        Assert.Equal(0, stats.RaceCount);
        Assert.Null(stats.BestPosition);
        Assert.Null(stats.TotalRatingChange);
        Assert.Equal(0, stats.RecentRideCount);
        Assert.Null(stats.RecentAvgPower);
    }

    [Fact]
    public void Build_SumsAllTimeTotals()
    {
        var activities = new[]
        {
            Ride(Now.AddDays(-5), meters: 40000, elevation: 300, minutes: 90, power: 210),
            Ride(Now.AddDays(-200), meters: 20000, elevation: 100, minutes: 45, power: 180),
        };

        var stats = OverviewStatsBuilder.Build(activities, Array.Empty<RaceResult>(), Now);

        Assert.Equal(2, stats.RideCount);
        Assert.Equal(60, stats.TotalDistanceKm);
        Assert.Equal(TimeSpan.FromMinutes(135), stats.TotalDuration);
        Assert.Equal(400, stats.TotalElevationM);
    }

    [Fact]
    public void Build_RecentWindow_ExcludesRidesOlderThan30Days_IncludesBoundary()
    {
        var activities = new[]
        {
            Ride(Now.AddDays(-5), meters: 30000, minutes: 60, power: 220),      // in
            Ride(Now.AddDays(-30), meters: 10000, minutes: 20, power: 180),     // exactly 30 days -> in
            Ride(Now.AddDays(-31), meters: 50000, minutes: 120, power: 300),    // out
        };

        var stats = OverviewStatsBuilder.Build(activities, Array.Empty<RaceResult>(), Now);

        Assert.Equal(2, stats.RecentRideCount);
        Assert.Equal(40, stats.RecentDistanceKm);
        Assert.Equal(TimeSpan.FromMinutes(80), stats.RecentDuration);
        Assert.Equal(200, stats.RecentAvgPower);   // (220 + 180) / 2
    }

    [Fact]
    public void Build_RaceCount_CountsRaceWorkoutType()
    {
        var activities = new[]
        {
            Ride(Now.AddDays(-1), workoutType: 11),   // race
            Ride(Now.AddDays(-2), workoutType: 10),   // default
            Ride(Now.AddDays(-3), workoutType: null), // unknown
            Ride(Now.AddDays(-4), workoutType: 11),   // race
        };

        var stats = OverviewStatsBuilder.Build(activities, Array.Empty<RaceResult>(), Now);

        Assert.Equal(2, stats.RaceCount);
    }

    [Fact]
    public void Build_RacingStats_BestPositionAndRatingSum()
    {
        var results = new[]
        {
            Result(position: 24, ratingChange: 12.2),
            Result(position: 3, ratingChange: -4.0),
            Result(position: null, ratingChange: 5.0),
        };

        var stats = OverviewStatsBuilder.Build(Array.Empty<Activity>(), results, Now);

        Assert.Equal(3, stats.BestPosition);
        Assert.Equal(13.2, stats.TotalRatingChange!.Value, 3);
    }

    [Fact]
    public void Build_RecentAvgPower_IgnoresRidesWithoutPower()
    {
        var activities = new[]
        {
            Ride(Now.AddDays(-1), power: 250),
            Ride(Now.AddDays(-2), power: null),
        };

        var stats = OverviewStatsBuilder.Build(activities, Array.Empty<RaceResult>(), Now);

        Assert.Equal(250, stats.RecentAvgPower);
    }
}
