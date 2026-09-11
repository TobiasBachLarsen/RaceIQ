using RaceIQ.Application;
using RaceIQ.Domain;

namespace RaceIQ.UnitTests.Fakes;

public class FakeActivityRepository : IActivityRepository
{
    private readonly List<Activity> _activities = new();
    private int _nextId = 1;

    public Task<Activity> AddAsync(Activity activity)
    {
        activity.Id = _nextId++;
        _activities.Add(activity);
        return Task.FromResult(activity);
    }

    public Task<Activity?> GetByIdAsync(int id, string userId) =>
        Task.FromResult(_activities.FirstOrDefault(a => a.Id == id && a.UserId == userId));

    public Task<IReadOnlyList<Activity>> GetAllForUserAsync(string userId) =>
        Task.FromResult<IReadOnlyList<Activity>>(
            _activities.Where(a => a.UserId == userId).ToList());

    public Task<Activity?> GetByStravaActivityIdAsync(string userId, string stravaActivityId) =>
        Task.FromResult(_activities.FirstOrDefault(
            a => a.UserId == userId && a.StravaActivityId == stravaActivityId));
}
