using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using RaceIQ.Application;
using RaceIQ.Domain;
using RaceIQ.Infrastructure;
using Xunit;

namespace RaceIQ.IntegrationTests;

public class ZwiftPowerConnectTests : IClassFixture<RaceIQApiFactory>
{
    private readonly RaceIQApiFactory _factory;

    public ZwiftPowerConnectTests(RaceIQApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Connect_StoresSessionCookieAndRiderId()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = "zp@example.com", Email = "zp@example.com" };
        await userManager.CreateAsync(user, "P@ssw0rd!");

        // Authenticate as this user via the Identity cookie. The Blazor Identity login
        // page (Components/Account/Pages/Login.razor) renders a static-SSR EditForm that
        // posts back to itself and needs two hidden fields the framework injects into the
        // markup: "_handler" (which named form on the page should handle the post - this
        // page only has one, named "login") and "__RequestVerificationToken" (antiforgery,
        // tied to the antiforgery cookie set on the GET response). WebApplicationFactory's
        // CreateClient() has HandleCookies=true by default, so the antiforgery cookie from
        // the GET and the resulting ".AspNetCore.Identity.Application" auth cookie from the
        // POST are both carried automatically on later requests from the same HttpClient.
        //
        // A naive POST directly to /Account/Login with only Input.Email/Input.Password (no
        // "_handler", no antiforgery token) does NOT authenticate: it silently fails to bind
        // to the login handler and no auth cookie is issued. That failure is easy to miss
        // because hitting an unauthenticated [Authorize]/.RequireAuthorization() endpoint
        // afterwards *also* returns a 302 (the auth challenge redirecting to the login page),
        // so an assertion that only checks for a Redirect status code passes anyway. This was
        // confirmed empirically: without these fields, the /zwiftpower/connect POST still came
        // back 302, but no ConnectedAccount row was ever written - only checking the persisted
        // data below caught it.
        var client = _factory.CreateClient(new() { AllowAutoRedirect = false });

        var loginPage = await client.GetAsync("/Account/Login");
        var loginHtml = await loginPage.Content.ReadAsStringAsync();
        var antiforgeryToken = Regex.Match(loginHtml, "name=\"__RequestVerificationToken\" value=\"([^\"]+)\"").Groups[1].Value;
        var formHandler = Regex.Match(loginHtml, "name=\"_handler\" value=\"([^\"]+)\"").Groups[1].Value;

        var loginResponse = await client.PostAsync("/Account/Login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["_handler"] = formHandler,
                ["__RequestVerificationToken"] = antiforgeryToken,
                ["Input.Email"] = "zp@example.com",
                ["Input.Password"] = "P@ssw0rd!"
            }));
        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);

        var response = await client.PostAsync("/zwiftpower/connect",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["sessionCookie"] = "fake-cookie",
                ["zwiftRiderId"] = "12345"
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var accountRepository = scope.ServiceProvider.GetRequiredService<IConnectedAccountRepository>();
        var account = await accountRepository.GetAsync(user.Id, ConnectedAccountProvider.ZwiftPower);
        Assert.NotNull(account);
        Assert.Equal("fake-cookie", account!.AccessToken);
        Assert.Equal("12345", account.ExternalAccountId);
    }
}
