using RaceIQ.Application;
using RaceIQ.Application.Exceptions;
using RaceIQ.Domain;

namespace RaceIQ.Infrastructure.ZwiftRacing;

public class ZwiftRacingSyncService : IZwiftRacingSyncService
{
    private readonly IZwiftRacingApiClient _apiClient;
    private readonly IRaceResultRepository _raceResultRepository;
    private readonly IRaceResultMatcher _matcher;
    private readonly IActivityRepository _activityRepository;
    private readonly IConnectedAccountRepository _accountRepository;

    public ZwiftRacingSyncService(
        IZwiftRacingApiClient apiClient,
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
        var account = await _accountRepository.GetAsync(userId, ConnectedAccountProvider.ZwiftRacing)
            ?? throw new ConnectedAccountNotFoundException(userId);

        if (account.Status == ConnectedAccountStatus.NeedsReconnect)
            return;

        IReadOnlyList<ZwiftRacingRaceResult> results;
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
                Provider = ConnectedAccountProvider.ZwiftRacing,
                ProviderResultId = result.RaceId,
                EventName = result.EventTitle,
                EventDate = result.EventTime,
                Category = result.Category,
                Position = result.Position,
                FieldSize = result.FieldSize,
                Duration = result.Duration,
                RatingChange = result.RatingDelta
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
