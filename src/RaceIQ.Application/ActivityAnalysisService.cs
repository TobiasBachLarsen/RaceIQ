using System.Text;
using System.Text.Json;
using RaceIQ.Application.Exceptions;
using RaceIQ.Domain;

namespace RaceIQ.Application;

public class ActivityAnalysisService : IActivityAnalysisService
{
    private readonly IActivityRepository _activityRepository;
    private readonly IAnalysisReportRepository _reportRepository;
    private readonly IClaudeClient _claudeClient;

    public ActivityAnalysisService(
        IActivityRepository activityRepository,
        IAnalysisReportRepository reportRepository,
        IClaudeClient claudeClient)
    {
        _activityRepository = activityRepository;
        _reportRepository = reportRepository;
        _claudeClient = claudeClient;
    }

    public static string BuildPrompt(Activity activity)
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

        sb.AppendLine();
        sb.AppendLine("Power sampled every 30 seconds through the ride (seconds, watts):");
        for (var i = 0; i < stream.Count; i += 30)
        {
            var point = stream[i];
            sb.AppendLine($"{point.TimeSeconds}s: {point.Watts:F0}W");
        }

        sb.AppendLine();
        sb.AppendLine("Explain in plain language where the rider's pacing was uneven, where " +
            "they went out too hard or faded, and what they should do differently next time on " +
            "a similar ride. Be specific and reference the numbers above. Keep it under 300 words.");

        return sb.ToString();
    }

    public async Task<AnalysisReport> AnalyzeAsync(int activityId, string userId)
    {
        var activity = await _activityRepository.GetByIdAsync(activityId, userId)
            ?? throw new ActivityNotFoundException(activityId);

        var prompt = BuildPrompt(activity);
        var reportText = await _claudeClient.GenerateAnalysisAsync(prompt);

        var report = new AnalysisReport
        {
            ActivityId = activity.Id,
            ReportText = reportText,
            GeneratedAt = DateTime.UtcNow
        };

        return await _reportRepository.AddAsync(report);
    }
}
