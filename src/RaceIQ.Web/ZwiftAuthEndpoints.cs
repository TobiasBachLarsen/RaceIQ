using RaceIQ.Application;
using RaceIQ.Domain;
using RaceIQ.Infrastructure.ZwiftPower;

namespace RaceIQ.Web;

// Both handlers bind the posted form as an IFormCollection parameter rather than calling
// Request.ReadFormAsync() themselves. The difference matters: minimal APIs only attach
// antiforgery validation to an endpoint when a parameter binds from the form, so the
// <AntiforgeryToken /> the dashboard renders is only actually checked with this shape.
public static class ZwiftAuthEndpoints
{
    public static void MapZwiftAuthEndpoints(this IEndpointRouteBuilder app)
    {
        // ZwiftRacing: paste an API key plus the rider id and store a ConnectedAccount.
        app.MapPost("/zwiftracing/connect", async (
            HttpContext context, IFormCollection form, IConnectedAccountRepository accountRepository) =>
        {
            var userId = context.GetRequiredUserId("ZwiftRacing connect");

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
        app.MapPost("/zwiftpower/import", async (
            HttpContext context, IFormCollection form, ZwiftPowerImportService importService) =>
        {
            var userId = context.GetRequiredUserId("ZwiftPower import");
            var riderId = form["zwiftRiderId"].ToString().Trim();
            if (riderId.Length == 0)
                return Results.Redirect("/dashboard?zwiftpower=error");

            try
            {
                var outcome = await importService.ImportAsync(userId, riderId, form["resultsJson"].ToString());
                return Results.Redirect($"/dashboard?zwiftpower={outcome.Imported}-{outcome.Matched}");
            }
            catch (ZwiftPowerImportException)
            {
                return Results.Redirect("/dashboard?zwiftpower=error");
            }
        }).RequireAuthorization();
    }
}
