using RaceIQ.Domain;

namespace RaceIQ.Application;

public interface IRecoveryDayRepository
{
    // Inserts a new RecoveryDay, or updates the existing one for the same (UserId, Date).
    // A user has at most one recovery reading per day.
    Task<RecoveryDay> UpsertAsync(RecoveryDay day);

    Task<RecoveryDay?> GetForDateAsync(string userId, DateOnly date);

    // The user's most recent reading by Date - what the dashboard shows as "today".
    Task<RecoveryDay?> GetLatestAsync(string userId);
}
