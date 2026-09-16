using RaceIQ.Application;
using RaceIQ.Domain;

namespace RaceIQ.UnitTests.Fakes;

public class FakeAnalysisReportRepository : IAnalysisReportRepository
{
    private readonly List<AnalysisReport> _reports = new();
    private int _nextId = 1;

    public Task<AnalysisReport> AddAsync(AnalysisReport report)
    {
        report.Id = _nextId++;
        _reports.Add(report);
        return Task.FromResult(report);
    }

    public Task<AnalysisReport?> GetForActivityAsync(int activityId) =>
        Task.FromResult(_reports
            .Where(r => r.ActivityId == activityId)
            .OrderByDescending(r => r.GeneratedAt)
            .ThenByDescending(r => r.Id)
            .FirstOrDefault());
}
