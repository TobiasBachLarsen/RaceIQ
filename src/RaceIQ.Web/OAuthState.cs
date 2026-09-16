using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace RaceIQ.Web;

// The OAuth `state` round-tripped through a provider's authorize page carries the user id
// that started the flow, signed and time-limited so the callback cannot be pointed at
// someone else's account by pasting a raw id. Shared by every OAuth provider; each gets
// its own protector purpose so a Strava state can't be replayed against WHOOP.
public static class OAuthState
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    public static string Protect(IDataProtectionProvider dataProtection, string purpose, string userId) =>
        dataProtection.CreateProtector(purpose).ToTimeLimitedDataProtector().Protect(userId, Lifetime);

    public static string? TryUnprotect(IDataProtectionProvider dataProtection, string purpose, string state)
    {
        try
        {
            return dataProtection.CreateProtector(purpose).ToTimeLimitedDataProtector().Unprotect(state);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}
