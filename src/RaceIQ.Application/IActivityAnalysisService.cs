using RaceIQ.Domain;

namespace RaceIQ.Application;

public interface IActivityAnalysisService
{
    Task<AnalysisReport> AnalyzeAsync(int activityId, string userId);
}
