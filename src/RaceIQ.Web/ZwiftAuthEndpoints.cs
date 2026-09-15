using RaceIQ.Application;
using RaceIQ.Domain;

namespace RaceIQ.Web;

// Both Zwift providers connect the same way: paste one credential plus a rider id and
// store a ConnectedAccount. Only the route, provider, and credential field name differ.
public static class ZwiftAuthEndpoints
{
    public static void MapZwiftAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapZwiftConnectEndpoint("/zwiftpower/connect", ConnectedAccountProvider.ZwiftPower, "sessionCookie");
        app.MapZwiftConnectEndpoint("/zwiftracing/connect", ConnectedAccountProvider.ZwiftRacing, "apiKey");
    }

    private static void MapZwiftConnectEndpoint(
        this IEndpointRouteBuilder app,
        string route,
        ConnectedAccountProvider provider,
        string credentialField)
    {
        app.MapPost(route, async (HttpContext context, IConnectedAccountRepository accountRepository) =>
        {
            var userId = context.GetRequiredUserId($"{provider} connect");
            var form = await context.Request.ReadFormAsync();

            await accountRepository.UpsertAsync(new ConnectedAccount
            {
                UserId = userId,
                Provider = provider,
                AccessToken = form[credentialField].ToString(),
                ExternalAccountId = form["zwiftRiderId"].ToString(),
                Status = ConnectedAccountStatus.Connected
            });

            return Results.Redirect("/dashboard");
        }).RequireAuthorization();
    }
}
