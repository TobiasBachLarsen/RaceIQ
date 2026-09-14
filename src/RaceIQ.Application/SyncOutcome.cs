namespace RaceIQ.Application;

// Result of one SyncCoordinator.SyncAllAsync call. Only Strava's failure is
// user-visible today (see Dashboard.razor), so that is the one flag callers need.
public class SyncOutcome
{
    public bool StravaFailed { get; init; }
}
