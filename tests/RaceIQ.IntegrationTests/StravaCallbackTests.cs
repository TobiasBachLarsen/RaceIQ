using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using RaceIQ.Application;
using RaceIQ.Domain;
using RaceIQ.Infrastructure;
using Xunit;

namespace RaceIQ.IntegrationTests;

public class StravaCallbackTests : IClassFixture<RaceIQApiFactory>
{
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

        var client = _factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/strava/callback?code=abc123&state={user.Id}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var accountRepository = scope.ServiceProvider.GetRequiredService<IConnectedAccountRepository>();
        var account = await accountRepository.GetAsync(user.Id, ConnectedAccountProvider.Strava);
        Assert.NotNull(account);
        Assert.Equal("fake-access", account!.AccessToken);
        Assert.Equal(ConnectedAccountStatus.Connected, account.Status);
    }
}
