using Microsoft.Extensions.Logging.Abstractions;
using RaceIQ.Domain;
using RaceIQ.Infrastructure.Strava;
using RaceIQ.UnitTests.Fakes;
using Xunit;

namespace RaceIQ.UnitTests;

public class StravaSyncServiceTests
{
    private static StravaTokenResponse Token(string access) =>
        new(access, "refresh-new", DateTimeOffset.UtcNow.AddHours(6).ToUnixTimeSeconds());

    private static async Task<(StravaSyncService service, FakeConnectedAccountRepository accounts, FakeStravaApiClient api, FakeStravaOAuthService oauth)>
        BuildAsync(ConnectedAccount account, Func<string, StravaTokenResponse> refresh)
    {
        var accounts = new FakeConnectedAccountRepository();
        await accounts.UpsertAsync(account);
        var api = new FakeStravaApiClient();
        var oauth = new FakeStravaOAuthService(refresh);
        var service = new StravaSyncService(
            api, oauth, new FakeActivityRepository(), accounts, NullLogger<StravaSyncService>.Instance);
        return (service, accounts, api, oauth);
    }

    private static ConnectedAccount Account(DateTime expiresAt, ConnectedAccountStatus status = ConnectedAccountStatus.Connected) => new()
    {
        UserId = "user-1",
        Provider = ConnectedAccountProvider.Strava,
        AccessToken = "fresh-token",
        RefreshToken = "refresh-old",
        TokenExpiresAt = expiresAt,
        Status = status
    };

    [Fact]
    public async Task SyncAsync_TokenStillFresh_DoesNotRefresh()
    {
        var (service, _, api, oauth) = await BuildAsync(
            Account(DateTime.UtcNow.AddHours(2)), _ => Token("should-not-be-used"));

        await service.SyncAsync("user-1");

        Assert.Equal(0, oauth.RefreshCallCount);
        Assert.Equal("fresh-token", api.LastAccessTokenUsed);
    }

    [Fact]
    public async Task SyncAsync_TokenNearExpiry_RefreshesAndPersistsNewToken()
    {
        var (service, accounts, api, oauth) = await BuildAsync(
            Account(DateTime.UtcNow.AddMinutes(2)), _ => Token("refreshed-token"));

        await service.SyncAsync("user-1");

        Assert.Equal(1, oauth.RefreshCallCount);
        Assert.Equal("refreshed-token", api.LastAccessTokenUsed);

        var stored = await accounts.GetAsync("user-1", ConnectedAccountProvider.Strava);
        Assert.Equal("refreshed-token", stored!.AccessToken);
        Assert.Equal("refresh-new", stored.RefreshToken);
        Assert.Equal(ConnectedAccountStatus.Connected, stored.Status);
    }

    [Fact]
    public async Task SyncAsync_RefreshFails_FlipsToNeedsReconnectAndRethrows()
    {
        var (service, accounts, _, _) = await BuildAsync(
            Account(DateTime.UtcNow.AddMinutes(2)),
            _ => throw new InvalidOperationException("refresh rejected"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SyncAsync("user-1"));

        var stored = await accounts.GetAsync("user-1", ConnectedAccountProvider.Strava);
        Assert.Equal(ConnectedAccountStatus.NeedsReconnect, stored!.Status);
    }

    [Fact]
    public async Task SyncAsync_AccountAlreadyNeedsReconnect_ReturnsWithoutCallingStrava()
    {
        var (service, _, api, oauth) = await BuildAsync(
            Account(DateTime.UtcNow.AddHours(2), ConnectedAccountStatus.NeedsReconnect),
            _ => Token("should-not-be-used"));

        await service.SyncAsync("user-1");

        Assert.Equal(0, oauth.RefreshCallCount);
        Assert.Null(api.LastAccessTokenUsed);
    }
}
