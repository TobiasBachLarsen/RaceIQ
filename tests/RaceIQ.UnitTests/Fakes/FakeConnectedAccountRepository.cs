using RaceIQ.Application;
using RaceIQ.Domain;

namespace RaceIQ.UnitTests.Fakes;

public class FakeConnectedAccountRepository : IConnectedAccountRepository
{
    private readonly List<ConnectedAccount> _accounts = new();
    private int _nextId = 1;

    public Task<ConnectedAccount?> GetAsync(string userId, ConnectedAccountProvider provider) =>
        Task.FromResult(_accounts.FirstOrDefault(a => a.UserId == userId && a.Provider == provider));

    public Task<ConnectedAccount> UpsertAsync(ConnectedAccount account)
    {
        var existing = _accounts.FirstOrDefault(a => a.UserId == account.UserId && a.Provider == account.Provider);
        if (existing is null)
        {
            account.Id = _nextId++;
            _accounts.Add(account);
            return Task.FromResult(account);
        }

        existing.AccessToken = account.AccessToken;
        existing.RefreshToken = account.RefreshToken;
        existing.TokenExpiresAt = account.TokenExpiresAt;
        existing.Status = account.Status;
        existing.ExternalAccountId = account.ExternalAccountId;
        return Task.FromResult(existing);
    }
}
