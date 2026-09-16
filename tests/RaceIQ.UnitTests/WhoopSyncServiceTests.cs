using Microsoft.Extensions.Logging.Abstractions;
using RaceIQ.Domain;
using RaceIQ.Infrastructure.Whoop;
using RaceIQ.UnitTests.Fakes;
using Xunit;

namespace RaceIQ.UnitTests;

public class WhoopSyncServiceTests
{
    private static WhoopTokenResponse Token(string access) => new(access, "refresh-new", 3600);

    private static async Task<(WhoopSyncService service, FakeConnectedAccountRepository accounts, FakeWhoopApiClient api, FakeWhoopOAuthService oauth, FakeRecoveryDayRepository recoveries)>
        BuildAsync(ConnectedAccount account, Func<string, WhoopTokenResponse> refresh)
    {
        var accounts = new FakeConnectedAccountRepository();
        await accounts.UpsertAsync(account);
        var api = new FakeWhoopApiClient();
        var oauth = new FakeWhoopOAuthService(refresh);
        var recoveries = new FakeRecoveryDayRepository();
        var service = new WhoopSyncService(api, oauth, recoveries, accounts, NullLogger<WhoopSyncService>.Instance);
        return (service, accounts, api, oauth, recoveries);
    }

    private static ConnectedAccount Account(DateTime expiresAt, ConnectedAccountStatus status = ConnectedAccountStatus.Connected) => new()
    {
        UserId = "user-1",
        Provider = ConnectedAccountProvider.Whoop,
        AccessToken = "fresh-token",
        RefreshToken = "refresh-old",
        TokenExpiresAt = expiresAt,
        Status = status
    };

    [Fact]
    public async Task SyncAsync_StoresOneRecoveryDayPerRecordOnItsDate()
    {
        var (service, _, api, _, recoveries) = await BuildAsync(
            Account(DateTime.UtcNow.AddHours(2)), _ => Token("unused"));
        api.Recoveries.Add(new WhoopRecovery("1", new DateTime(2026, 9, 10, 5, 12, 0, DateTimeKind.Utc), 72, 61.4, 48));
        api.Recoveries.Add(new WhoopRecovery("2", new DateTime(2026, 9, 11, 6, 0, 0, DateTimeKind.Utc), 31, 38.0, 55));

        await service.SyncAsync("user-1");

        Assert.Equal(2, recoveries.All.Count);
        var first = await recoveries.GetForDateAsync("user-1", new DateOnly(2026, 9, 10));
        Assert.Equal(72, first!.RecoveryScore);
        Assert.Equal(61.4, first.HrvMs);
        Assert.Equal(48, first.RestingHeartRate);
        Assert.Equal("1", first.ProviderRecordId);

        // The lookback window is what bounds the request, not "everything ever".
        Assert.InRange(api.LastStartUtc!.Value,
            DateTime.UtcNow.AddDays(-WhoopSyncService.SyncWindowDays).AddMinutes(-1),
            DateTime.UtcNow.AddDays(-WhoopSyncService.SyncWindowDays).AddMinutes(1));
    }

    [Fact]
    public async Task SyncAsync_TokenStillFresh_DoesNotRefresh()
    {
        var (service, _, api, oauth, _) = await BuildAsync(
            Account(DateTime.UtcNow.AddHours(2)), _ => Token("should-not-be-used"));

        await service.SyncAsync("user-1");

        Assert.Equal(0, oauth.RefreshCallCount);
        Assert.Equal("fresh-token", api.LastAccessTokenUsed);
    }

    [Fact]
    public async Task SyncAsync_TokenNearExpiry_RefreshesAndPersistsNewToken()
    {
        var (service, accounts, api, oauth, _) = await BuildAsync(
            Account(DateTime.UtcNow.AddMinutes(2)), _ => Token("refreshed-token"));

        await service.SyncAsync("user-1");

        Assert.Equal(1, oauth.RefreshCallCount);
        Assert.Equal("refreshed-token", api.LastAccessTokenUsed);

        var stored = await accounts.GetAsync("user-1", ConnectedAccountProvider.Whoop);
        Assert.Equal("refreshed-token", stored!.AccessToken);
        Assert.Equal("refresh-new", stored.RefreshToken);
        Assert.Equal(ConnectedAccountStatus.Connected, stored.Status);
        // expires_in is relative (3600 s) and must become an absolute UTC timestamp.
        Assert.InRange(stored.TokenExpiresAt!.Value, DateTime.UtcNow.AddMinutes(58), DateTime.UtcNow.AddMinutes(61));
    }

    [Fact]
    public async Task SyncAsync_RefreshFails_FlipsToNeedsReconnectAndRethrows()
    {
        var (service, accounts, _, _, _) = await BuildAsync(
            Account(DateTime.UtcNow.AddMinutes(2)),
            _ => throw new InvalidOperationException("refresh rejected"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SyncAsync("user-1"));

        var stored = await accounts.GetAsync("user-1", ConnectedAccountProvider.Whoop);
        Assert.Equal(ConnectedAccountStatus.NeedsReconnect, stored!.Status);
    }

    [Fact]
    public async Task SyncAsync_AccountAlreadyNeedsReconnect_ReturnsWithoutCallingWhoop()
    {
        var (service, _, api, oauth, _) = await BuildAsync(
            Account(DateTime.UtcNow.AddHours(2), ConnectedAccountStatus.NeedsReconnect),
            _ => Token("should-not-be-used"));

        await service.SyncAsync("user-1");

        Assert.Equal(0, oauth.RefreshCallCount);
        Assert.Null(api.LastAccessTokenUsed);
    }
}
