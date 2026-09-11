using RaceIQ.Domain;
using Xunit;

namespace RaceIQ.UnitTests;

public class ActivityAnalysisServiceTests
{
    [Fact]
    public void NewConnectedAccount_DefaultsToConnectedStatus()
    {
        var account = new ConnectedAccount
        {
            UserId = "user-1",
            Provider = ConnectedAccountProvider.Strava,
            AccessToken = "token"
        };

        Assert.Equal(ConnectedAccountStatus.Connected, account.Status);
    }
}
