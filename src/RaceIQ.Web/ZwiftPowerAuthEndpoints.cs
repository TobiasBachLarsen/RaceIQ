using Microsoft.AspNetCore.Mvc;
using RaceIQ.Application;
using RaceIQ.Domain;

namespace RaceIQ.Web;

public static class ZwiftPowerAuthEndpoints
{
    public static void MapZwiftPowerAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/zwiftpower/connect", async (
            HttpContext context,
            [FromForm] string sessionCookie,
            [FromForm] string zwiftRiderId,
            IConnectedAccountRepository accountRepository) =>
        {
            var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? throw new InvalidOperationException("ZwiftPower connect requires an authenticated user.");

            await accountRepository.UpsertAsync(new ConnectedAccount
            {
                UserId = userId,
                Provider = ConnectedAccountProvider.ZwiftPower,
                AccessToken = sessionCookie,
                ExternalAccountId = zwiftRiderId,
                Status = ConnectedAccountStatus.Connected
            });

            return Results.Redirect("/dashboard");
        }).RequireAuthorization();
    }
}
