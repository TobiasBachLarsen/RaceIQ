using Microsoft.AspNetCore.DataProtection;
using RaceIQ.Application;
using RaceIQ.Domain;
using RaceIQ.Infrastructure.Whoop;

namespace RaceIQ.Web;

public static class WhoopAuthEndpoints
{
    private const string ProtectorPurpose = "RaceIQ.WhoopOAuthState";

    public static void MapWhoopAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/whoop/connect", (HttpContext context, IWhoopOAuthService oauth, IDataProtectionProvider dataProtection) =>
        {
            var userId = context.GetRequiredUserId("WHOOP connect");
            var state = OAuthState.Protect(dataProtection, ProtectorPurpose, userId);

            return Results.Redirect(oauth.BuildAuthorizeUrl(state));
        }).RequireAuthorization();

        // WHOOP sends the user back here with ?code=...&state=... on success, or with
        // ?error=... if they declined - in which case there is no code and we just go home.
        app.MapGet("/whoop/callback", async (
            string? code,
            string state,
            IWhoopOAuthService oauth,
            IWhoopApiClient apiClient,
            IConnectedAccountRepository accountRepository,
            IDataProtectionProvider dataProtection) =>
        {
            var userId = OAuthState.TryUnprotect(dataProtection, ProtectorPurpose, state);
            if (userId is null)
                return Results.BadRequest("Invalid or expired WHOOP connection request.");

            if (string.IsNullOrEmpty(code))
                return Results.Redirect("/dashboard");

            var tokenResponse = await oauth.ExchangeCodeAsync(code);
            var profile = await apiClient.GetProfileAsync(tokenResponse.AccessToken);

            await accountRepository.UpsertAsync(new ConnectedAccount
            {
                UserId = userId,
                Provider = ConnectedAccountProvider.Whoop,
                AccessToken = tokenResponse.AccessToken,
                RefreshToken = tokenResponse.RefreshToken,
                TokenExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresInSeconds),
                ExternalAccountId = profile.UserId,
                Status = ConnectedAccountStatus.Connected
            });

            return Results.Redirect("/dashboard");
        });
    }
}
