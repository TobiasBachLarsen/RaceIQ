using System.Net;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using RaceIQ.Application;
using RaceIQ.Domain;
using RaceIQ.Infrastructure;
using Xunit;

namespace RaceIQ.IntegrationTests;

public class WhoopCallbackTests : IClassFixture<RaceIQApiFactory>
{
    private const string ProtectorPurpose = "RaceIQ.WhoopOAuthState";

    private readonly RaceIQApiFactory _factory;

    public WhoopCallbackTests(RaceIQApiFactory factory)
    {
        _factory = factory;
        _factory.WhoopOAuthResponder = _ => FakeHttpMessageHandler.JsonResponse(
            HttpStatusCode.OK,
            """{"access_token":"whoop-access","refresh_token":"whoop-refresh","expires_in":3600,"scope":"offline read:recovery read:profile","token_type":"bearer"}""");
        _factory.WhoopApiResponder = _ => FakeHttpMessageHandler.JsonResponse(
            HttpStatusCode.OK,
            """{"user_id":10129,"email":"rider@example.com","first_name":"A","last_name":"B"}""");
    }

    private static string SignedState(IServiceProvider services, string userId)
    {
        var dataProtection = services.GetRequiredService<IDataProtectionProvider>();
        return dataProtection.CreateProtector(ProtectorPurpose).ToTimeLimitedDataProtector()
            .Protect(userId, TimeSpan.FromMinutes(10));
    }

    [Fact]
    public async Task Callback_ExchangesCodeFetchesProfileAndStoresConnectedAccount()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = "whoop@example.com", Email = "whoop@example.com" };
        await userManager.CreateAsync(user, "P@ssw0rd!");

        var state = SignedState(scope.ServiceProvider, user.Id);
        var client = _factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/whoop/callback?code=abc123&state={Uri.EscapeDataString(state)}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/dashboard", response.Headers.Location!.ToString());

        var accountRepository = scope.ServiceProvider.GetRequiredService<IConnectedAccountRepository>();
        var account = await accountRepository.GetAsync(user.Id, ConnectedAccountProvider.Whoop);
        Assert.NotNull(account);
        Assert.Equal("whoop-access", account!.AccessToken);
        Assert.Equal("whoop-refresh", account.RefreshToken);
        Assert.Equal("10129", account.ExternalAccountId);
        Assert.Equal(ConnectedAccountStatus.Connected, account.Status);
        Assert.InRange(account.TokenExpiresAt!.Value, DateTime.UtcNow.AddMinutes(58), DateTime.UtcNow.AddMinutes(61));
    }

    [Fact]
    public async Task Callback_WithTamperedState_ReturnsBadRequest()
    {
        using var scope = _factory.Services.CreateScope();
        var accountRepository = scope.ServiceProvider.GetRequiredService<IConnectedAccountRepository>();
        const string victimUserId = "some-victim-user-id";

        var client = _factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/whoop/callback?code=abc123&state={victimUserId}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(await accountRepository.GetAsync(victimUserId, ConnectedAccountProvider.Whoop));
    }

    [Fact]
    public async Task Callback_UserDeclined_RedirectsHomeWithoutStoringAnything()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = "declined@example.com", Email = "declined@example.com" };
        await userManager.CreateAsync(user, "P@ssw0rd!");

        var state = SignedState(scope.ServiceProvider, user.Id);
        var client = _factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/whoop/callback?error=access_denied&state={Uri.EscapeDataString(state)}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var accountRepository = scope.ServiceProvider.GetRequiredService<IConnectedAccountRepository>();
        Assert.Null(await accountRepository.GetAsync(user.Id, ConnectedAccountProvider.Whoop));
    }
}
