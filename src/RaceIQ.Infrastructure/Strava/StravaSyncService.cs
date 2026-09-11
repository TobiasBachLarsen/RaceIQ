using System.Text.Json;
using RaceIQ.Application;
using RaceIQ.Application.Exceptions;
using RaceIQ.Domain;

namespace RaceIQ.Infrastructure.Strava;

public class StravaSyncService : IStravaSyncService
{
    private readonly IStravaApiClient _apiClient;
    private readonly IStravaOAuthService _oauthService;
    private readonly IActivityRepository _activityRepository;
    private readonly IConnectedAccountRepository _accountRepository;

    public StravaSyncService(
        IStravaApiClient apiClient,
        IStravaOAuthService oauthService,
        IActivityRepository activityRepository,
        IConnectedAccountRepository accountRepository)
    {
        _apiClient = apiClient;
        _oauthService = oauthService;
        _activityRepository = activityRepository;
        _accountRepository = accountRepository;
    }

    public async Task SyncAsync(string userId)
    {
        var account = await _accountRepository.GetAsync(userId, ConnectedAccountProvider.Strava)
            ?? throw new ConnectedAccountNotFoundException(userId);

        if (account.Status == ConnectedAccountStatus.NeedsReconnect)
            throw new StravaReconnectRequiredException(userId);

        var accessToken = await EnsureFreshTokenAsync(account);

        var summaries = await _apiClient.ListRecentActivitiesAsync(accessToken);

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
                StreamDataJson = JsonSerializer.Serialize(stream)
            });
        }
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
        catch (Exception)
        {
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
