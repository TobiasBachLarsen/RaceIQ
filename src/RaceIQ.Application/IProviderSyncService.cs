using RaceIQ.Domain;

namespace RaceIQ.Application;

public interface IProviderSyncService
{
    ConnectedAccountProvider Provider { get; }

    // Syncs this provider's data for the user. Returns quietly if there is no account or
    // the account already needs reconnecting, rather than throwing, so the coordinator
    // never surfaces a missing or reconnect-state account as a sync failure. A genuine failure
    // mid-sync may still throw and is caught per-provider by the coordinator.
    Task SyncAsync(string userId);
}
