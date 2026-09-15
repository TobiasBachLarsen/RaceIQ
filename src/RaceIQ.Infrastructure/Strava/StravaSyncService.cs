using System.Text.Json;
using Microsoft.Extensions.Logging;
using RaceIQ.Application;
using RaceIQ.Application.Exceptions;
using RaceIQ.Domain;

namespace RaceIQ.Infrastructure.Strava;

public class StravaSyncService : IProviderSyncService
{
    private readonly IStravaApiClient _apiClient;
    private readonly IStravaOAuthService _oauthService;
    private readonly IActivityRepository _activityRepository;
    private readonly IConnectedAccountRepository _accountRepository;
    private readonly ILogger<StravaSyncService> _logger;

    public ConnectedAccountProvider Provider => ConnectedAccountProvider.Strava;

    public StravaSyncService(
        IStravaApiClient apiClient,
        IStravaOAuthService oauthService,
        IActivityRepository activityRepository,
        IConnectedAccountRepository accountRepository,
        ILogger<StravaSyncService> logger)
    {
        _apiClient = apiClient;
        _oauthService = oauthService;
        _activityRepository = activityRepository;
        _accountRepository = accountRepository;
        _logger = logger;
    }

    public async Task SyncAsync(string userId)
    {
        var account = await _accountRepository.GetAsync(userId, ConnectedAccountProvider.Strava)
            ?? throw new ConnectedAccountNotFoundException(userId);

        if (account.Status == ConnectedAccountStatus.NeedsReconnect)
            return;

        var accessToken = await EnsureFreshTokenAsync(account);

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

    private async Task<string> EnsureFreshTokenAsync(ConnectedAccount account)
    {
        if (account.TokenExpiresAt is { } expiresAt && expiresAt > DateTime.UtcNow.AddMinutes(5))
            return account.AccessToken;

        StravaTokenResponse refreshed;
        try
        {
            refreshed = await _oauthService.RefreshTokenAsync(account.RefreshToken!);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Strava token refresh failed for user {UserId}; flipping account to NeedsReconnect", account.UserId);
            account.Status = ConnectedAccountStatus.NeedsReconnect;
            await _accountRepository.UpsertAsync(account);
            throw;
        }

        account.AccessToken = refreshed.AccessToken;
        account.RefreshToken = refreshed.RefreshToken;
        account.TokenExpiresAt = DateTimeOffset.FromUnixTimeSeconds(refreshed.ExpiresAtUnix).UtcDateTime;
        await _accountRepository.UpsertAsync(account);

        return account.AccessToken;
    }
}
