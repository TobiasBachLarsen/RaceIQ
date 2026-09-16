using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using RaceIQ.Application;
using RaceIQ.Domain;
using RaceIQ.Infrastructure;
using Xunit;

namespace RaceIQ.IntegrationTests;

public class RecoveryDayRepositoryTests : IClassFixture<RaceIQApiFactory>
{
    private readonly RaceIQApiFactory _factory;

    public RecoveryDayRepositoryTests(RaceIQApiFactory factory)
    {
        _factory = factory;
    }

    private static RecoveryDay Day(string userId, DateOnly date, int score, string recordId = "cycle-1") => new()
    {
        UserId = userId,
        Date = date,
        RecoveryScore = score,
        HrvMs = 55.5,
        RestingHeartRate = 48,
        ProviderRecordId = recordId
    };

    [Fact]
    public async Task UpsertAsync_SameUserAndDate_UpdatesInsteadOfDuplicating()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = "recovery@example.com", Email = "recovery@example.com" };
        await userManager.CreateAsync(user, "P@ssw0rd!");

        var repository = scope.ServiceProvider.GetRequiredService<IRecoveryDayRepository>();
        var date = new DateOnly(2026, 9, 10);

        await repository.UpsertAsync(Day(user.Id, date, 40));
        await repository.UpsertAsync(Day(user.Id, date, 72));
        await repository.UpsertAsync(Day(user.Id, date.AddDays(-1), 30, "cycle-0"));

        var stored = await repository.GetForDateAsync(user.Id, date);
        Assert.NotNull(stored);
        Assert.Equal(72, stored!.RecoveryScore);

        var latest = await repository.GetLatestAsync(user.Id);
        Assert.Equal(date, latest!.Date);

        var db = scope.ServiceProvider.GetRequiredService<RaceIQDbContext>();
        Assert.Equal(2, db.RecoveryDays.Count(d => d.UserId == user.Id));

        Assert.Null(await repository.GetForDateAsync(user.Id, date.AddDays(5)));
    }
}
