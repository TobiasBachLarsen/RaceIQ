namespace RaceIQ.Infrastructure.Whoop;

public interface IWhoopOAuthService
{
    string BuildAuthorizeUrl(string state);
    Task<WhoopTokenResponse> ExchangeCodeAsync(string code);
    Task<WhoopTokenResponse> RefreshTokenAsync(string refreshToken);
}

// WHOOP reports token lifetime as a relative expires_in (seconds), unlike Strava's
// absolute expires_at, so the caller turns it into a timestamp when storing it.
public record WhoopTokenResponse(string AccessToken, string RefreshToken, int ExpiresInSeconds);
