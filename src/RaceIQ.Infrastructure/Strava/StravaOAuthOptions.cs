namespace RaceIQ.Infrastructure.Strava;

public class StravaOAuthOptions
{
    public required string ClientId { get; set; }
    public required string ClientSecret { get; set; }
    public required string RedirectUri { get; set; }
}
