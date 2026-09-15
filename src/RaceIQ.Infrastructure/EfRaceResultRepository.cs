using Microsoft.EntityFrameworkCore;
using RaceIQ.Application;
using RaceIQ.Domain;

namespace RaceIQ.Infrastructure;

public class EfRaceResultRepository : IRaceResultRepository
{
    private readonly RaceIQDbContext _context;

    public EfRaceResultRepository(RaceIQDbContext context)
    {
        _context = context;
    }

    public async Task<RaceResult> UpsertAsync(RaceResult result)
    {
        var existing = await _context.RaceResults.FirstOrDefaultAsync(r =>
            r.UserId == result.UserId &&
            r.Provider == result.Provider &&
            r.ProviderResultId == result.ProviderResultId);

        if (existing is null)
        {
            _context.RaceResults.Add(result);
            await _context.SaveChangesAsync();
            return result;
        }

        existing.EventName = result.EventName;
        existing.EventDate = result.EventDate;
        existing.Category = result.Category;
        existing.Position = result.Position;
        existing.FieldSize = result.FieldSize;
        existing.Duration = result.Duration;
        existing.RatingChange = result.RatingChange;

        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<IReadOnlyList<RaceResult>> GetAllForUserAsync(string userId)
    {
        return await _context.RaceResults
            .Where(r => r.UserId == userId)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<RaceResult>> GetUnmatchedForUserAsync(string userId)
    {
        return await _context.RaceResults
            .Where(r => r.UserId == userId && r.ActivityId == null)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<RaceResult>> GetForActivityAsync(int activityId)
    {
        return await _context.RaceResults
            .Where(r => r.ActivityId == activityId)
            .ToListAsync();
    }

    public async Task<IReadOnlyDictionary<int, IReadOnlyList<RaceResult>>> GetForActivitiesAsync(
        string userId, IReadOnlyList<int> activityIds)
    {
        var results = await _context.RaceResults
            .Where(r => r.UserId == userId && r.ActivityId != null && activityIds.Contains(r.ActivityId.Value))
            .ToListAsync();

        return results
            .GroupBy(r => r.ActivityId!.Value)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<RaceResult>)g.ToList());
    }

    public async Task LinkToActivityAsync(int raceResultId, int activityId)
    {
        var result = await _context.RaceResults.FindAsync(raceResultId);
        if (result is null) return;

        result.ActivityId = activityId;
        await _context.SaveChangesAsync();
    }
}
