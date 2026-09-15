using System.Security.Claims;

namespace RaceIQ.Web;

public static class HttpContextExtensions
{
    // The authenticated user's id, or an exception naming the action that required it.
    public static string GetRequiredUserId(this HttpContext context, string action) =>
        context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException($"{action} requires an authenticated user.");
}
