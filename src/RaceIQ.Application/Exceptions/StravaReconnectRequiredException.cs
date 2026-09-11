namespace RaceIQ.Application.Exceptions;

public class StravaReconnectRequiredException : Exception
{
    public StravaReconnectRequiredException(string userId)
        : base($"Strava connection for user {userId} needs to be reconnected before syncing.") { }
}
