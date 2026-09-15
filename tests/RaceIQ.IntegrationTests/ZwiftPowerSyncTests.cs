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
            """{"data":[{"zid":"999","event_title":"Crit Race","event_date":1756742400,"category":"B","pos":4,"time_gun":2700,"f_t":"TYPE_RACE TYPE_RACE "}]}""");

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

        var syncService = scope.ServiceProvider.GetRequiredService<ZwiftPowerSyncService>();
        await syncService.SyncAsync(user.Id);

        var raceResultRepository = scope.ServiceProvider.GetRequiredService<IRaceResultRepository>();
        var results = await raceResultRepository.GetForActivityAsync(activity.Id);
        Assert.Single(results);
        Assert.Equal("B", results[0].Category);
        Assert.Equal(4, results[0].Position);

        // sync again - must not create a duplicate RaceResult. GetUnmatchedForUserAsync
        // alone can't prove this: RaceResultMatcher matches unconditionally when there's
        // exactly one candidate activity in the date window, so even a duplicate row from
        // a broken de-dup key would also get matched and linked, leaving nothing
        // unmatched. Re-check GetForActivityAsync instead - a broken de-dup key would
        // produce 2 results here, not 1.
        await syncService.SyncAsync(user.Id);
        var resultsAfterResync = await raceResultRepository.GetForActivityAsync(activity.Id);
        Assert.Single(resultsAfterResync);
        var unmatched = await raceResultRepository.GetUnmatchedForUserAsync(user.Id);
        Assert.Empty(unmatched);
    }

    [Fact]
    public async Task SyncAsync_ExpiredCookieReturnsLoginPage_FlipsAccountToNeedsReconnect()
    {
        // HttpClient follows redirects by default, so an expired ZwiftPower session cookie
        // doesn't come back as a network error - it comes back as a real HTTP 200 with an
        // HTML login page body. EnsureSuccessStatusCode() passes on that; the failure only
        // shows up when ReadFromJsonAsync tries to parse HTML as JSON. This simulates that
        // exact response shape (200 OK, text/html body) to prove the sync service catches
        // it and flips the account to NeedsReconnect instead of leaking the exception.
        _factory.ZwiftPowerApiResponder = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html><body>Please log in</body></html>", System.Text.Encoding.UTF8, "text/html")
        };

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = "zpexpired@example.com", Email = "zpexpired@example.com" };
        await userManager.CreateAsync(user, "P@ssw0rd!");

        var accountRepository = scope.ServiceProvider.GetRequiredService<IConnectedAccountRepository>();
        await accountRepository.UpsertAsync(new ConnectedAccount
        {
            UserId = user.Id,
            Provider = ConnectedAccountProvider.ZwiftPower,
            AccessToken = "stale-cookie",
            ExternalAccountId = "12345"
        });

        var syncService = scope.ServiceProvider.GetRequiredService<ZwiftPowerSyncService>();
        await syncService.SyncAsync(user.Id);

        var account = await accountRepository.GetAsync(user.Id, ConnectedAccountProvider.ZwiftPower);
        Assert.Equal(ConnectedAccountStatus.NeedsReconnect, account!.Status);
    }
}
