using System.Text.Json;
using RaceIQ.Application;
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

    [Fact]
    public void BuildPrompt_IncludesKeyRideStatsAndPowerSamples()
    {
        var stream = new List<StreamPoint>
        {
            new(0, 150, 120, 85, 10, 0),
            new(30, 280, 140, 92, 12, 200),
            new(60, 190, 130, 88, 15, 410)
        };

        var activity = new Activity
        {
            UserId = "user-1",
            StravaActivityId = "12345",
            Name = "Morning Zwift race",
            Duration = TimeSpan.FromMinutes(45),
            DistanceMeters = 30000,
            ElevationGainMeters = 250,
            AveragePowerWatts = 210,
            NormalizedPowerWatts = 230,
            AverageHeartRateBpm = 155,
            StreamDataJson = JsonSerializer.Serialize(stream)
        };

        var prompt = ActivityAnalysisService.BuildPrompt(activity);

        Assert.Contains("Morning Zwift race", prompt);
        Assert.Contains("210", prompt);
        Assert.Contains("230", prompt);
        Assert.Contains("155", prompt);
        Assert.Contains("0s: 150W", prompt);
    }
}
