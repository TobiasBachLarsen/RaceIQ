using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using RaceIQ.Application;
using RaceIQ.Domain;
using RaceIQ.Infrastructure;
using RaceIQ.Infrastructure.ZwiftRacing;
using Xunit;

namespace RaceIQ.IntegrationTests;

public class ZwiftRacingSyncTests : IClassFixture<RaceIQApiFactory>
{
    private readonly RaceIQApiFactory _factory;

    public ZwiftRacingSyncTests(RaceIQApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SyncAsync_MatchesResultToExistingActivity()
    {
        // ZwiftRacingApiClient (Task 9) makes two HTTP calls per sync: first it
        // discovers recent race ids from /public/riders/{id}, then it fetches each
        // race's detail from /public/results/{raceId}. The fake responder below
        // dispatches on the request path to serve both shapes, same as
        // ZwiftRacingApiClientTests (unit tests, Task 9) does.
        _factory.ZwiftRacingApiResponder = req =>
        {
            if (req.RequestUri!.AbsolutePath.Contains("/riders/"))
                return FakeHttpMessageHandler.JsonResponse(HttpStatusCode.OK,
                    """{"recent_race_ids":[777]}""");

            return FakeHttpMessageHandler.JsonResponse(HttpStatusCode.OK,
                """
                {"eventId":777,"title":"Club Crit","time":1756742400,
                 "results":[
                   {"riderId":12345,"position":4,"category":"B","time":2700.0,"ratingDelta":18.5}
                 ]}
                """);
        };

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = "zrsync@example.com", Email = "zrsync@example.com" };
        await userManager.CreateAsync(user, "P@ssw0rd!");

        var accountRepository = scope.ServiceProvider.GetRequiredService<IConnectedAccountRepository>();
        await accountRepository.UpsertAsync(new ConnectedAccount
        {
            UserId = user.Id,
            Provider = ConnectedAccountProvider.ZwiftRacing,
            AccessToken = "fake-api-key",
            ExternalAccountId = "12345"
        });

        var activityRepository = scope.ServiceProvider.GetRequiredService<IActivityRepository>();
        var activity = await activityRepository.AddAsync(new Activity
        {
            UserId = user.Id,
            StravaActivityId = "556",
            Name = "Zwift club crit",
            StartedAt = DateTimeOffset.FromUnixTimeSeconds(1756742400).UtcDateTime.AddMinutes(2),
            Duration = TimeSpan.FromSeconds(2700),
            StreamDataJson = "[]"
        });

        var syncService = scope.ServiceProvider.GetRequiredService<IZwiftRacingSyncService>();
        await syncService.SyncAsync(user.Id);

        var raceResultRepository = scope.ServiceProvider.GetRequiredService<IRaceResultRepository>();
        var results = await raceResultRepository.GetForActivityAsync(activity.Id);
        Assert.Single(results);
        Assert.Equal("B", results[0].Category);
        Assert.Equal(4, results[0].Position);
        Assert.Equal(18.5, results[0].RatingChange);

        // Sync again - must not create a duplicate RaceResult. GetUnmatchedForUserAsync
        // alone can't prove this: RaceResultMatcher matches unconditionally when there's
        // exactly one candidate activity in the date window, so even a duplicate row from
        // a broken de-dup key would also get matched and linked, leaving nothing
        // unmatched. Re-check GetForActivityAsync instead - a broken de-dup key would
        // produce 2 results here, not 1.
        await syncService.SyncAsync(user.Id);
        var resultsAfterResync = await raceResultRepository.GetForActivityAsync(activity.Id);
        Assert.Single(resultsAfterResync);
    }
}
