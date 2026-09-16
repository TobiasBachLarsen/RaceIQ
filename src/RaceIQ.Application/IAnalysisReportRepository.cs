using RaceIQ.Domain;

namespace RaceIQ.Application;

public interface IAnalysisReportRepository
{
    Task<AnalysisReport> AddAsync(AnalysisReport report);
    // The newest report for the activity. Re-analysing adds a new report rather than
    // overwriting, so earlier write-ups are kept while the page always shows the latest.
    Task<AnalysisReport?> GetForActivityAsync(int activityId);
}
