using RaceIQ.Domain;

namespace RaceIQ.Infrastructure.Strava;

public interface IStravaApiClient
{
    Task<IReadOnlyList<StravaActivitySummary>> ListRecentActivitiesAsync(string accessToken);
    Task<IReadOnlyList<StreamPoint>> GetActivityStreamAsync(string accessToken, string stravaActivityId);
}

public record StravaActivitySummary(
    string Id,
    string Name,
    DateTime StartDate,
    double DistanceMeters,
    TimeSpan MovingTime,
    double ElevationGainMeters,
    double? AveragePowerWatts,
    double? WeightedAveragePowerWatts,
    double? AverageHeartRateBpm,
    double? AverageCadenceRpm,
    int? WorkoutType);
