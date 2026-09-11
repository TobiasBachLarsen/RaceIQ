namespace RaceIQ.Infrastructure.Strava;

public interface IStravaOAuthService
{
    string BuildAuthorizeUrl(string state);
    Task<StravaTokenResponse> ExchangeCodeAsync(string code);
    Task<StravaTokenResponse> RefreshTokenAsync(string refreshToken);
}

public record StravaTokenResponse(string AccessToken, string RefreshToken, long ExpiresAtUnix);
