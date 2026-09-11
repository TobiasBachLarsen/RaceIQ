using Microsoft.EntityFrameworkCore;
using RaceIQ.Application;
using RaceIQ.Domain;

namespace RaceIQ.Infrastructure;

public class EfActivityRepository : IActivityRepository
{
    private readonly RaceIQDbContext _context;

    public EfActivityRepository(RaceIQDbContext context)
    {
        _context = context;
    }

    public async Task<Activity> AddAsync(Activity activity)
    {
        _context.Activities.Add(activity);
        await _context.SaveChangesAsync();
        return activity;
    }

    public async Task<Activity?> GetByIdAsync(int id, string userId)
    {
        return await _context.Activities
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);
    }

    public async Task<IReadOnlyList<Activity>> GetAllForUserAsync(string userId)
    {
        return await _context.Activities
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.StartedAt)
            .ToListAsync();
    }

    public async Task<Activity?> GetByStravaActivityIdAsync(string userId, string stravaActivityId)
    {
        return await _context.Activities
            .FirstOrDefaultAsync(a => a.UserId == userId && a.StravaActivityId == stravaActivityId);
    }
}
