using System.Net;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using RaceIQ.Application;
using RaceIQ.Domain;
using RaceIQ.Infrastructure;
using Xunit;

namespace RaceIQ.IntegrationTests;

public class StravaCallbackTests : IClassFixture<RaceIQApiFactory>
{
    private const string ProtectorPurpose = "RaceIQ.StravaOAuthState";
    private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(10);

    private readonly RaceIQApiFactory _factory;

    public StravaCallbackTests(RaceIQApiFactory factory)
    {
        _factory = factory;
        _factory.StravaOAuthResponder = _ => FakeHttpMessageHandler.JsonResponse(
            HttpStatusCode.OK,
            """{"access_token":"fake-access","refresh_token":"fake-refresh","expires_at":9999999999}""");
    }

    [Fact]
    public async Task Callback_ExchangesCodeAndStoresConnectedAccount()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = "rider@example.com", Email = "rider@example.com" };
        await userManager.CreateAsync(user, "P@ssw0rd!");

        // Real callers arrive here via /strava/connect's signed, time-limited state
        // token, not the raw user id -- reproduce that here rather than trusting the
        // id verbatim, the way /strava/connect itself does.
        var dataProtection = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>();
        var protector = dataProtection.CreateProtector(ProtectorPurpose).ToTimeLimitedDataProtector();
        var state = protector.Protect(user.Id, StateLifetime);

        var client = _factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/strava/callback?code=abc123&state={Uri.EscapeDataString(state)}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var accountRepository = scope.ServiceProvider.GetRequiredService<IConnectedAccountRepository>();
        var account = await accountRepository.GetAsync(user.Id, ConnectedAccountProvider.Strava);
        Assert.NotNull(account);
        Assert.Equal("fake-access", account!.AccessToken);
        Assert.Equal(ConnectedAccountStatus.Connected, account.Status);
    }

    [Fact]
    public async Task Callback_WithTamperedState_ReturnsBadRequest()
    {
        using var scope = _factory.Services.CreateScope();
        var accountRepository = scope.ServiceProvider.GetRequiredService<IConnectedAccountRepository>();

        // Attack pattern this vulnerability enabled: an attacker completes their OWN
        // Strava OAuth flow (a legitimate `code`), but supplies a victim's raw user id
        // as `state` instead of a signed token, hoping the callback trusts it verbatim
        // and attaches the attacker's Strava tokens to the victim's account.
        const string victimUserId = "some-victim-user-id";

        var client = _factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/strava/callback?code=abc123&state={victimUserId}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var account = await accountRepository.GetAsync(victimUserId, ConnectedAccountProvider.Strava);
        Assert.Null(account);
    }
}
