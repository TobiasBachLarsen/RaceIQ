using RaceIQ.Infrastructure.Whoop;

namespace RaceIQ.UnitTests.Fakes;

public class FakeWhoopOAuthService : IWhoopOAuthService
{
    private readonly Func<string, WhoopTokenResponse> _refresh;

    public int RefreshCallCount { get; private set; }

    public FakeWhoopOAuthService(Func<string, WhoopTokenResponse> refresh) => _refresh = refresh;

    public string BuildAuthorizeUrl(string state) => $"https://example.test/authorize?state={state}";

    public Task<WhoopTokenResponse> ExchangeCodeAsync(string code) => Task.FromResult(_refresh(code));

    public Task<WhoopTokenResponse> RefreshTokenAsync(string refreshToken)
    {
        RefreshCallCount++;
        return Task.FromResult(_refresh(refreshToken));
    }
}
