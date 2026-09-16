using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace RaceIQ.Infrastructure.Whoop;

public class WhoopOAuthService : IWhoopOAuthService
{
    public const string AuthorizeUrl = "https://api.prod.whoop.com/oauth/oauth2/auth";
    public const string TokenUrl = "https://api.prod.whoop.com/oauth/oauth2/token";

    // offline: get a refresh token (access tokens only live an hour).
    // read:recovery: the daily recovery scores. read:profile: the user id we store.
    public const string Scopes = "offline read:recovery read:profile";

    private readonly HttpClient _httpClient;
    private readonly WhoopOAuthOptions _options;

    public WhoopOAuthService(HttpClient httpClient, IOptions<WhoopOAuthOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public string BuildAuthorizeUrl(string state)
    {
        return AuthorizeUrl +
            $"?client_id={Uri.EscapeDataString(_options.ClientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(_options.RedirectUri)}" +
            "&response_type=code" +
            $"&scope={Uri.EscapeDataString(Scopes)}" +
            $"&state={Uri.EscapeDataString(state)}";
    }

    public Task<WhoopTokenResponse> ExchangeCodeAsync(string code) =>
        RequestTokenAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["redirect_uri"] = _options.RedirectUri
        });

    // WHOOP requires the offline scope to be repeated on refresh, otherwise the
    // response carries no new refresh token and the chain breaks after an hour.
    public Task<WhoopTokenResponse> RefreshTokenAsync(string refreshToken) =>
        RequestTokenAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["scope"] = "offline"
        });

    private async Task<WhoopTokenResponse> RequestTokenAsync(Dictionary<string, string> form)
    {
        var response = await _httpClient.PostAsync(TokenUrl, new FormUrlEncodedContent(form));
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<WhoopTokenPayload>()
            ?? throw new InvalidOperationException("WHOOP token response was empty.");

        if (string.IsNullOrEmpty(payload.RefreshToken))
            throw new InvalidOperationException("WHOOP token response had no refresh token; was the offline scope granted?");

        return new WhoopTokenResponse(payload.AccessToken, payload.RefreshToken, payload.ExpiresIn);
    }

    private record WhoopTokenPayload(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
