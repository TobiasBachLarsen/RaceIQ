using Microsoft.EntityFrameworkCore;
using RaceIQ.Application;
using RaceIQ.Domain;

namespace RaceIQ.Infrastructure;

public class EfRecoveryDayRepository : IRecoveryDayRepository
{
    private readonly RaceIQDbContext _context;

    public EfRecoveryDayRepository(RaceIQDbContext context)
    {
        _context = context;
    }

    public async Task<RecoveryDay> UpsertAsync(RecoveryDay day)
    {
        var existing = await _context.RecoveryDays.FirstOrDefaultAsync(d =>
            d.UserId == day.UserId && d.Date == day.Date);

        if (existing is null)
        {
            _context.RecoveryDays.Add(day);
            await _context.SaveChangesAsync();
            return day;
        }

        existing.RecoveryScore = day.RecoveryScore;
        existing.HrvMs = day.HrvMs;
        existing.RestingHeartRate = day.RestingHeartRate;
        existing.ProviderRecordId = day.ProviderRecordId;

        await _context.SaveChangesAsync();
        return existing;
    }

    public Task<RecoveryDay?> GetForDateAsync(string userId, DateOnly date) =>
        _context.RecoveryDays.FirstOrDefaultAsync(d => d.UserId == userId && d.Date == date);

    public Task<RecoveryDay?> GetLatestAsync(string userId) =>
        _context.RecoveryDays
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.Date)
            .FirstOrDefaultAsync();
}
