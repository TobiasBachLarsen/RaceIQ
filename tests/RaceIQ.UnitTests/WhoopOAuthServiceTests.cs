using System.Net;
using Microsoft.Extensions.Options;
using RaceIQ.Infrastructure.Whoop;
using Xunit;

namespace RaceIQ.UnitTests;

public class WhoopOAuthServiceTests
{
    private class FakeHandler : HttpMessageHandler
    {
        private readonly string _json;
        public HttpRequestMessage? LastRequest;
        public string? LastBody;

        public FakeHandler(string json) => _json = json;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_json, System.Text.Encoding.UTF8, "application/json")
            };
        }
    }

    private static WhoopOAuthService Build(FakeHandler handler) =>
        new(new HttpClient(handler), Options.Create(new WhoopOAuthOptions
        {
            ClientId = "client-1",
            ClientSecret = "secret-1",
            RedirectUri = "http://localhost:5120/whoop/callback"
        }));

    private const string TokenJson =
        """{"access_token":"access-1","refresh_token":"refresh-1","expires_in":3600,"scope":"offline read:recovery read:profile","token_type":"bearer"}""";

    [Fact]
    public void BuildAuthorizeUrl_IncludesClientRedirectScopesAndState()
    {
        var url = Build(new FakeHandler(TokenJson)).BuildAuthorizeUrl("signed-state");

        Assert.StartsWith(WhoopOAuthService.AuthorizeUrl, url);
        Assert.Contains("client_id=client-1", url);
        Assert.Contains("redirect_uri=http%3A%2F%2Flocalhost%3A5120%2Fwhoop%2Fcallback", url);
        Assert.Contains("response_type=code", url);
        Assert.Contains("scope=offline%20read%3Arecovery%20read%3Aprofile", url);
        Assert.Contains("state=signed-state", url);
    }

    [Fact]
    public async Task ExchangeCodeAsync_PostsAuthorizationCodeGrantAndParsesTokens()
    {
        var handler = new FakeHandler(TokenJson);

        var token = await Build(handler).ExchangeCodeAsync("code-xyz");

        Assert.Equal(WhoopOAuthService.TokenUrl, handler.LastRequest!.RequestUri!.ToString());
        Assert.Contains("grant_type=authorization_code", handler.LastBody);
        Assert.Contains("code=code-xyz", handler.LastBody);
        Assert.Contains("client_secret=secret-1", handler.LastBody);
        Assert.Contains("redirect_uri=http%3A%2F%2Flocalhost%3A5120%2Fwhoop%2Fcallback", handler.LastBody);
        Assert.Equal(new WhoopTokenResponse("access-1", "refresh-1", 3600), token);
    }

    [Fact]
    public async Task RefreshTokenAsync_PostsRefreshGrantWithOfflineScope()
    {
        var handler = new FakeHandler(TokenJson);

        await Build(handler).RefreshTokenAsync("refresh-old");

        Assert.Contains("grant_type=refresh_token", handler.LastBody);
        Assert.Contains("refresh_token=refresh-old", handler.LastBody);
        Assert.Contains("scope=offline", handler.LastBody);
    }

    [Fact]
    public async Task ExchangeCodeAsync_WithoutRefreshToken_Throws()
    {
        // Without the offline scope WHOOP omits refresh_token; storing such a token would
        // silently break the account after an hour, so fail loudly instead.
        var handler = new FakeHandler("""{"access_token":"access-1","expires_in":3600,"token_type":"bearer"}""");

        await Assert.ThrowsAsync<InvalidOperationException>(() => Build(handler).ExchangeCodeAsync("code"));
    }
}
