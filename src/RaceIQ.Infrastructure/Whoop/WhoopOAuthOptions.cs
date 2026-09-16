namespace RaceIQ.Infrastructure.Whoop;

public class WhoopOAuthOptions
{
    public required string ClientId { get; set; }
    public required string ClientSecret { get; set; }
    public required string RedirectUri { get; set; }
}
