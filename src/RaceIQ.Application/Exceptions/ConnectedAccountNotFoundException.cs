namespace RaceIQ.Application.Exceptions;

public class ConnectedAccountNotFoundException : Exception
{
    public ConnectedAccountNotFoundException(string userId)
        : base($"No connected Strava account was found for user {userId}.") { }
}
