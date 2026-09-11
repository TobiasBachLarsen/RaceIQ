using RaceIQ.Domain;

namespace RaceIQ.Application;

public interface IAnalysisReportRepository
{
    Task<AnalysisReport> AddAsync(AnalysisReport report);
    Task<AnalysisReport?> GetForActivityAsync(int activityId);
}
