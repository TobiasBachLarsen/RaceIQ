using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace RaceIQ.Infrastructure.Strava;

public class StravaOAuthService : IStravaOAuthService
{
    private readonly HttpClient _httpClient;
    private readonly StravaOAuthOptions _options;
    private readonly StravaRequestThrottle _throttle;

    public StravaOAuthService(HttpClient httpClient, IOptions<StravaOAuthOptions> options, StravaRequestThrottle throttle)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _throttle = throttle;
    }

    public string BuildAuthorizeUrl(string state)
    {
        return "https://www.strava.com/oauth/authorize" +
            $"?client_id={Uri.EscapeDataString(_options.ClientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(_options.RedirectUri)}" +
            "&response_type=code" +
            "&scope=read,activity:read_all" +
            $"&state={Uri.EscapeDataString(state)}";
    }

    public Task<StravaTokenResponse> ExchangeCodeAsync(string code) =>
        RequestTokenAsync(new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["code"] = code,
            ["grant_type"] = "authorization_code"
        });

    public Task<StravaTokenResponse> RefreshTokenAsync(string refreshToken) =>
        RequestTokenAsync(new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["refresh_token"] = refreshToken,
            ["grant_type"] = "refresh_token"
        });

    private async Task<StravaTokenResponse> RequestTokenAsync(Dictionary<string, string> form)
    {
        await _throttle.WaitForSlotAsync();

        var response = await _httpClient.PostAsync(
            "https://www.strava.com/oauth/token",
            new FormUrlEncodedContent(form));

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<StravaTokenPayload>()
            ?? throw new InvalidOperationException("Strava token response was empty.");

        return new StravaTokenResponse(payload.AccessToken, payload.RefreshToken, payload.ExpiresAt);
    }

    private record StravaTokenPayload(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("refresh_token")] string RefreshToken,
        [property: JsonPropertyName("expires_at")] long ExpiresAt);
}
