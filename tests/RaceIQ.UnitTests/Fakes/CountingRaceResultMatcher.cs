using RaceIQ.Application;
using RaceIQ.Domain;

namespace RaceIQ.UnitTests.Fakes;

// Wraps a real matcher and counts how many times FindMatch was called per
// RaceResult.Id, so a test can prove a result was (or wasn't) matched more than
// once - e.g. by both the initial upsert pass and the retry-unmatched pass.
public class CountingRaceResultMatcher : IRaceResultMatcher
{
    private readonly IRaceResultMatcher _inner;

    public CountingRaceResultMatcher(IRaceResultMatcher inner)
    {
        _inner = inner;
    }

    public Dictionary<int, int> CallCountsByResultId { get; } = new();

    public Activity? FindMatch(RaceResult result, IReadOnlyList<Activity> candidates)
    {
        CallCountsByResultId[result.Id] = CallCountsByResultId.GetValueOrDefault(result.Id) + 1;
        return _inner.FindMatch(result, candidates);
    }
}
