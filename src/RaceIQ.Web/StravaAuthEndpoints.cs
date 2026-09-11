using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using RaceIQ.Application;
using RaceIQ.Domain;
using RaceIQ.Infrastructure.Strava;

namespace RaceIQ.Web;

public static class StravaAuthEndpoints
{
    private const string ProtectorPurpose = "RaceIQ.StravaOAuthState";
    private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(10);

    public static void MapStravaAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/strava/connect", (HttpContext context, IStravaOAuthService oauth, IDataProtectionProvider dataProtection) =>
        {
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new InvalidOperationException("Strava connect requires an authenticated user.");

            var protector = dataProtection.CreateProtector(ProtectorPurpose).ToTimeLimitedDataProtector();
            var state = protector.Protect(userId, StateLifetime);

            return Results.Redirect(oauth.BuildAuthorizeUrl(state));
        }).RequireAuthorization();

        app.MapGet("/strava/callback", async (
            string code,
            string state,
            IStravaOAuthService oauth,
            IConnectedAccountRepository accountRepository,
            IDataProtectionProvider dataProtection) =>
        {
            string userId;
            try
            {
                var protector = dataProtection.CreateProtector(ProtectorPurpose).ToTimeLimitedDataProtector();
                userId = protector.Unprotect(state);
            }
            catch (CryptographicException)
            {
                return Results.BadRequest("Invalid or expired Strava connection request.");
            }

            var tokenResponse = await oauth.ExchangeCodeAsync(code);

            await accountRepository.UpsertAsync(new ConnectedAccount
            {
                UserId = userId,
                Provider = ConnectedAccountProvider.Strava,
                AccessToken = tokenResponse.AccessToken,
                RefreshToken = tokenResponse.RefreshToken,
                TokenExpiresAt = DateTimeOffset.FromUnixTimeSeconds(tokenResponse.ExpiresAtUnix).UtcDateTime,
                Status = ConnectedAccountStatus.Connected
            });

            return Results.Redirect("/dashboard");
        });
    }
}
