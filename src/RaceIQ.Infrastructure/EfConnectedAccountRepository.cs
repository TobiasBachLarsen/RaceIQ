using Microsoft.EntityFrameworkCore;
using RaceIQ.Application;
using RaceIQ.Domain;

namespace RaceIQ.Infrastructure;

public class EfConnectedAccountRepository : IConnectedAccountRepository
{
    private readonly RaceIQDbContext _context;

    public EfConnectedAccountRepository(RaceIQDbContext context)
    {
        _context = context;
    }

    public async Task<ConnectedAccount?> GetAsync(string userId, ConnectedAccountProvider provider)
    {
        return await _context.ConnectedAccounts
            .FirstOrDefaultAsync(a => a.UserId == userId && a.Provider == provider);
    }

    public async Task<ConnectedAccount> UpsertAsync(ConnectedAccount account)
    {
        var existing = await GetAsync(account.UserId, account.Provider);
        if (existing is null)
        {
            _context.ConnectedAccounts.Add(account);
        }
        else
        {
            existing.AccessToken = account.AccessToken;
            existing.RefreshToken = account.RefreshToken;
            existing.TokenExpiresAt = account.TokenExpiresAt;
            existing.Status = account.Status;
            existing.ExternalAccountId = account.ExternalAccountId;
        }

        await _context.SaveChangesAsync();
        return existing ?? account;
    }
}
