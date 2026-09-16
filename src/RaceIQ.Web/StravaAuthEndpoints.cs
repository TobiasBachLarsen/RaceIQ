using Microsoft.AspNetCore.DataProtection;
using RaceIQ.Application;
using RaceIQ.Domain;
using RaceIQ.Infrastructure.Strava;

namespace RaceIQ.Web;

public static class StravaAuthEndpoints
{
    private const string ProtectorPurpose = "RaceIQ.StravaOAuthState";

    public static void MapStravaAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/strava/connect", (HttpContext context, IStravaOAuthService oauth, IDataProtectionProvider dataProtection) =>
        {
            var userId = context.GetRequiredUserId("Strava connect");
            var state = OAuthState.Protect(dataProtection, ProtectorPurpose, userId);

            return Results.Redirect(oauth.BuildAuthorizeUrl(state));
        }).RequireAuthorization();

        app.MapGet("/strava/callback", async (
            string code,
            string state,
            IStravaOAuthService oauth,
            IConnectedAccountRepository accountRepository,
            IDataProtectionProvider dataProtection) =>
        {
            var userId = OAuthState.TryUnprotect(dataProtection, ProtectorPurpose, state);
            if (userId is null)
                return Results.BadRequest("Invalid or expired Strava connection request.");

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
