using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
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

        var prompt = ActivityAnalysisService.BuildPrompt(activity, Array.Empty<RaceResult>());

        Assert.Contains("Morning Zwift race", prompt);
        Assert.Contains("210", prompt);
        Assert.Contains("230", prompt);
        Assert.Contains("155", prompt);
        Assert.Contains("0s: 150W", prompt);
    }

    [Fact]
    public void BuildPrompt_WithMatchedRaceResult_StatesRaceContextDirectly()
    {
        var activity = new Activity
        {
            UserId = "user-1",
            StravaActivityId = "1",
            Name = "Crit Race",
            StreamDataJson = "[]"
        };
        var raceResult = new RaceResult
        {
            UserId = "user-1",
            Provider = ConnectedAccountProvider.ZwiftPower,
            ProviderResultId = "999",
            EventName = "Crit Race",
            EventDate = DateTime.UtcNow,
            Category = "B",
            Position = 4,
            FieldSize = 38
        };

        var prompt = ActivityAnalysisService.BuildPrompt(activity, new[] { raceResult });

        Assert.Contains("category B race", prompt);
        Assert.Contains("finished 4 of 38", prompt);
        Assert.DoesNotContain("may have been done alone", prompt);
    }

    [Fact]
    public void BuildPrompt_WithMultipleMatchedRaceResults_MergesFieldsAcrossThem()
    {
        var activity = new Activity
        {
            UserId = "user-1",
            StravaActivityId = "1",
            Name = "Crit Race",
            StreamDataJson = "[]"
        };
        var zwiftPowerResult = new RaceResult
        {
            UserId = "user-1",
            Provider = ConnectedAccountProvider.ZwiftPower,
            ProviderResultId = "999",
            EventName = "Crit Race",
            EventDate = DateTime.UtcNow,
            Category = "B",
            Position = null,
            FieldSize = null
        };
        var zwiftRacingResult = new RaceResult
        {
            UserId = "user-1",
            Provider = ConnectedAccountProvider.ZwiftRacing,
            ProviderResultId = "998",
            EventName = "Crit Race",
            EventDate = DateTime.UtcNow,
            Category = null,
            Position = 4,
            FieldSize = null
        };

        var prompt = ActivityAnalysisService.BuildPrompt(
            activity, new[] { zwiftPowerResult, zwiftRacingResult });

        Assert.Contains("category B", prompt);
        Assert.Contains("finished 4", prompt);
    }

    [Fact]
    public void BuildPrompt_MatchedRaceResultAndIsRaceBothTrue_PrefersMatchedResultBranch()
    {
        var activity = new Activity
        {
            UserId = "user-1",
            StravaActivityId = "1",
            Name = "Crit Race",
            StreamDataJson = "[]",
            WorkoutType = 11
        };
        var raceResult = new RaceResult
        {
            UserId = "user-1",
            Provider = ConnectedAccountProvider.ZwiftPower,
            ProviderResultId = "999",
            EventName = "Crit Race",
            EventDate = DateTime.UtcNow,
            Category = "B",
            Position = 4,
            FieldSize = 38
        };

        var prompt = ActivityAnalysisService.BuildPrompt(activity, new[] { raceResult });

        Assert.Contains("category B race", prompt);
        Assert.Contains("finished 4 of 38", prompt);
        Assert.DoesNotContain("no result details", prompt);
    }

    [Fact]
    public void BuildPrompt_IsRaceButNoMatchedResult_StatesRaceWithoutDetails()
    {
        var activity = new Activity
        {
            UserId = "user-1",
            StravaActivityId = "1",
            Name = "Unmatched Race",
            StreamDataJson = "[]",
            WorkoutType = 11
        };

        var prompt = ActivityAnalysisService.BuildPrompt(activity, Array.Empty<RaceResult>());

        Assert.Contains("This was a race, though no result details", prompt);
    }

    [Fact]
    public void BuildPrompt_NotARaceAndNoResult_UsesHedgedFraming()
    {
        var activity = new Activity
        {
            UserId = "user-1",
            StravaActivityId = "1",
            Name = "Sunday Spin",
            StreamDataJson = "[]"
        };

        var prompt = ActivityAnalysisService.BuildPrompt(activity, Array.Empty<RaceResult>());

        Assert.Contains("may have been done alone", prompt);
    }

    [Fact]
    public async Task AnalyzeAsync_PersistsReport_GroundedInThePrompt()
    {
        var activityRepository = new FakeActivityRepository();
        var reportRepository = new FakeAnalysisReportRepository();
        var claudeClient = new FakeClaudeClient { ResponseText = "Great even pacing." };
        var service = new ActivityAnalysisService(
            activityRepository, reportRepository, claudeClient, new FakeRaceResultRepository(),
            new FakeRecoveryDayRepository(), NullLogger<ActivityAnalysisService>.Instance);

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
            new FakeActivityRepository(), new FakeAnalysisReportRepository(), new FakeClaudeClient(),
            new FakeRaceResultRepository(), new FakeRecoveryDayRepository(), NullLogger<ActivityAnalysisService>.Instance);

        await Assert.ThrowsAsync<ActivityNotFoundException>(
            () => service.AnalyzeAsync(999, "user-1"));
    }

    [Fact]
    public async Task AnalyzeAsync_WrongUser_ThrowsActivityNotFoundException()
    {
        var activityRepository = new FakeActivityRepository();
        var service = new ActivityAnalysisService(
            activityRepository, new FakeAnalysisReportRepository(), new FakeClaudeClient(),
            new FakeRaceResultRepository(), new FakeRecoveryDayRepository(), NullLogger<ActivityAnalysisService>.Instance);

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
    [Fact]
    public void BuildPrompt_WithRecovery_IncludesScoreHrvAndRestingHeartRate()
    {
        var activity = new Activity
        {
            UserId = "user-1",
            StravaActivityId = "1",
            Name = "Tired Tuesday",
            StreamDataJson = "[]"
        };
        var recovery = new RecoveryDay
        {
            UserId = "user-1",
            Date = new DateOnly(2026, 9, 10),
            RecoveryScore = 28,
            HrvMs = 37.6,
            RestingHeartRate = 56,
            ProviderRecordId = "93845"
        };

        var prompt = ActivityAnalysisService.BuildPrompt(activity, Array.Empty<RaceResult>(), recovery);

        Assert.Contains("WHOOP recovery was 28%", prompt);
        Assert.Contains("HRV 38 ms", prompt);
        Assert.Contains("resting heart rate 56 bpm", prompt);
    }

    [Fact]
    public void BuildPrompt_WithoutRecovery_SaysNothingAboutWhoop()
    {
        var activity = new Activity
        {
            UserId = "user-1",
            StravaActivityId = "1",
            Name = "Sunday Spin",
            StreamDataJson = "[]"
        };

        var prompt = ActivityAnalysisService.BuildPrompt(activity, Array.Empty<RaceResult>());

        Assert.DoesNotContain("WHOOP", prompt);
    }

    [Fact]
    public async Task AnalyzeAsync_UsesTheRecoveryOfTheDayTheRideStarted()
    {
        var activityRepository = new FakeActivityRepository();
        var recoveryRepository = new FakeRecoveryDayRepository();
        var claudeClient = new FakeClaudeClient();
        var service = new ActivityAnalysisService(
            activityRepository, new FakeAnalysisReportRepository(), claudeClient, new FakeRaceResultRepository(),
            recoveryRepository, NullLogger<ActivityAnalysisService>.Instance);

        var activity = await activityRepository.AddAsync(new Activity
        {
            UserId = "user-1",
            StravaActivityId = "1",
            Name = "Evening ride",
            StartedAt = new DateTime(2026, 9, 10, 18, 30, 0, DateTimeKind.Utc),
            StreamDataJson = "[]"
        });
        await recoveryRepository.UpsertAsync(new RecoveryDay
        {
            UserId = "user-1", Date = new DateOnly(2026, 9, 9), RecoveryScore = 90, HrvMs = 80, RestingHeartRate = 45, ProviderRecordId = "a"
        });
        await recoveryRepository.UpsertAsync(new RecoveryDay
        {
            UserId = "user-1", Date = new DateOnly(2026, 9, 10), RecoveryScore = 41, HrvMs = 50, RestingHeartRate = 52, ProviderRecordId = "b"
        });

        await service.AnalyzeAsync(activity.Id, "user-1");

        Assert.Contains("WHOOP recovery was 41%", claudeClient.LastPromptReceived);
        Assert.DoesNotContain("90%", claudeClient.LastPromptReceived);
    }
}
