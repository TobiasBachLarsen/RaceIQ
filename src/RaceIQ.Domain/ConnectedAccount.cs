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
}
