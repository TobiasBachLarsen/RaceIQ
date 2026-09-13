using RaceIQ.Application;
using RaceIQ.Domain;

namespace RaceIQ.UnitTests.Fakes;

public class FakeRaceResultRepository : IRaceResultRepository
{
    private readonly List<RaceResult> _results = new();
    private int _nextId = 1;

    public Task<RaceResult> UpsertAsync(RaceResult result)
    {
        var existing = _results.FirstOrDefault(r =>
            r.UserId == result.UserId &&
            r.Provider == result.Provider &&
            r.ProviderResultId == result.ProviderResultId);

        if (existing is null)
        {
            result.Id = _nextId++;
            _results.Add(result);
            return Task.FromResult(result);
        }

        existing.EventName = result.EventName;
        existing.EventDate = result.EventDate;
        existing.Category = result.Category;
        existing.Position = result.Position;
        existing.FieldSize = result.FieldSize;
        existing.Duration = result.Duration;
        existing.RatingChange = result.RatingChange;
        return Task.FromResult(existing);
    }

    public Task<IReadOnlyList<RaceResult>> GetUnmatchedForUserAsync(string userId) =>
        Task.FromResult<IReadOnlyList<RaceResult>>(
            _results.Where(r => r.UserId == userId && r.ActivityId == null).ToList());

    public Task<IReadOnlyList<RaceResult>> GetForActivityAsync(int activityId) =>
        Task.FromResult<IReadOnlyList<RaceResult>>(
            _results.Where(r => r.ActivityId == activityId).ToList());

    public Task LinkToActivityAsync(int raceResultId, int activityId)
    {
        var result = _results.FirstOrDefault(r => r.Id == raceResultId);
        if (result is not null) result.ActivityId = activityId;
        return Task.CompletedTask;
    }
}
