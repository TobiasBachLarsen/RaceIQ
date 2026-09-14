using RaceIQ.Application;
using RaceIQ.Application.Exceptions;
using RaceIQ.Domain;

namespace RaceIQ.Infrastructure.ZwiftPower;

public class ZwiftPowerSyncService : IZwiftPowerSyncService
{
    private readonly IZwiftPowerApiClient _apiClient;
    private readonly IRaceResultRepository _raceResultRepository;
    private readonly IRaceResultMatcher _matcher;
    private readonly IActivityRepository _activityRepository;
    private readonly IConnectedAccountRepository _accountRepository;

    public ZwiftPowerSyncService(
        IZwiftPowerApiClient apiClient,
        IRaceResultRepository raceResultRepository,
        IRaceResultMatcher matcher,
        IActivityRepository activityRepository,
        IConnectedAccountRepository accountRepository)
    {
        _apiClient = apiClient;
        _raceResultRepository = raceResultRepository;
        _matcher = matcher;
        _activityRepository = activityRepository;
        _accountRepository = accountRepository;
    }

    public async Task SyncAsync(string userId)
    {
        var account = await _accountRepository.GetAsync(userId, ConnectedAccountProvider.ZwiftPower)
            ?? throw new ConnectedAccountNotFoundException(userId);

        if (account.Status == ConnectedAccountStatus.NeedsReconnect)
            return;

        IReadOnlyList<ZwiftPowerRaceResult> results;
        try
        {
            results = await _apiClient.GetRecentResultsAsync(account.AccessToken, account.ExternalAccountId!);
        }
        catch (HttpRequestException)
        {
            account.Status = ConnectedAccountStatus.NeedsReconnect;
            await _accountRepository.UpsertAsync(account);
            return;
        }

        var activities = await _activityRepository.GetAllForUserAsync(userId);

        foreach (var result in results)
        {
            var stored = await _raceResultRepository.UpsertAsync(new RaceResult
            {
                UserId = userId,
                Provider = ConnectedAccountProvider.ZwiftPower,
                ProviderResultId = result.RaceId,
                EventName = result.EventName,
                EventDate = result.EventDate,
                Category = result.Category,
                Position = result.Position,
                Duration = result.Duration
            });

            if (stored.ActivityId is null)
            {
                var match = _matcher.FindMatch(stored, activities);
                if (match is not null)
                    await _raceResultRepository.LinkToActivityAsync(stored.Id, match.Id);
            }
        }
    }
}
