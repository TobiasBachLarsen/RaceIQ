using RaceIQ.Infrastructure.Whoop;

namespace RaceIQ.UnitTests.Fakes;

public class FakeWhoopApiClient : IWhoopApiClient
{
    public List<WhoopRecovery> Recoveries { get; } = new();
    public string? LastAccessTokenUsed { get; private set; }
    public DateTime? LastStartUtc { get; private set; }

    public Task<WhoopProfile> GetProfileAsync(string accessToken)
    {
        LastAccessTokenUsed = accessToken;
        return Task.FromResult(new WhoopProfile("10129"));
    }

    public Task<IReadOnlyList<WhoopRecovery>> GetRecoveriesAsync(string accessToken, DateTime startUtc)
    {
        LastAccessTokenUsed = accessToken;
        LastStartUtc = startUtc;
        return Task.FromResult<IReadOnlyList<WhoopRecovery>>(Recoveries.ToList());
    }
}
