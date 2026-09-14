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
    private readonly ILogger<ActivityAnalysisService> _logger;

    public ActivityAnalysisService(
        IActivityRepository activityRepository,
        IAnalysisReportRepository reportRepository,
        IClaudeClient claudeClient,
        IRaceResultRepository raceResultRepository,
        ILogger<ActivityAnalysisService> logger)
    {
        _activityRepository = activityRepository;
        _reportRepository = reportRepository;
        _claudeClient = claudeClient;
        _raceResultRepository = raceResultRepository;
        _logger = logger;
    }

    public static string BuildPrompt(Activity activity, IReadOnlyList<RaceResult> raceResults)
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
        var prompt = BuildPrompt(activity, raceResults);

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
