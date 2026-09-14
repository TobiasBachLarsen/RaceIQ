using Microsoft.AspNetCore.Mvc;
using RaceIQ.Application;
using RaceIQ.Domain;

namespace RaceIQ.Web;

public static class ZwiftRacingAuthEndpoints
{
    public static void MapZwiftRacingAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/zwiftracing/connect", async (
            HttpContext context,
            [FromForm] string apiKey,
            [FromForm] string zwiftRiderId,
            IConnectedAccountRepository accountRepository) =>
        {
            var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? throw new InvalidOperationException("ZwiftRacing connect requires an authenticated user.");

            await accountRepository.UpsertAsync(new ConnectedAccount
            {
                UserId = userId,
                Provider = ConnectedAccountProvider.ZwiftRacing,
                AccessToken = apiKey,
                ExternalAccountId = zwiftRiderId,
                Status = ConnectedAccountStatus.Connected
            });

            return Results.Redirect("/dashboard");
        }).RequireAuthorization();
    }
}
