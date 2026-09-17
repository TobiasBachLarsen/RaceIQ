using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RaceIQ.Application.Exceptions;
using RaceIQ.Domain;

namespace RaceIQ.Application;

public class ActivityAnalysisService : IActivityAnalysisService
{
    private readonly IActivityRepository _activityRepository;
    private readonly IAnalysisReportRepository _reportRepository;
    private readonly IClaudeClient _claudeClient;
    private readonly IRaceResultRepository _raceResultRepository;
    private readonly IRecoveryDayRepository _recoveryDayRepository;
    private readonly ILogger<ActivityAnalysisService> _logger;

    public ActivityAnalysisService(
        IActivityRepository activityRepository,
        IAnalysisReportRepository reportRepository,
        IClaudeClient claudeClient,
        IRaceResultRepository raceResultRepository,
        IRecoveryDayRepository recoveryDayRepository,
        ILogger<ActivityAnalysisService> logger)
    {
        _activityRepository = activityRepository;
        _reportRepository = reportRepository;
        _claudeClient = claudeClient;
        _raceResultRepository = raceResultRepository;
        _recoveryDayRepository = recoveryDayRepository;
        _logger = logger;
    }

    public static string BuildPrompt(
        Activity activity, IReadOnlyList<RaceResult> raceResults, RecoveryDay? recovery = null)
    {
        var stream = JsonSerializer.Deserialize<List<StreamPoint>>(activity.StreamDataJson)
            ?? new List<StreamPoint>();

        var sb = new StringBuilder();
        sb.AppendLine("You are a cycling coach analyzing a rider's pacing on a single ride.");
        sb.AppendLine($"Ride: {activity.Name}, {activity.Duration.TotalMinutes:F0} minutes, " +
            $"{activity.DistanceMeters / 1000:F1} km, {activity.ElevationGainMeters:F0} m elevation gain.");

        if (activity.AveragePowerWatts is { } avgPower)
            sb.AppendLine($"Average power: {avgPower:F0} W.");
        if (activity.NormalizedPowerWatts is { } np)
            sb.AppendLine($"Normalized power: {np:F0} W.");
        if (activity.AverageHeartRateBpm is { } avgHr)
            sb.AppendLine($"Average heart rate: {avgHr:F0} bpm.");

        if (stream.Any(p => p.Watts is not null))
        {
            sb.AppendLine();
            sb.AppendLine("Power sampled every 30 seconds through the ride (seconds, watts):");
            for (var i = 0; i < stream.Count; i += 30)
            {
                var point = stream[i];
                sb.AppendLine($"{point.TimeSeconds}s: {point.Watts:F0}W");
            }
        }

        sb.AppendLine();
        if (raceResults.Count > 0)
        {
            var eventName = raceResults[0].EventName;
            var category = raceResults.Select(r => r.Category).FirstOrDefault(c => c is not null);
            var position = raceResults.Select(r => r.Position).FirstOrDefault(p => p is not null);
            var fieldSize = raceResults.Select(r => r.FieldSize).FirstOrDefault(f => f is not null);
            var ratingChange = raceResults.Select(r => r.RatingChange).FirstOrDefault(rc => rc is not null);

            var placement = position is { } pos && fieldSize is { } field
                ? $"finished {pos} of {field}"
                : position is { } posOnly
                    ? $"finished {posOnly}"
                    : "result details are incomplete";
            var categoryText = category is { } cat ? $"category {cat} " : "";
            sb.AppendLine($"This was a {categoryText}race ({eventName}). The rider {placement}. " +
                "In a race, drafting behind other riders lets a rider vary their power a lot on purpose " +
                "(surging to close a gap or follow an attack, then recovering while sheltered in the group), " +
                "so bursty, uneven power is often normal and correct there, not a mistake. Use the placement " +
                "above as context: if the pacing pattern plausibly explains it, say so; otherwise don't force " +
                "a connection that isn't there.");

            if (ratingChange is { } rc)
                sb.AppendLine(
                    $"The rider's race ranking changed by {rc.ToString("+0.0;-0.0", CultureInfo.InvariantCulture)} " +
                    "points from this result - context on how the race went, not a pacing signal on its own.");
        }
        else if (activity.IsRace)
        {
            sb.AppendLine("This was a race, though no result details (category, placement) are available yet. " +
                "In a race, drafting behind other riders lets a rider vary their power a lot on purpose, so " +
                "bursty, uneven power is often normal and correct there, not a mistake.");
        }
        else
        {
            sb.AppendLine("This ride may have been done alone, in a group, or in a pack/peloton race. " +
                "You only have power/heart rate numbers, not positional data, so you cannot tell which " +
                "it was. Keep this in mind: in a group or race, drafting behind other riders lets a " +
                "rider vary their power a lot on purpose (surging to close a gap or follow an attack, " +
                "then recovering while sheltered in the group), so bursty, uneven power is often normal " +
                "and correct there, not a mistake. Do not default to recommending steady, even power as " +
                "the goal. Only call out pacing as a real problem if the pattern looks like it cost the " +
                "rider dearly regardless of context, such as a hard effort early that is never " +
                "recovered from, a big fade in the final part of the ride, or power dropping toward zero " +
                "well before the ride ends. If you are not confident the pattern is a genuine problem, " +
                "say so plainly instead of inventing pacing advice.");
        }
        if (recovery is not null)
        {
            sb.AppendLine();
            sb.AppendLine(
                $"That morning the rider's WHOOP recovery was {recovery.RecoveryScore}% " +
                $"(HRV {recovery.HrvMs.ToString("F0", CultureInfo.InvariantCulture)} ms, " +
                $"resting heart rate {recovery.RestingHeartRate} bpm). " +
                $"A low recovery (under {RecoveryDay.MediumThreshold}%) means the body was already under strain before the ride, so " +
                "a fade or lower-than-usual power may reflect that rather than a pacing mistake; a high " +
                $"recovery ({RecoveryDay.HighThreshold}% or more) means the rider started fresh. Use it as context for what you " +
                "see in the numbers, not as an explanation for everything.");
        }

        sb.AppendLine();
        sb.AppendLine("Explain in plain language what you found. Be specific and reference the " +
            "numbers above. Keep it under 300 words. Write your entire response in Danish.");

        return sb.ToString();
    }

    public async Task<AnalysisReport> AnalyzeAsync(int activityId, string userId)
    {
        var activity = await _activityRepository.GetByIdAsync(activityId, userId)
            ?? throw new ActivityNotFoundException(activityId);

        var raceResults = await _raceResultRepository.GetForActivityAsync(activity.Id);
        var recovery = await _recoveryDayRepository.GetForDateAsync(userId, DateOnly.FromDateTime(activity.StartedAt));
        var prompt = BuildPrompt(activity, raceResults, recovery);

        string reportText;
        try
        {
            reportText = await _claudeClient.GenerateAnalysisAsync(prompt);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Claude analysis call failed for activity {ActivityId}, user {UserId}", activityId, userId);
            throw;
        }

        var report = new AnalysisReport
        {
            ActivityId = activity.Id,
            ReportText = reportText,
            GeneratedAt = DateTime.UtcNow
        };

        return await _reportRepository.AddAsync(report);
    }
}
