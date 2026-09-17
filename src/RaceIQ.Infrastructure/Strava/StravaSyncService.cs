using System.Text.Json;
using Microsoft.Extensions.Logging;
using RaceIQ.Application;
using RaceIQ.Domain;

namespace RaceIQ.Infrastructure.Strava;

public class StravaSyncService : IProviderSyncService
{
    private readonly IStravaApiClient _apiClient;
    private readonly IStravaOAuthService _oauthService;
    private readonly IActivityRepository _activityRepository;
    private readonly IConnectedAccountRepository _accountRepository;
    private readonly ConnectedAccountTokenRefresher _tokenRefresher;
    private readonly ILogger<StravaSyncService> _logger;

    public ConnectedAccountProvider Provider => ConnectedAccountProvider.Strava;

    public StravaSyncService(
        IStravaApiClient apiClient,
        IStravaOAuthService oauthService,
        IActivityRepository activityRepository,
        IConnectedAccountRepository accountRepository,
        ConnectedAccountTokenRefresher tokenRefresher,
        ILogger<StravaSyncService> logger)
    {
        _apiClient = apiClient;
        _oauthService = oauthService;
        _activityRepository = activityRepository;
        _accountRepository = accountRepository;
        _tokenRefresher = tokenRefresher;
        _logger = logger;
    }

    public async Task SyncAsync(string userId)
    {
        var account = await _accountRepository.GetAsync(userId, ConnectedAccountProvider.Strava);
        if (account is null || account.Status == ConnectedAccountStatus.NeedsReconnect)
            return;

        // Strava reports expiry as an absolute unix timestamp.
        var accessToken = await _tokenRefresher.EnsureFreshAsync(account, async refreshToken =>
        {
            var token = await _oauthService.RefreshTokenAsync(refreshToken);
            return new RefreshedToken(
                token.AccessToken,
                token.RefreshToken,
                DateTimeOffset.FromUnixTimeSeconds(token.ExpiresAtUnix).UtcDateTime);
        });

        var summaries = await _apiClient.ListRecentActivitiesAsync(accessToken);
        var importedCount = 0;

        foreach (var summary in summaries)
        {
            var existing = await _activityRepository.GetByStravaActivityIdAsync(userId, summary.Id);
            if (existing is not null)
                continue;

            var stream = await _apiClient.GetActivityStreamAsync(accessToken, summary.Id);

            await _activityRepository.AddAsync(new Activity
            {
                UserId = userId,
                StravaActivityId = summary.Id,
                Name = summary.Name,
                StartedAt = summary.StartDate,
                Duration = summary.MovingTime,
                DistanceMeters = summary.DistanceMeters,
                ElevationGainMeters = summary.ElevationGainMeters,
                AveragePowerWatts = summary.AveragePowerWatts,
                NormalizedPowerWatts = summary.WeightedAveragePowerWatts,
                AverageHeartRateBpm = summary.AverageHeartRateBpm,
                AverageCadenceRpm = summary.AverageCadenceRpm,
                WorkoutType = summary.WorkoutType,
                StreamDataJson = JsonSerializer.Serialize(stream)
            });
            importedCount++;
        }

        _logger.LogInformation(
            "Strava sync imported {ImportedCount} new activities for user {UserId}", importedCount, userId);
    }
}
