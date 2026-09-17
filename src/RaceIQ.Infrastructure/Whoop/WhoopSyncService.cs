using Microsoft.Extensions.Logging;
using RaceIQ.Application;
using RaceIQ.Domain;

namespace RaceIQ.Infrastructure.Whoop;

public class WhoopSyncService : IProviderSyncService
{
    // How far back each sync looks. Recovery only exists once per day, so 90 days is at
    // most four pages and comfortably covers any gap between syncs.
    public const int SyncWindowDays = 90;

    private readonly IWhoopApiClient _apiClient;
    private readonly IWhoopOAuthService _oauthService;
    private readonly IRecoveryDayRepository _recoveryRepository;
    private readonly IConnectedAccountRepository _accountRepository;
    private readonly ILogger<WhoopSyncService> _logger;

    public ConnectedAccountProvider Provider => ConnectedAccountProvider.Whoop;

    public WhoopSyncService(
        IWhoopApiClient apiClient,
        IWhoopOAuthService oauthService,
        IRecoveryDayRepository recoveryRepository,
        IConnectedAccountRepository accountRepository,
        ILogger<WhoopSyncService> logger)
    {
        _apiClient = apiClient;
        _oauthService = oauthService;
        _recoveryRepository = recoveryRepository;
        _accountRepository = accountRepository;
        _logger = logger;
    }

    public async Task SyncAsync(string userId)
    {
        var account = await _accountRepository.GetAsync(userId, ConnectedAccountProvider.Whoop);
        if (account is null || account.Status == ConnectedAccountStatus.NeedsReconnect)
            return;

        var accessToken = await EnsureFreshTokenAsync(account);

        var since = DateTime.UtcNow.AddDays(-SyncWindowDays);
        var recoveries = await _apiClient.GetRecoveriesAsync(accessToken, since);

        foreach (var recovery in recoveries)
        {
            await _recoveryRepository.UpsertAsync(new RecoveryDay
            {
                UserId = userId,
                Date = DateOnly.FromDateTime(recovery.CreatedAtUtc),
                RecoveryScore = recovery.RecoveryScore,
                HrvMs = recovery.HrvMs,
                RestingHeartRate = recovery.RestingHeartRate,
                ProviderRecordId = recovery.CycleId
            });
        }

        _logger.LogInformation(
            "WHOOP sync stored {Count} recovery days for user {UserId}", recoveries.Count, userId);
    }

    // WHOOP access tokens live one hour, so nearly every sync refreshes. On a failed
    // refresh the account flips to NeedsReconnect and the error propagates, exactly as
    // for Strava, so the dashboard shows a reconnect card rather than a silent no-op.
    private async Task<string> EnsureFreshTokenAsync(ConnectedAccount account)
    {
        if (account.TokenExpiresAt is { } expiresAt && expiresAt > DateTime.UtcNow.AddMinutes(5))
            return account.AccessToken;

        WhoopTokenResponse refreshed;
        try
        {
            refreshed = await _oauthService.RefreshTokenAsync(account.RefreshToken!);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "WHOOP token refresh failed for user {UserId}; flipping account to NeedsReconnect", account.UserId);
            account.Status = ConnectedAccountStatus.NeedsReconnect;
            await _accountRepository.UpsertAsync(account);
            throw;
        }

        account.AccessToken = refreshed.AccessToken;
        account.RefreshToken = refreshed.RefreshToken;
        account.TokenExpiresAt = DateTime.UtcNow.AddSeconds(refreshed.ExpiresInSeconds);
        await _accountRepository.UpsertAsync(account);

        return account.AccessToken;
    }
}
