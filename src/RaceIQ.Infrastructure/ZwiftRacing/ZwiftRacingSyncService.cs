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
        catch (Exception)
        {
            // Covers both a real network failure (HttpRequestException) and an expired
            // API key that HttpClient's default redirect-following turns into an HTTP 200
            // login-page response: EnsureSuccessStatusCode() passes on that, and the
            // subsequent ReadFromJsonAsync<T> call is what actually throws (JsonException,
            // NotSupportedException depending on the returned content-type, or
            // TaskCanceledException on a slow/broken redirect chain). Any of these shapes
            // means the credential can no longer be used, so treat them the same way.
            account.Status = ConnectedAccountStatus.NeedsReconnect;
            await _accountRepository.UpsertAsync(account);
            return;
        }

        var activities = await _activityRepository.GetAllForUserAsync(userId);
        var matchedResultIds = new HashSet<int>();

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

            matchedResultIds.Add(stored.Id);

            if (stored.ActivityId is null)
            {
                var match = _matcher.FindMatch(stored, activities);
                if (match is not null)
                    await _raceResultRepository.LinkToActivityAsync(stored.Id, match.Id);
            }
        }

        // Retry any still-unmatched results from earlier syncs against this sync's freshly
        // loaded activities - e.g. a race result that arrived before its matching Strava
        // activity had synced. Skip rows we already just upserted above in this same pass.
        var unmatched = await _raceResultRepository.GetUnmatchedForUserAsync(userId);
        foreach (var result in unmatched)
        {
            if (matchedResultIds.Contains(result.Id))
                continue;

            var match = _matcher.FindMatch(result, activities);
            if (match is not null)
                await _raceResultRepository.LinkToActivityAsync(result.Id, match.Id);
        }
    }
}
