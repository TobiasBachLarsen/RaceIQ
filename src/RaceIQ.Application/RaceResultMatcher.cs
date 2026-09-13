using RaceIQ.Domain;

namespace RaceIQ.Application;

public class RaceResultMatcher : IRaceResultMatcher
{
    private const double DateWindowMinutes = 30;
    private const double DurationToleranceFraction = 0.2;

    public Activity? FindMatch(RaceResult result, IReadOnlyList<Activity> candidates)
    {
        var withinDate = candidates
            .Where(a => Math.Abs((a.StartedAt - result.EventDate).TotalMinutes) <= DateWindowMinutes)
            .ToList();

        if (withinDate.Count == 0)
            return null;

        // If only one candidate within date window, match it regardless of duration.
        // A single candidate in such a narrow window is already a strong signal.
        if (withinDate.Count == 1)
            return withinDate[0];

        // Multiple candidates: apply duration narrowing if available
        if (result.Duration is { } duration && duration > TimeSpan.Zero)
        {
            var tolerance = duration.TotalSeconds * DurationToleranceFraction;
            var withinDuration = withinDate
                .Where(a => Math.Abs((a.Duration - duration).TotalSeconds) <= tolerance)
                .ToList();

            // If no candidates satisfy the duration tolerance when we have multiple options
            // and a duration was specified, return null rather than guessing by proximity.
            if (withinDuration.Count == 0)
                return null;

            withinDate = withinDuration;
        }

        return withinDate
            .OrderBy(a => Math.Abs((a.StartedAt - result.EventDate).Ticks))
            .First();
    }
}
