using RaceIQ.Domain;
using RaceIQ.Infrastructure.Strava;

namespace RaceIQ.UnitTests.Fakes;

// Returns no activities so StravaSyncService tests can focus on the token-refresh flow.
// Records the access token it was called with, to assert whether a fresh or refreshed
// token was used.
public class FakeStravaApiClient : IStravaApiClient
{
    public string? LastAccessTokenUsed { get; private set; }

    public Task<IReadOnlyList<StravaActivitySummary>> ListRecentActivitiesAsync(string accessToken)
    {
        LastAccessTokenUsed = accessToken;
        return Task.FromResult<IReadOnlyList<StravaActivitySummary>>(Array.Empty<StravaActivitySummary>());
    }

    public Task<IReadOnlyList<StreamPoint>> GetActivityStreamAsync(string accessToken, string stravaActivityId) =>
        Task.FromResult<IReadOnlyList<StreamPoint>>(Array.Empty<StreamPoint>());
}
