using Microsoft.Extensions.Logging;
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
    private readonly ILogger<RaceResultImporter> _logger;

    public RaceResultImporter(
        IRaceResultRepository raceResultRepository,
        IRaceResultMatcher matcher,
        IActivityRepository activityRepository,
        ILogger<RaceResultImporter> logger)
    {
        _raceResultRepository = raceResultRepository;
        _matcher = matcher;
        _activityRepository = activityRepository;
        _logger = logger;
    }

    public async Task ImportAsync(string userId, IReadOnlyList<RaceResult> incoming)
    {
        var activities = await _activityRepository.GetAllForUserAsync(userId);
        // Every result upserted in this pass, matched or not, so the retry pass below can
        // skip rows it already handled here.
        var processedResultIds = new HashSet<int>();
        var matchedCount = 0;

        foreach (var result in incoming)
        {
            var stored = await _raceResultRepository.UpsertAsync(result);
            processedResultIds.Add(stored.Id);

            if (stored.ActivityId is null)
            {
                var match = _matcher.FindMatch(stored, activities);
                if (match is not null)
                {
                    await _raceResultRepository.LinkToActivityAsync(stored.Id, match.Id);
                    matchedCount++;
                }
            }
        }

        // Retry any still-unmatched results from earlier syncs against this sync's freshly
        // loaded activities - e.g. a race result that arrived before its matching Strava
        // activity had synced. Skip rows we already processed above in this same pass.
        var unmatched = await _raceResultRepository.GetUnmatchedForUserAsync(userId);
        foreach (var result in unmatched)
        {
            if (processedResultIds.Contains(result.Id))
                continue;

            var match = _matcher.FindMatch(result, activities);
            if (match is not null)
            {
                await _raceResultRepository.LinkToActivityAsync(result.Id, match.Id);
                matchedCount++;
            }
        }

        _logger.LogInformation(
            "Imported {ImportedCount} race results for user {UserId}, matched {MatchedCount} to activities",
            incoming.Count, userId, matchedCount);
    }
}
