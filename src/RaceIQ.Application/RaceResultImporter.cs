using RaceIQ.Domain;

namespace RaceIQ.Application;

// Shared upsert -> match-if-unlinked -> retry-previously-unmatched algorithm used by
// every provider that syncs race results (ZwiftPower, ZwiftRacing, ...). Each provider
// service is responsible only for fetching its own results and projecting them onto
// RaceResult; this class owns what happens to them afterwards.
public class RaceResultImporter
{
    private readonly IRaceResultRepository _raceResultRepository;
    private readonly IRaceResultMatcher _matcher;
    private readonly IActivityRepository _activityRepository;

    public RaceResultImporter(
        IRaceResultRepository raceResultRepository,
        IRaceResultMatcher matcher,
        IActivityRepository activityRepository)
    {
        _raceResultRepository = raceResultRepository;
        _matcher = matcher;
        _activityRepository = activityRepository;
    }

    public async Task ImportAsync(string userId, IReadOnlyList<RaceResult> incoming)
    {
        var activities = await _activityRepository.GetAllForUserAsync(userId);
        var matchedResultIds = new HashSet<int>();

        foreach (var result in incoming)
        {
            var stored = await _raceResultRepository.UpsertAsync(result);
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
