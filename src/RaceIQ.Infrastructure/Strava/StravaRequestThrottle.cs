namespace RaceIQ.Infrastructure.Strava;

public class StravaRequestThrottle
{
    public Task WaitForSlotAsync() => Task.CompletedTask;
}
