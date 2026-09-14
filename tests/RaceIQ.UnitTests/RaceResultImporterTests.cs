using Microsoft.Extensions.Logging.Abstractions;
using RaceIQ.Application;
using RaceIQ.Domain;
using RaceIQ.UnitTests.Fakes;
using Xunit;

namespace RaceIQ.UnitTests;

public class RaceResultImporterTests
{
    private static RaceResult MakeIncoming(DateTime eventDate, TimeSpan? duration = null) => new()
    {
        UserId = "user-1",
        Provider = ConnectedAccountProvider.ZwiftPower,
        ProviderResultId = "race-1",
        EventName = "Crit Race",
        EventDate = eventDate,
        Duration = duration
    };

    [Fact]
    public async Task ImportAsync_ResultArrivesBeforeItsActivity_LinksOnNextImportRetryPass()
    {
        var activityRepository = new FakeActivityRepository();
        var raceResultRepository = new FakeRaceResultRepository();
        var importer = new RaceResultImporter(
            raceResultRepository, new RaceResultMatcher(), activityRepository, NullLogger<RaceResultImporter>.Instance);

        var eventDate = new DateTime(2026, 9, 1, 18, 0, 0, DateTimeKind.Utc);
        var incoming = new List<RaceResult> { MakeIncoming(eventDate, TimeSpan.FromMinutes(45)) };

        // First sync: no matching Strava activity has arrived yet.
        await importer.ImportAsync("user-1", incoming);
        var unmatchedAfterFirstImport = await raceResultRepository.GetUnmatchedForUserAsync("user-1");
        Assert.Single(unmatchedAfterFirstImport);

        // The matching activity syncs afterwards.
        var activity = await activityRepository.AddAsync(new Activity
        {
            UserId = "user-1",
            StravaActivityId = "1",
            Name = "Crit Race",
            StartedAt = eventDate.AddMinutes(2),
            Duration = TimeSpan.FromMinutes(45),
            StreamDataJson = "[]"
        });

        // Next sync brings no new results, but the retry pass should link the earlier one.
        await importer.ImportAsync("user-1", Array.Empty<RaceResult>());

        Assert.Empty(await raceResultRepository.GetUnmatchedForUserAsync("user-1"));
        Assert.Single(await raceResultRepository.GetForActivityAsync(activity.Id));
    }

    [Fact]
    public async Task ImportAsync_ResultUpsertedThisPass_IsNotReMatchedByRetryLoop()
    {
        // No activities at all, so the incoming result can't match on the initial pass -
        // it stays unmatched and would also show up in GetUnmatchedForUserAsync's retry
        // query within the very same ImportAsync call.
        var activityRepository = new FakeActivityRepository();
        var raceResultRepository = new FakeRaceResultRepository();
        var countingMatcher = new CountingRaceResultMatcher(new RaceResultMatcher());
        var importer = new RaceResultImporter(
            raceResultRepository, countingMatcher, activityRepository, NullLogger<RaceResultImporter>.Instance);

        var eventDate = new DateTime(2026, 9, 1, 18, 0, 0, DateTimeKind.Utc);
        var incoming = new List<RaceResult> { MakeIncoming(eventDate) };

        await importer.ImportAsync("user-1", incoming);

        var stored = Assert.Single(await raceResultRepository.GetUnmatchedForUserAsync("user-1"));
        Assert.Equal(1, countingMatcher.CallCountsByResultId[stored.Id]);
    }

    [Fact]
    public async Task ImportAsync_MatchingActivityAlreadyExists_UpsertsAndLinksInOnePass()
    {
        var activityRepository = new FakeActivityRepository();
        var raceResultRepository = new FakeRaceResultRepository();
        var importer = new RaceResultImporter(
            raceResultRepository, new RaceResultMatcher(), activityRepository, NullLogger<RaceResultImporter>.Instance);

        var eventDate = new DateTime(2026, 9, 1, 18, 0, 0, DateTimeKind.Utc);
        var activity = await activityRepository.AddAsync(new Activity
        {
            UserId = "user-1",
            StravaActivityId = "1",
            Name = "Crit Race",
            StartedAt = eventDate.AddMinutes(2),
            Duration = TimeSpan.FromMinutes(45),
            StreamDataJson = "[]"
        });

        var incoming = new List<RaceResult>
        {
            new()
            {
                UserId = "user-1",
                Provider = ConnectedAccountProvider.ZwiftPower,
                ProviderResultId = "race-1",
                EventName = "Crit Race",
                EventDate = eventDate,
                Category = "B",
                Position = 4,
                Duration = TimeSpan.FromMinutes(45)
            }
        };

        await importer.ImportAsync("user-1", incoming);

        var linked = Assert.Single(await raceResultRepository.GetForActivityAsync(activity.Id));
        Assert.Equal("B", linked.Category);
        Assert.Equal(4, linked.Position);
    }
}
