using RaceIQ.Application;
using RaceIQ.Domain;
using Xunit;

namespace RaceIQ.UnitTests;

public class RaceResultMatcherTests
{
    private static Activity MakeActivity(DateTime startedAt, TimeSpan duration) => new()
    {
        UserId = "user-1",
        StravaActivityId = Guid.NewGuid().ToString(),
        Name = "Ride",
        StreamDataJson = "[]",
        StartedAt = startedAt,
        Duration = duration
    };

    private static RaceResult MakeResult(DateTime eventDate, TimeSpan? duration = null) => new()
    {
        UserId = "user-1",
        Provider = ConnectedAccountProvider.ZwiftPower,
        ProviderResultId = "race-1",
        EventName = "Crit Race",
        EventDate = eventDate,
        Duration = duration
    };

    [Fact]
    public void FindMatch_SingleActivityWithinWindow_Matches()
    {
        var eventDate = new DateTime(2026, 9, 1, 18, 0, 0, DateTimeKind.Utc);
        var activity = MakeActivity(eventDate.AddMinutes(2), TimeSpan.FromMinutes(45));
        var matcher = new RaceResultMatcher();

        var match = matcher.FindMatch(MakeResult(eventDate), new[] { activity });

        Assert.Same(activity, match);
    }

    [Fact]
    public void FindMatch_NoActivityWithinWindow_ReturnsNull()
    {
        var eventDate = new DateTime(2026, 9, 1, 18, 0, 0, DateTimeKind.Utc);
        var activity = MakeActivity(eventDate.AddHours(3), TimeSpan.FromMinutes(45));
        var matcher = new RaceResultMatcher();

        var match = matcher.FindMatch(MakeResult(eventDate), new[] { activity });

        Assert.Null(match);
    }

    [Fact]
    public void FindMatch_TwoCandidatesWithinWindow_PicksClosestStartTime()
    {
        var eventDate = new DateTime(2026, 9, 1, 18, 0, 0, DateTimeKind.Utc);
        var closer = MakeActivity(eventDate.AddMinutes(1), TimeSpan.FromMinutes(45));
        var farther = MakeActivity(eventDate.AddMinutes(20), TimeSpan.FromMinutes(45));
        var matcher = new RaceResultMatcher();

        var match = matcher.FindMatch(MakeResult(eventDate), new[] { farther, closer });

        Assert.Same(closer, match);
    }

    [Fact]
    public void FindMatch_DurationProvided_NarrowsToMatchingDuration()
    {
        var eventDate = new DateTime(2026, 9, 1, 18, 0, 0, DateTimeKind.Utc);
        // Both start within the 30-minute window, but only one has a duration close
        // to the result's reported 45 minutes.
        var wrongDuration = MakeActivity(eventDate.AddMinutes(5), TimeSpan.FromMinutes(90));
        var rightDuration = MakeActivity(eventDate.AddMinutes(10), TimeSpan.FromMinutes(44));
        var matcher = new RaceResultMatcher();

        var match = matcher.FindMatch(
            MakeResult(eventDate, TimeSpan.FromMinutes(45)),
            new[] { wrongDuration, rightDuration });

        Assert.Same(rightDuration, match);
    }

    [Fact]
    public void FindMatch_SingleActivityWithDurationMismatch_StillMatches()
    {
        var eventDate = new DateTime(2026, 9, 1, 18, 0, 0, DateTimeKind.Utc);
        // Single activity within date window, but duration doesn't match the reported duration.
        // Single candidate is already a strong signal, so should match despite duration mismatch.
        var activity = MakeActivity(eventDate.AddMinutes(5), TimeSpan.FromMinutes(90));
        var matcher = new RaceResultMatcher();

        var match = matcher.FindMatch(
            MakeResult(eventDate, TimeSpan.FromMinutes(45)),
            new[] { activity });

        Assert.Same(activity, match);
    }

    [Fact]
    public void FindMatch_MultipleCandidatesNoDurationMatch_ReturnsNull()
    {
        var eventDate = new DateTime(2026, 9, 1, 18, 0, 0, DateTimeKind.Utc);
        // Multiple candidates within date window, but result has a duration that
        // doesn't match any of them within 20% tolerance. Should return null rather
        // than silently guessing by proximity.
        var wrongDuration1 = MakeActivity(eventDate.AddMinutes(5), TimeSpan.FromMinutes(90));
        var wrongDuration2 = MakeActivity(eventDate.AddMinutes(10), TimeSpan.FromMinutes(100));
        var matcher = new RaceResultMatcher();

        var match = matcher.FindMatch(
            MakeResult(eventDate, TimeSpan.FromMinutes(45)),
            new[] { wrongDuration1, wrongDuration2 });

        Assert.Null(match);
    }

    [Fact]
    public void FindMatch_EmptyCandidateList_ReturnsNull()
    {
        var eventDate = new DateTime(2026, 9, 1, 18, 0, 0, DateTimeKind.Utc);
        var matcher = new RaceResultMatcher();

        var match = matcher.FindMatch(MakeResult(eventDate), new Activity[] { });

        Assert.Null(match);
    }

    [Fact]
    public void FindMatch_BoundaryDateWindow_Exactly30MinutesMatches()
    {
        var eventDate = new DateTime(2026, 9, 1, 18, 0, 0, DateTimeKind.Utc);
        var activity = MakeActivity(eventDate.AddMinutes(30), TimeSpan.FromMinutes(45));
        var matcher = new RaceResultMatcher();

        var match = matcher.FindMatch(MakeResult(eventDate), new[] { activity });

        Assert.Same(activity, match);
    }

    [Fact]
    public void FindMatch_BoundaryDateWindow_MoreThan30MinutesDoesNotMatch()
    {
        var eventDate = new DateTime(2026, 9, 1, 18, 0, 0, DateTimeKind.Utc);
        var activity = MakeActivity(eventDate.AddMinutes(30).AddTicks(1), TimeSpan.FromMinutes(45));
        var matcher = new RaceResultMatcher();

        var match = matcher.FindMatch(MakeResult(eventDate), new[] { activity });

        Assert.Null(match);
    }
}
