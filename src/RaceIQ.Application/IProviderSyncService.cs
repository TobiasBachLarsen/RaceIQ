using RaceIQ.Domain;

namespace RaceIQ.Application;

public interface IProviderSyncService
{
    ConnectedAccountProvider Provider { get; }

    // Syncs this provider's data for the user. Returns quietly if the account is missing a
    // usable connection (e.g. already NeedsReconnect) rather than throwing, so the
    // coordinator never surfaces a reconnect state as a sync failure. A genuine failure
    // mid-sync may still throw and is caught per-provider by the coordinator.
    Task SyncAsync(string userId);
}
