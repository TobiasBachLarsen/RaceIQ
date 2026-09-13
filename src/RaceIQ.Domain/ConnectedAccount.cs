namespace RaceIQ.Domain;

public class ConnectedAccount
{
    public int Id { get; set; }
    public required string UserId { get; set; }
    public ConnectedAccountProvider Provider { get; set; }
    public required string AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? TokenExpiresAt { get; set; }
    public ConnectedAccountStatus Status { get; set; } = ConnectedAccountStatus.Connected;

    // The user's own Zwift rider id, required by ZwiftPower and ZwiftRacing to look
    // up their results. Always null for Strava.
    public string? ExternalAccountId { get; set; }
}
