using RaceIQ.Domain;

namespace RaceIQ.Application;

public interface IActivityRepository
{
    Task<Activity> AddAsync(Activity activity);
    Task<Activity?> GetByIdAsync(int id, string userId);
    Task<IReadOnlyList<Activity>> GetAllForUserAsync(string userId);
    Task<Activity?> GetByStravaActivityIdAsync(string userId, string stravaActivityId);
}
