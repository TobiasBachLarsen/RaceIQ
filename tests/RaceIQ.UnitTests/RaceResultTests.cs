using RaceIQ.Domain;
using Xunit;

namespace RaceIQ.UnitTests;

public class RaceResultTests
{
    [Theory]
    [InlineData(10, true)]
    [InlineData(11, false)]
    [InlineData(null, false)]
    public void Activity_IsRace_ReflectsWorkoutType(int? workoutType, bool expected)
    {
        var activity = new Activity
        {
            UserId = "user-1",
            StravaActivityId = "1",
            Name = "Test",
            StreamDataJson = "[]",
            WorkoutType = workoutType
        };

        Assert.Equal(expected, activity.IsRace);
    }
}
