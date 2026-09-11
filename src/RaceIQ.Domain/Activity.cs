namespace RaceIQ.Domain;

public class Activity
{
    public int Id { get; set; }
    public required string UserId { get; set; }
    public required string StravaActivityId { get; set; }
    public required string Name { get; set; }
    public DateTime StartedAt { get; set; }
    public TimeSpan Duration { get; set; }
    public double DistanceMeters { get; set; }
    public double ElevationGainMeters { get; set; }
    public double? AveragePowerWatts { get; set; }
    public double? NormalizedPowerWatts { get; set; }
    public double? AverageHeartRateBpm { get; set; }
    public double? AverageCadenceRpm { get; set; }
    public required string StreamDataJson { get; set; }
}
