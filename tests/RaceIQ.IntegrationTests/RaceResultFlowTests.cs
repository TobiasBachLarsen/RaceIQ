using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using RaceIQ.Application;
using RaceIQ.Domain;
using RaceIQ.Infrastructure;
using RaceIQ.Infrastructure.Strava;
using RaceIQ.Infrastructure.ZwiftPower;
using RaceIQ.Infrastructure.ZwiftRacing;
using Xunit;

namespace RaceIQ.IntegrationTests;

public class RaceResultFlowTests : IClassFixture<RaceIQApiFactory>
{
    private readonly RaceIQApiFactory _factory;

    public RaceResultFlowTests(RaceIQApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task FullFlow_StravaZwiftPowerAndZwiftRacing_ConvergeOnSameActivity()
    {
        _factory.StravaApiResponder = req =>
        {
            if (req.RequestUri!.AbsolutePath.Contains("/streams"))
                return FakeHttpMessageHandler.JsonResponse(HttpStatusCode.OK,
                    """{"time":{"data":[0,30]},"watts":{"data":[200,210]},"heartrate":{"data":[140,145]},"cadence":{"data":[88,90]},"altitude":{"data":[10,11]},"distance":{"data":[0,300]}}""");

            return FakeHttpMessageHandler.JsonResponse(HttpStatusCode.OK,
                """[{"id":42,"name":"Club Crit","start_date":"2026-09-01T18:00:02Z","distance":30000,"moving_time":2700,"total_elevation_gain":200,"average_watts":210,"weighted_average_watts":220,"average_heartrate":150,"average_cadence":90,"workout_type":11}]""");
        };
        // Wrapped in a top-level "data" object, not a bare array - ZwiftPowerApiClient
        // (Task 6) expects the ZwiftPower cache3 envelope shape, not a bare array.
        // event_date 1788285600 = 2026-09-01T18:00:00Z, matching the Strava activity's
        // start_date above within RaceResultMatcher's 30-minute window.
        _factory.ZwiftPowerApiResponder = _ => FakeHttpMessageHandler.JsonResponse(HttpStatusCode.OK,
            """{"data":[{"zid":"999","event_title":"Club Crit","event_date":1788285600,"category":"B","pos":4,"time_gun":2700,"f_t":"TYPE_RACE TYPE_RACE "}]}""");
        // ZwiftRacingApiClient (Task 9) makes two HTTP calls per sync: first it discovers
        // recent race ids from /public/riders/{id} (still the brief's original
        // recent_race_ids guess - unresolved by Task 9's research), then it fetches each
        // race's detail from /public/results/{raceId} using the real camelCase field
        // names (eventId, title, time, riderId, ratingDelta) confirmed during Task 9.
        _factory.ZwiftRacingApiResponder = req =>
        {
            if (req.RequestUri!.AbsolutePath.Contains("/riders/"))
                return FakeHttpMessageHandler.JsonResponse(HttpStatusCode.OK, """{"recent_race_ids":[777]}""");

            return FakeHttpMessageHandler.JsonResponse(HttpStatusCode.OK, """
                {"eventId":777,"title":"Club Crit","time":1788285600,
                 "results":[{"riderId":555,"position":4,"category":"B","time":2700.0,"ratingDelta":12.0}]}
                """);
        };
        _factory.ClaudeAnalysisText = "Du kørte et fint kategori B-løb.";

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = "race@example.com", Email = "race@example.com" };
        await userManager.CreateAsync(user, "P@ssw0rd!");

        var accountRepository = scope.ServiceProvider.GetRequiredService<IConnectedAccountRepository>();
        await accountRepository.UpsertAsync(new ConnectedAccount
        {
            UserId = user.Id, Provider = ConnectedAccountProvider.Strava,
            AccessToken = "fake", RefreshToken = "fake", TokenExpiresAt = DateTime.UtcNow.AddHours(1)
        });
        await accountRepository.UpsertAsync(new ConnectedAccount
        {
            UserId = user.Id, Provider = ConnectedAccountProvider.ZwiftPower,
            AccessToken = "fake-cookie", ExternalAccountId = "555"
        });
        await accountRepository.UpsertAsync(new ConnectedAccount
        {
            UserId = user.Id, Provider = ConnectedAccountProvider.ZwiftRacing,
            AccessToken = "fake-key", ExternalAccountId = "555"
        });

        await scope.ServiceProvider.GetRequiredService<StravaSyncService>().SyncAsync(user.Id);
        await scope.ServiceProvider.GetRequiredService<ZwiftPowerSyncService>().SyncAsync(user.Id);
        await scope.ServiceProvider.GetRequiredService<ZwiftRacingSyncService>().SyncAsync(user.Id);

        var activityRepository = scope.ServiceProvider.GetRequiredService<IActivityRepository>();
        var activities = await activityRepository.GetAllForUserAsync(user.Id);
        Assert.Single(activities);
        Assert.True(activities[0].IsRace);

        var raceResultRepository = scope.ServiceProvider.GetRequiredService<IRaceResultRepository>();
        var raceResults = await raceResultRepository.GetForActivityAsync(activities[0].Id);
        // Both ZwiftPower and ZwiftRacing independently matched this same activity -
        // both rows must survive, not just one.
        Assert.Equal(2, raceResults.Count);
        Assert.All(raceResults, r => Assert.Equal("B", r.Category));
        Assert.Contains(raceResults, r => r.RatingChange == 12.0);

        var analysisService = scope.ServiceProvider.GetRequiredService<IActivityAnalysisService>();
        var report = await analysisService.AnalyzeAsync(activities[0].Id, user.Id);
        Assert.Equal("Du kørte et fint kategori B-løb.", report.ReportText);
    }
}
