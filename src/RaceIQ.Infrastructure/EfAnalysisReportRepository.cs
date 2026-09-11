using Microsoft.EntityFrameworkCore;
using RaceIQ.Application;
using RaceIQ.Domain;

namespace RaceIQ.Infrastructure;

public class EfAnalysisReportRepository : IAnalysisReportRepository
{
    private readonly RaceIQDbContext _context;

    public EfAnalysisReportRepository(RaceIQDbContext context)
    {
        _context = context;
    }

    public async Task<AnalysisReport> AddAsync(AnalysisReport report)
    {
        _context.AnalysisReports.Add(report);
        await _context.SaveChangesAsync();
        return report;
    }

    public async Task<AnalysisReport?> GetForActivityAsync(int activityId)
    {
        return await _context.AnalysisReports
            .Where(r => r.ActivityId == activityId)
            .OrderByDescending(r => r.GeneratedAt)
            .FirstOrDefaultAsync();
    }
}
