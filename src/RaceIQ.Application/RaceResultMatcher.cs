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

        if (result.Duration is { } duration && duration > TimeSpan.Zero)
        {
            var tolerance = duration.TotalSeconds * DurationToleranceFraction;
            var withinDuration = withinDate
                .Where(a => Math.Abs((a.Duration - duration).TotalSeconds) <= tolerance)
                .ToList();

            if (withinDuration.Count > 0)
                withinDate = withinDuration;
        }

        return withinDate
            .OrderBy(a => Math.Abs((a.StartedAt - result.EventDate).Ticks))
            .First();
    }
}
