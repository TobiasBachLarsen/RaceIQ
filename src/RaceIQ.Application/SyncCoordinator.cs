using Microsoft.Extensions.Logging;
using RaceIQ.Domain;

namespace RaceIQ.Application;

// Orchestrates a sync across every registered provider. Each provider syncs
// independently: a failure is caught and logged here and never blocks the
// others. Only providers with a connected, usable account are attempted:
// no account at all, or an account that already needs reconnecting, are
// both skipped here rather than attempted and reported as a failure - a
// NeedsReconnect account already has its own reconnect card on the
// Dashboard explaining the real problem, and attempting it anyway would
// otherwise surface a second, misleading "sync failed" banner alongside it.
// The individual provider services keep their own NeedsReconnect guard too,
// as defense in depth for any other caller.
public class SyncCoordinator
{
    private readonly IEnumerable<IProviderSyncService> _providerSyncServices;
    private readonly IConnectedAccountRepository _accountRepository;
    private readonly ILogger<SyncCoordinator> _logger;

    public SyncCoordinator(
        IEnumerable<IProviderSyncService> providerSyncServices,
        IConnectedAccountRepository accountRepository,
        ILogger<SyncCoordinator> logger)
    {
        _providerSyncServices = providerSyncServices;
        _accountRepository = accountRepository;
        _logger = logger;
    }

    public async Task<SyncOutcome> SyncAllAsync(string userId)
    {
        var stravaFailed = false;

        foreach (var providerSyncService in _providerSyncServices)
        {
            var account = await _accountRepository.GetAsync(userId, providerSyncService.Provider);
            if (account is null || account.Status == ConnectedAccountStatus.NeedsReconnect)
                continue;

            try
            {
                await providerSyncService.SyncAsync(userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Provider} sync failed for user {UserId}", providerSyncService.Provider, userId);

                if (providerSyncService.Provider == ConnectedAccountProvider.Strava)
                    stravaFailed = true;
            }
        }

        return new SyncOutcome { StravaFailed = stravaFailed };
    }
}
