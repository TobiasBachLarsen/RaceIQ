using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using RaceIQ.Application;
using RaceIQ.Domain;
using RaceIQ.Infrastructure;
using RaceIQ.Infrastructure.ZwiftPower;
using Xunit;

namespace RaceIQ.IntegrationTests;

public class ZwiftPowerSyncTests : IClassFixture<RaceIQApiFactory>
{
    private readonly RaceIQApiFactory _factory;

    public ZwiftPowerSyncTests(RaceIQApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SyncAsync_MatchesResultToExistingActivity()
    {
        // Wrapped in a top-level "data" object, not a bare array - ZwiftPowerApiClient
        // (Task 6) expects the ZwiftPower cache3 envelope shape, not a bare array.
        _factory.ZwiftPowerApiResponder = _ => FakeHttpMessageHandler.JsonResponse(HttpStatusCode.OK,
            """{"data":[{"race_id":999,"event_name":"Crit Race","event_date":1756742400,"category":"B","position":4,"time":2700}]}""");

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = "zpsync@example.com", Email = "zpsync@example.com" };
        await userManager.CreateAsync(user, "P@ssw0rd!");

        var accountRepository = scope.ServiceProvider.GetRequiredService<IConnectedAccountRepository>();
        await accountRepository.UpsertAsync(new ConnectedAccount
        {
            UserId = user.Id,
            Provider = ConnectedAccountProvider.ZwiftPower,
            AccessToken = "fake-cookie",
            ExternalAccountId = "12345"
        });

        var activityRepository = scope.ServiceProvider.GetRequiredService<IActivityRepository>();
        var activity = await activityRepository.AddAsync(new Activity
        {
            UserId = user.Id,
            StravaActivityId = "555",
            Name = "Zwift crit race",
            StartedAt = DateTimeOffset.FromUnixTimeSeconds(1756742400).UtcDateTime.AddMinutes(2),
            Duration = TimeSpan.FromSeconds(2700),
            StreamDataJson = "[]"
        });

        var syncService = scope.ServiceProvider.GetRequiredService<IZwiftPowerSyncService>();
        await syncService.SyncAsync(user.Id);

        var raceResultRepository = scope.ServiceProvider.GetRequiredService<IRaceResultRepository>();
        var results = await raceResultRepository.GetForActivityAsync(activity.Id);
        Assert.Single(results);
        Assert.Equal("B", results[0].Category);
        Assert.Equal(4, results[0].Position);

        // sync again - must not create a duplicate RaceResult
        await syncService.SyncAsync(user.Id);
        var unmatched = await raceResultRepository.GetUnmatchedForUserAsync(user.Id);
        Assert.Empty(unmatched);
    }
}
