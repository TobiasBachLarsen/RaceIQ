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
    private readonly ConnectedAccountTokenRefresher _tokenRefresher;
    private readonly ILogger<WhoopSyncService> _logger;

    public ConnectedAccountProvider Provider => ConnectedAccountProvider.Whoop;

    public WhoopSyncService(
        IWhoopApiClient apiClient,
        IWhoopOAuthService oauthService,
        IRecoveryDayRepository recoveryRepository,
        IConnectedAccountRepository accountRepository,
        ConnectedAccountTokenRefresher tokenRefresher,
        ILogger<WhoopSyncService> logger)
    {
        _apiClient = apiClient;
        _oauthService = oauthService;
        _recoveryRepository = recoveryRepository;
        _accountRepository = accountRepository;
        _tokenRefresher = tokenRefresher;
        _logger = logger;
    }

    public async Task SyncAsync(string userId)
    {
        var account = await _accountRepository.GetAsync(userId, ConnectedAccountProvider.Whoop);
        if (account is null || account.Status == ConnectedAccountStatus.NeedsReconnect)
            return;

        // WHOOP access tokens live one hour and expiry is reported as a relative expires_in,
        // so nearly every sync refreshes.
        var accessToken = await _tokenRefresher.EnsureFreshAsync(account, async refreshToken =>
        {
            var token = await _oauthService.RefreshTokenAsync(refreshToken);
            return new RefreshedToken(
                token.AccessToken,
                token.RefreshToken,
                DateTime.UtcNow.AddSeconds(token.ExpiresInSeconds));
        });

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
}
