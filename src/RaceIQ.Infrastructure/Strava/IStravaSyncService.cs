namespace RaceIQ.Infrastructure.Strava;

public interface IStravaSyncService
{
    Task SyncAsync(string userId);
}
