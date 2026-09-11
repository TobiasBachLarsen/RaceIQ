using System.Text.Json;
using RaceIQ.Application;
using RaceIQ.Application.Exceptions;
using RaceIQ.Domain;
using RaceIQ.UnitTests.Fakes;
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

    [Fact]
    public async Task AnalyzeAsync_PersistsReport_GroundedInThePrompt()
    {
        var activityRepository = new FakeActivityRepository();
        var reportRepository = new FakeAnalysisReportRepository();
        var claudeClient = new FakeClaudeClient { ResponseText = "Great even pacing." };
        var service = new ActivityAnalysisService(activityRepository, reportRepository, claudeClient);

        var stream = new List<StreamPoint> { new(0, 200, 140, 90, 10, 0) };
        var activity = await activityRepository.AddAsync(new Activity
        {
            UserId = "user-1",
            StravaActivityId = "1",
            Name = "Test ride",
            StreamDataJson = System.Text.Json.JsonSerializer.Serialize(stream)
        });

        var report = await service.AnalyzeAsync(activity.Id, "user-1");

        Assert.Equal("Great even pacing.", report.ReportText);
        Assert.Equal(activity.Id, report.ActivityId);
        Assert.Contains("Test ride", claudeClient.LastPromptReceived);
    }

    [Fact]
    public async Task AnalyzeAsync_UnknownActivity_ThrowsActivityNotFoundException()
    {
        var service = new ActivityAnalysisService(
            new FakeActivityRepository(), new FakeAnalysisReportRepository(), new FakeClaudeClient());

        await Assert.ThrowsAsync<ActivityNotFoundException>(
            () => service.AnalyzeAsync(999, "user-1"));
    }

    [Fact]
    public async Task AnalyzeAsync_WrongUser_ThrowsActivityNotFoundException()
    {
        var activityRepository = new FakeActivityRepository();
        var service = new ActivityAnalysisService(
            activityRepository, new FakeAnalysisReportRepository(), new FakeClaudeClient());

        var activity = await activityRepository.AddAsync(new Activity
        {
            UserId = "owner",
            StravaActivityId = "1",
            Name = "Private ride",
            StreamDataJson = "[]"
        });

        await Assert.ThrowsAsync<ActivityNotFoundException>(
            () => service.AnalyzeAsync(activity.Id, "someone-else"));
    }
}
