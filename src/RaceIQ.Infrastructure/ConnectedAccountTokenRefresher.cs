using Microsoft.Extensions.Logging;
using RaceIQ.Application;
using RaceIQ.Domain;

namespace RaceIQ.Infrastructure;

public record RefreshedToken(string AccessToken, string RefreshToken, DateTime ExpiresAtUtc);

// The one piece of OAuth housekeeping every token-based provider (Strava, WHOOP) needs
// before it can call its API: use the stored access token while it is still good, refresh
// it through the provider when it is about to expire, and on a failed refresh flip the
// account to NeedsReconnect so the dashboard shows a reconnect card. The provider supplies
// only the refresh call itself.
public class ConnectedAccountTokenRefresher
{
    // Refresh a little early so a token can't expire between this check and the API call.
    private static readonly TimeSpan ExpiryMargin = TimeSpan.FromMinutes(5);

    private readonly IConnectedAccountRepository _accountRepository;
    private readonly ILogger<ConnectedAccountTokenRefresher> _logger;

    public ConnectedAccountTokenRefresher(
        IConnectedAccountRepository accountRepository,
        ILogger<ConnectedAccountTokenRefresher> logger)
    {
        _accountRepository = accountRepository;
        _logger = logger;
    }

    public async Task<string> EnsureFreshAsync(ConnectedAccount account, Func<string, Task<RefreshedToken>> refresh)
    {
        if (account.TokenExpiresAt is { } expiresAt && expiresAt > DateTime.UtcNow + ExpiryMargin)
            return account.AccessToken;

        RefreshedToken refreshed;
        try
        {
            refreshed = await refresh(account.RefreshToken!);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "{Provider} token refresh failed for user {UserId}; flipping account to NeedsReconnect",
                account.Provider, account.UserId);
            account.Status = ConnectedAccountStatus.NeedsReconnect;
            await _accountRepository.UpsertAsync(account);
            throw;
        }

        account.AccessToken = refreshed.AccessToken;
        account.RefreshToken = refreshed.RefreshToken;
        account.TokenExpiresAt = refreshed.ExpiresAtUtc;
        await _accountRepository.UpsertAsync(account);

        return account.AccessToken;
    }
}
