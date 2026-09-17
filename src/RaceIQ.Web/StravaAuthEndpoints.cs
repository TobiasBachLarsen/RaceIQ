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

        // Strava sends the user back here with ?code=...&state=... on success, or with
        // ?error=access_denied and no code if they declined - in which case we just go
        // home. A failed code exchange (expired or reused code, Strava down) also goes
        // home, with a flag the dashboard turns into a "try again" message.
        app.MapGet("/strava/callback", async (
            string? code,
            string state,
            IStravaOAuthService oauth,
            IConnectedAccountRepository accountRepository,
            IDataProtectionProvider dataProtection,
            ILogger<Program> logger) =>
        {
            var userId = OAuthState.TryUnprotect(dataProtection, ProtectorPurpose, state);
            if (userId is null)
                return Results.BadRequest("Invalid or expired Strava connection request.");

            if (string.IsNullOrEmpty(code))
                return Results.Redirect("/dashboard");

            StravaTokenResponse tokenResponse;
            try
            {
                tokenResponse = await oauth.ExchangeCodeAsync(code);
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
            {
                logger.LogWarning(ex, "Strava code exchange failed for user {UserId}", userId);
                return Results.Redirect("/dashboard?connect=strava-failed");
            }

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
