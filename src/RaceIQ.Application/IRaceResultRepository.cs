using RaceIQ.Domain;

namespace RaceIQ.Application;

public interface IRaceResultRepository
{
    // Inserts a new RaceResult, or updates the existing one sharing
    // (UserId, Provider, ProviderResultId) if one already exists. Never creates a
    // duplicate for the same result.
    Task<RaceResult> UpsertAsync(RaceResult result);

    // Every RaceResult for the user, matched or not - used for overview totals.
    Task<IReadOnlyList<RaceResult>> GetAllForUserAsync(string userId);

    // Results with no ActivityId yet — candidates for (re-)matching on the next sync.
    Task<IReadOnlyList<RaceResult>> GetUnmatchedForUserAsync(string userId);

    // Every RaceResult matched to this activity - can be more than one when both
    // ZwiftPower and ZwiftRacing matched the same real-world race.
    Task<IReadOnlyList<RaceResult>> GetForActivityAsync(int activityId);

    // Same as GetForActivityAsync, but for many activities in one query - avoids an
    // N+1 when a page needs race results for a whole list of activities at once.
    Task<IReadOnlyDictionary<int, IReadOnlyList<RaceResult>>> GetForActivitiesAsync(
        string userId, IReadOnlyList<int> activityIds);

    Task LinkToActivityAsync(int raceResultId, int activityId);
}
