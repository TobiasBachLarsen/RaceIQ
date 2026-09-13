using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using RaceIQ.Application;
using RaceIQ.Domain;
using RaceIQ.Infrastructure;
using RaceIQ.Infrastructure.Strava;
using Xunit;

namespace RaceIQ.IntegrationTests;

public class SyncAndAnalyzeFlowTests : IClassFixture<RaceIQApiFactory>
{
    private readonly RaceIQApiFactory _factory;

    public SyncAndAnalyzeFlowTests(RaceIQApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task FullFlow_SyncThenAnalyze_ProducesPersistedReport()
    {
        _factory.StravaApiResponder = req =>
        {
            if (req.RequestUri!.AbsolutePath.Contains("/streams"))
            {
                return FakeHttpMessageHandler.JsonResponse(HttpStatusCode.OK,
                    """{"time":{"data":[0,30,60]},"watts":{"data":[150,280,190]},"heartrate":{"data":[120,140,130]},"cadence":{"data":[85,92,88]},"altitude":{"data":[10,12,15]},"distance":{"data":[0,200,410]}}""");
            }

            return FakeHttpMessageHandler.JsonResponse(HttpStatusCode.OK,
                """[{"id":555,"name":"Zwift crit race","start_date":"2026-09-01T18:00:00Z","distance":30000,"moving_time":2700,"total_elevation_gain":250,"average_watts":210,"weighted_average_watts":230,"average_heartrate":155,"average_cadence":90,"workout_type":11}]""");
        };
        _factory.ClaudeAnalysisText = "You paced the opening lap too hard and faded by lap three.";

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = "flow@example.com", Email = "flow@example.com" };
        await userManager.CreateAsync(user, "P@ssw0rd!");

        var accountRepository = scope.ServiceProvider.GetRequiredService<IConnectedAccountRepository>();
        await accountRepository.UpsertAsync(new ConnectedAccount
        {
            UserId = user.Id,
            Provider = ConnectedAccountProvider.Strava,
            AccessToken = "fake-token",
            RefreshToken = "fake-refresh",
            TokenExpiresAt = DateTime.UtcNow.AddHours(1)
        });

        var syncService = scope.ServiceProvider.GetRequiredService<IStravaSyncService>();
        await syncService.SyncAsync(user.Id);

        var activityRepository = scope.ServiceProvider.GetRequiredService<IActivityRepository>();
        var activities = await activityRepository.GetAllForUserAsync(user.Id);
        Assert.Single(activities);
        Assert.Equal("Zwift crit race", activities[0].Name);
        Assert.True(activities[0].IsRace);

        var analysisService = scope.ServiceProvider.GetRequiredService<IActivityAnalysisService>();
        var report = await analysisService.AnalyzeAsync(activities[0].Id, user.Id);
        Assert.Equal("You paced the opening lap too hard and faded by lap three.", report.ReportText);

        // sync again - must not create a duplicate activity
        await syncService.SyncAsync(user.Id);
        var activitiesAfterResync = await activityRepository.GetAllForUserAsync(user.Id);
        Assert.Single(activitiesAfterResync);
    }
}
