using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using RaceIQ.Application;
using RaceIQ.Domain;
using RaceIQ.Infrastructure;
using RaceIQ.Infrastructure.Whoop;
using Xunit;

namespace RaceIQ.IntegrationTests;

public class WhoopSyncTests : IClassFixture<RaceIQApiFactory>
{
    private readonly RaceIQApiFactory _factory;

    public WhoopSyncTests(RaceIQApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SyncAsync_StoresRecoveryDaysAndDoesNotDuplicateOnResync()
    {
        _factory.WhoopApiResponder = _ => FakeHttpMessageHandler.JsonResponse(HttpStatusCode.OK,
            """
            {"records":[
              {"cycle_id":93845,"sleep_id":"e3c3f1a2-0000-4000-8000-000000000001","user_id":10129,"created_at":"2026-09-10T05:12:00Z","updated_at":"2026-09-10T05:12:00Z","score_state":"SCORED","score":{"user_calibrating":false,"recovery_score":72,"resting_heart_rate":48,"hrv_rmssd_milli":61.4,"spo2_percentage":96.1,"skin_temp_celsius":33.2}},
              {"cycle_id":93846,"sleep_id":"e3c3f1a2-0000-4000-8000-000000000002","user_id":10129,"created_at":"2026-09-11T05:40:00Z","updated_at":"2026-09-11T05:40:00Z","score_state":"SCORED","score":{"user_calibrating":false,"recovery_score":31,"resting_heart_rate":55,"hrv_rmssd_milli":38.0,"spo2_percentage":95.0,"skin_temp_celsius":33.9}}
            ],"next_token":null}
            """);

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = "whoopsync@example.com", Email = "whoopsync@example.com" };
        await userManager.CreateAsync(user, "P@ssw0rd!");

        var accountRepository = scope.ServiceProvider.GetRequiredService<IConnectedAccountRepository>();
        await accountRepository.UpsertAsync(new ConnectedAccount
        {
            UserId = user.Id,
            Provider = ConnectedAccountProvider.Whoop,
            AccessToken = "whoop-access",
            RefreshToken = "whoop-refresh",
            TokenExpiresAt = DateTime.UtcNow.AddHours(1),
            ExternalAccountId = "10129"
        });

        var syncService = scope.ServiceProvider.GetRequiredService<WhoopSyncService>();
        await syncService.SyncAsync(user.Id);
        await syncService.SyncAsync(user.Id);

        var db = scope.ServiceProvider.GetRequiredService<RaceIQDbContext>();
        var days = db.RecoveryDays.Where(d => d.UserId == user.Id).OrderBy(d => d.Date).ToList();
        Assert.Equal(2, days.Count);
        Assert.Equal(new DateOnly(2026, 9, 10), days[0].Date);
        Assert.Equal(72, days[0].RecoveryScore);
        Assert.Equal(new DateOnly(2026, 9, 11), days[1].Date);
        Assert.Equal(31, days[1].RecoveryScore);

        var recoveryRepository = scope.ServiceProvider.GetRequiredService<IRecoveryDayRepository>();
        var latest = await recoveryRepository.GetLatestAsync(user.Id);
        Assert.Equal(new DateOnly(2026, 9, 11), latest!.Date);
    }
}
