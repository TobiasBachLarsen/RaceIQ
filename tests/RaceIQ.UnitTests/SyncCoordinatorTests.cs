using Microsoft.Extensions.Logging.Abstractions;
using RaceIQ.Application;
using RaceIQ.Domain;
using RaceIQ.UnitTests.Fakes;
using Xunit;

namespace RaceIQ.UnitTests;

public class SyncCoordinatorTests
{
    [Fact]
    public async Task SyncAllAsync_FailingProvider_DoesNotBlockOthers()
    {
        var accountRepository = new FakeConnectedAccountRepository();
        await accountRepository.UpsertAsync(new ConnectedAccount
        {
            UserId = "user-1", Provider = ConnectedAccountProvider.Strava, AccessToken = "token"
        });
        await accountRepository.UpsertAsync(new ConnectedAccount
        {
            UserId = "user-1", Provider = ConnectedAccountProvider.ZwiftPower, AccessToken = "cookie"
        });

        var strava = new FakeProviderSyncService(ConnectedAccountProvider.Strava,
            _ => throw new InvalidOperationException("boom"));
        var zwiftPower = new FakeProviderSyncService(ConnectedAccountProvider.ZwiftPower);

        var coordinator = new SyncCoordinator(
            new[] { strava, zwiftPower }, accountRepository, NullLogger<SyncCoordinator>.Instance);

        await coordinator.SyncAllAsync("user-1");

        Assert.Equal(1, strava.SyncCallCount);
        Assert.Equal(1, zwiftPower.SyncCallCount);
    }

    [Fact]
    public async Task SyncAllAsync_StravaFails_OutcomeReportsStravaFailed()
    {
        var accountRepository = new FakeConnectedAccountRepository();
        await accountRepository.UpsertAsync(new ConnectedAccount
        {
            UserId = "user-1", Provider = ConnectedAccountProvider.Strava, AccessToken = "token"
        });

        var strava = new FakeProviderSyncService(ConnectedAccountProvider.Strava,
            _ => throw new InvalidOperationException("boom"));

        var coordinator = new SyncCoordinator(
            new[] { strava }, accountRepository, NullLogger<SyncCoordinator>.Instance);

        var outcome = await coordinator.SyncAllAsync("user-1");

        Assert.True(outcome.StravaFailed);
    }

    [Fact]
    public async Task SyncAllAsync_ZwiftPowerFails_OutcomeDoesNotReportStravaFailed()
    {
        var accountRepository = new FakeConnectedAccountRepository();
        await accountRepository.UpsertAsync(new ConnectedAccount
        {
            UserId = "user-1", Provider = ConnectedAccountProvider.ZwiftPower, AccessToken = "cookie"
        });

        var zwiftPower = new FakeProviderSyncService(ConnectedAccountProvider.ZwiftPower,
            _ => throw new InvalidOperationException("boom"));

        var coordinator = new SyncCoordinator(
            new[] { zwiftPower }, accountRepository, NullLogger<SyncCoordinator>.Instance);

        var outcome = await coordinator.SyncAllAsync("user-1");

        Assert.False(outcome.StravaFailed);
    }

    [Fact]
    public async Task SyncAllAsync_ProviderWithNoConnectedAccount_IsSkipped()
    {
        var accountRepository = new FakeConnectedAccountRepository();
        // No account upserted for ZwiftRacing at all.
        var zwiftRacing = new FakeProviderSyncService(ConnectedAccountProvider.ZwiftRacing);

        var coordinator = new SyncCoordinator(
            new[] { zwiftRacing }, accountRepository, NullLogger<SyncCoordinator>.Instance);

        await coordinator.SyncAllAsync("user-1");

        Assert.Equal(0, zwiftRacing.SyncCallCount);
    }
}
