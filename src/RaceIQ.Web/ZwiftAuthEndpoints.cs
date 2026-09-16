using RaceIQ.Application;
using RaceIQ.Domain;
using RaceIQ.Infrastructure.ZwiftPower;

namespace RaceIQ.Web;

public static class ZwiftAuthEndpoints
{
    public static void MapZwiftAuthEndpoints(this IEndpointRouteBuilder app)
    {
        // ZwiftRacing: paste an API key plus the rider id and store a ConnectedAccount.
        app.MapPost("/zwiftracing/connect", async (HttpContext context, IConnectedAccountRepository accountRepository) =>
        {
            var userId = context.GetRequiredUserId("ZwiftRacing connect");
            var form = await context.Request.ReadFormAsync();

            await accountRepository.UpsertAsync(new ConnectedAccount
            {
                UserId = userId,
                Provider = ConnectedAccountProvider.ZwiftRacing,
                AccessToken = form["apiKey"].ToString(),
                ExternalAccountId = form["zwiftRiderId"].ToString(),
                Status = ConnectedAccountStatus.Connected
            });

            return Results.Redirect("/dashboard");
        }).RequireAuthorization();

        // ZwiftPower: the rider pastes their results JSON (see ZwiftPowerResultParser for
        // why the app cannot fetch it). The outcome travels back to the dashboard in the
        // query string, since a plain form post can't update the Blazor circuit directly.
        app.MapPost("/zwiftpower/import", async (HttpContext context, ZwiftPowerImportService importService) =>
        {
            var userId = context.GetRequiredUserId("ZwiftPower import");
            var form = await context.Request.ReadFormAsync();

            try
            {
                var outcome = await importService.ImportAsync(
                    userId, form["zwiftRiderId"].ToString(), form["resultsJson"].ToString());
                return Results.Redirect($"/dashboard?zwiftpower={outcome.Imported}-{outcome.Matched}");
            }
            catch (ZwiftPowerImportException)
            {
                return Results.Redirect("/dashboard?zwiftpower=error");
            }
        }).RequireAuthorization();
    }
}
