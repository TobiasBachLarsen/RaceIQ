using RaceIQ.Domain;

namespace RaceIQ.Application;

public interface IProviderSyncService
{
    ConnectedAccountProvider Provider { get; }

    Task SyncAsync(string userId);
}
