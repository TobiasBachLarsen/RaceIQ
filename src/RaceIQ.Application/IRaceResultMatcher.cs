using RaceIQ.Domain;

namespace RaceIQ.Application;

public interface IRaceResultMatcher
{
    Activity? FindMatch(RaceResult result, IReadOnlyList<Activity> candidates);
}
