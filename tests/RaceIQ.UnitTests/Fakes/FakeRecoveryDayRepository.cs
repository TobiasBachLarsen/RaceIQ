using RaceIQ.Application;
using RaceIQ.Domain;

namespace RaceIQ.UnitTests.Fakes;

public class FakeRecoveryDayRepository : IRecoveryDayRepository
{
    private readonly List<RecoveryDay> _days = new();
    private int _nextId = 1;

    public IReadOnlyList<RecoveryDay> All => _days;

    public Task<RecoveryDay> UpsertAsync(RecoveryDay day)
    {
        var existing = _days.FirstOrDefault(d => d.UserId == day.UserId && d.Date == day.Date);
        if (existing is null)
        {
            day.Id = _nextId++;
            _days.Add(day);
            return Task.FromResult(day);
        }

        existing.RecoveryScore = day.RecoveryScore;
        existing.HrvMs = day.HrvMs;
        existing.RestingHeartRate = day.RestingHeartRate;
        existing.ProviderRecordId = day.ProviderRecordId;
        return Task.FromResult(existing);
    }

    public Task<RecoveryDay?> GetForDateAsync(string userId, DateOnly date) =>
        Task.FromResult(_days.FirstOrDefault(d => d.UserId == userId && d.Date == date));

    public Task<RecoveryDay?> GetLatestAsync(string userId) =>
        Task.FromResult(_days.Where(d => d.UserId == userId).OrderByDescending(d => d.Date).FirstOrDefault());
}
