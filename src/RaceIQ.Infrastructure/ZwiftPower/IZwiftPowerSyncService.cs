namespace RaceIQ.Infrastructure.ZwiftPower;

public interface IZwiftPowerSyncService
{
    Task SyncAsync(string userId);
}
