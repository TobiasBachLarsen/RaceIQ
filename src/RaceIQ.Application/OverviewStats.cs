using RaceIQ.Domain;

namespace RaceIQ.Application;

// Aggregated headline numbers for the overview shown at the top of the dashboard.
public record OverviewStats(
    int RideCount,
    double TotalDistanceKm,
    TimeSpan TotalDuration,
    double TotalElevationM,
    int RaceCount,
    int? BestPosition,
    double? TotalRatingChange,
    int RecentRideCount,
    double RecentDistanceKm,
    TimeSpan RecentDuration,
    double? RecentAvgPower);

public static class OverviewStatsBuilder
{
    public const int RecentDays = 30;

    public static OverviewStats Build(
        IReadOnlyList<Activity> activities,
        IReadOnlyList<RaceResult> raceResults,
        DateTime now)
    {
        var cutoff = now.AddDays(-RecentDays);
        var recent = activities.Where(a => a.StartedAt >= cutoff).ToList();

        var recentPowers = recent
            .Where(a => a.AveragePowerWatts is not null)
            .Select(a => a.AveragePowerWatts!.Value)
            .ToList();

        var positions = raceResults
            .Where(r => r.Position is not null)
            .Select(r => r.Position!.Value)
            .ToList();

        var ratingChanges = raceResults
            .Where(r => r.RatingChange is not null)
            .Select(r => r.RatingChange!.Value)
            .ToList();

        return new OverviewStats(
            RideCount: activities.Count,
            TotalDistanceKm: activities.Sum(a => a.DistanceMeters) / 1000,
            TotalDuration: activities.Aggregate(TimeSpan.Zero, (total, a) => total + a.Duration),
            TotalElevationM: activities.Sum(a => a.ElevationGainMeters),
            RaceCount: activities.Count(a => a.IsRace),
            BestPosition: positions.Count > 0 ? positions.Min() : null,
            TotalRatingChange: ratingChanges.Count > 0 ? ratingChanges.Sum() : null,
            RecentRideCount: recent.Count,
            RecentDistanceKm: recent.Sum(a => a.DistanceMeters) / 1000,
            RecentDuration: recent.Aggregate(TimeSpan.Zero, (total, a) => total + a.Duration),
            RecentAvgPower: recentPowers.Count > 0 ? recentPowers.Average() : null);
    }
}
