using RaceIQ.Domain;

namespace RaceIQ.Application;

public interface IConnectedAccountRepository
{
    Task<ConnectedAccount?> GetAsync(string userId, ConnectedAccountProvider provider);
    Task<ConnectedAccount> UpsertAsync(ConnectedAccount account);
}
