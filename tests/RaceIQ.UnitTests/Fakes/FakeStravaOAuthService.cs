using RaceIQ.Infrastructure.Strava;

namespace RaceIQ.UnitTests.Fakes;

// Configurable OAuth fake: RefreshTokenAsync either returns a preset token or throws,
// and counts how many times it was called.
public class FakeStravaOAuthService : IStravaOAuthService
{
    private readonly Func<string, StravaTokenResponse> _refresh;

    public int RefreshCallCount { get; private set; }

    public FakeStravaOAuthService(Func<string, StravaTokenResponse> refresh) => _refresh = refresh;

    public string BuildAuthorizeUrl(string state) => $"https://example.test/authorize?state={state}";

    public Task<StravaTokenResponse> ExchangeCodeAsync(string code) =>
        Task.FromResult(_refresh(code));

    public Task<StravaTokenResponse> RefreshTokenAsync(string refreshToken)
    {
        RefreshCallCount++;
        return Task.FromResult(_refresh(refreshToken));
    }
}
