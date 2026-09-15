using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaceIQ.Application;
using RaceIQ.Domain;
using RaceIQ.Infrastructure;
using Xunit;

namespace RaceIQ.IntegrationTests;

public class CredentialEncryptionTests : IClassFixture<RaceIQApiFactory>
{
    private readonly RaceIQApiFactory _factory;

    public CredentialEncryptionTests(RaceIQApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Credentials_AreEncryptedAtRest_ButReadBackInClear()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = "enc@example.com", Email = "enc@example.com" };
        await userManager.CreateAsync(user, "P@ssw0rd!");

        const string secret = "super-secret-strava-token";
        var accountRepository = scope.ServiceProvider.GetRequiredService<IConnectedAccountRepository>();
        await accountRepository.UpsertAsync(new ConnectedAccount
        {
            UserId = user.Id,
            Provider = ConnectedAccountProvider.Strava,
            AccessToken = secret,
            RefreshToken = "super-secret-refresh"
        });

        // Read the raw column straight from Postgres, bypassing the EF value converter.
        var db = scope.ServiceProvider.GetRequiredService<RaceIQDbContext>();
        var storedRaw = await db.Database
            .SqlQuery<string>($"SELECT \"AccessToken\" AS \"Value\" FROM \"ConnectedAccounts\" WHERE \"UserId\" = {user.Id}")
            .SingleAsync();

        Assert.NotEqual(secret, storedRaw);                 // not plaintext on disk
        Assert.DoesNotContain("strava-token", storedRaw);   // ciphertext, not obfuscation

        // The application still reads it back in clear through the converter.
        var account = await accountRepository.GetAsync(user.Id, ConnectedAccountProvider.Strava);
        Assert.Equal(secret, account!.AccessToken);
        Assert.Equal("super-secret-refresh", account.RefreshToken);
    }
}
