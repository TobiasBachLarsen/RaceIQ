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

    // Strava's own workout_type value for Ride activities. Strava does not officially
    // document these integers; per Strava's developer Community Hub ("Identifying
    // workouts from SummaryActivity object", ActivityFix's reply) and corroborating
    // community sources, Ride follows the same "+10 offset from Run" pattern Strava
    // uses elsewhere (Run: 0 = Default, 1 = Race, 2 = Long Run, 3 = Workout):
    // Ride: 10 = Default, 11 = Race, 12 = Workout. Task 1 originally shipped this as
    // WorkoutType == 10 based on an unverified recollection; corrected in Task 2 after
    // research contradicted that assumption. Still not verified against one of
    // Tobias's own Strava activities - confirm next time he's available. Null until
    // Task 2 backfills it for newly synced activities.
    public int? WorkoutType { get; set; }

    public bool IsRace => WorkoutType == 11;
}
