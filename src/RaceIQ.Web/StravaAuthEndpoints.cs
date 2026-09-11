using System.Security.Claims;
using RaceIQ.Application;
using RaceIQ.Domain;
using RaceIQ.Infrastructure.Strava;

namespace RaceIQ.Web;

public static class StravaAuthEndpoints
{
    public static void MapStravaAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/strava/connect", (HttpContext context, IStravaOAuthService oauth) =>
        {
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new InvalidOperationException("Strava connect requires an authenticated user.");

            return Results.Redirect(oauth.BuildAuthorizeUrl(state: userId));
        }).RequireAuthorization();

        app.MapGet("/strava/callback", async (
            string code,
            string state,
            IStravaOAuthService oauth,
            IConnectedAccountRepository accountRepository) =>
        {
            var tokenResponse = await oauth.ExchangeCodeAsync(code);

            await accountRepository.UpsertAsync(new ConnectedAccount
            {
                UserId = state,
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
