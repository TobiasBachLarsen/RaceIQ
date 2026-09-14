namespace RaceIQ.Infrastructure.ZwiftRacing;

public interface IZwiftRacingSyncService
{
    Task SyncAsync(string userId);
}
