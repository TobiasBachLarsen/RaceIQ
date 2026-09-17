using System.Security.Claims;

namespace RaceIQ.Web;

public static class HttpContextExtensions
{
    // The authenticated user's id, or an exception naming the action that required it.
    public static string GetRequiredUserId(this HttpContext context, string action) =>
        context.User.GetRequiredUserId(action);

    // Same lookup for Blazor pages, which hold a ClaimsPrincipal from AuthenticationState
    // rather than an HttpContext.
    public static string GetRequiredUserId(this ClaimsPrincipal user, string action) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException($"{action} requires an authenticated user.");
}
