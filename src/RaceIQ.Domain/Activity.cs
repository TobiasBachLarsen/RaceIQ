namespace RaceIQ.Domain;

// Strava's workout_type integers for Ride activities (Run's values + 10).
public enum RideWorkoutType
{
    Default = 10,
    Race = 11,
    Workout = 12,
}

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
    // Ride: 10 = Default, 11 = Race, 12 = Workout (see RideWorkoutType). Not yet verified
    // against a real Strava activity; null for activities synced before this field existed.
    public int? WorkoutType { get; set; }

    public bool IsRace => WorkoutType == (int)RideWorkoutType.Race;
}
