using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using RaceIQ.Application;
using RaceIQ.Domain;
using RaceIQ.Infrastructure;
using Xunit;

namespace RaceIQ.IntegrationTests;

public class ZwiftPowerImportTests : IClassFixture<RaceIQApiFactory>
{
    private readonly RaceIQApiFactory _factory;

    public ZwiftPowerImportTests(RaceIQApiFactory factory)
    {
        _factory = factory;
    }

    // Logs in through the real Identity login form (it needs the "_handler" and
    // antiforgery fields the page renders, or the post silently doesn't authenticate) and
    // returns a client carrying the auth cookie plus the antiforgery token scraped from
    // the dashboard's ZwiftPower import form.
    private async Task<(HttpClient client, string formToken)> LoginAsync(string email)
    {
        var client = _factory.CreateClient(new() { AllowAutoRedirect = false });

        var loginHtml = await (await client.GetAsync("/Account/Login")).Content.ReadAsStringAsync();
        var loginToken = Regex.Match(loginHtml, "name=\"__RequestVerificationToken\" value=\"([^\"]+)\"").Groups[1].Value;
        var handler = Regex.Match(loginHtml, "name=\"_handler\" value=\"([^\"]+)\"").Groups[1].Value;

        var login = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = handler,
            ["__RequestVerificationToken"] = loginToken,
            ["Input.Email"] = email,
            ["Input.Password"] = "P@ssw0rd!"
        }));
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var dashboardHtml = await (await client.GetAsync("/dashboard")).Content.ReadAsStringAsync();
        var formToken = Regex.Match(
            dashboardHtml,
            "action=\"/zwiftpower/import\"[^>]*><input type=\"hidden\" name=\"__RequestVerificationToken\" value=\"([^\"]+)\"").Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(formToken), "Could not find the ZwiftPower import form's antiforgery token in the dashboard HTML.");

        return (client, formToken);
    }

    [Fact]
    public async Task Import_StoresRacesMatchesThemAndRemembersRiderId()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = "zpimport@example.com", Email = "zpimport@example.com" };
        await userManager.CreateAsync(user, "P@ssw0rd!");

        var activityRepository = scope.ServiceProvider.GetRequiredService<IActivityRepository>();
        var activity = await activityRepository.AddAsync(new Activity
        {
            UserId = user.Id,
            StravaActivityId = "555",
            Name = "Zwift crit race",
            StartedAt = DateTimeOffset.FromUnixTimeSeconds(1756742400).UtcDateTime.AddMinutes(2),
            Duration = TimeSpan.FromSeconds(2700),
            StreamDataJson = "[]"
        });

        var (client, formToken) = await LoginAsync("zpimport@example.com");

        const string json =
            """{"data":[{"zid":"999","event_title":"Crit Race","event_date":1756742400,"category":"B","pos":4,"time_gun":2700,"f_t":"TYPE_RACE TYPE_RACE ","skill_gain":"28.29"},{"zid":"1000","event_title":"Workout","event_date":1756828800,"f_t":"TYPE_WORKOUT"}]}""";

        var response = await client.PostAsync("/zwiftpower/import", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = formToken,
            ["zwiftRiderId"] = "12345",
            ["resultsJson"] = json
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/dashboard?zwiftpower=1-1", response.Headers.Location!.ToString());

        var raceResultRepository = scope.ServiceProvider.GetRequiredService<IRaceResultRepository>();
        var results = await raceResultRepository.GetForActivityAsync(activity.Id);
        Assert.Single(results);
        Assert.Equal("B", results[0].Category);
        Assert.Equal(4, results[0].Position);
        Assert.Equal(28.29, results[0].RatingChange);

        var accountRepository = scope.ServiceProvider.GetRequiredService<IConnectedAccountRepository>();
        var account = await accountRepository.GetAsync(user.Id, ConnectedAccountProvider.ZwiftPower);
        Assert.Equal("12345", account!.ExternalAccountId);

        // Importing the same document again must update, not duplicate.
        await client.PostAsync("/zwiftpower/import", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = formToken,
            ["zwiftRiderId"] = "12345",
            ["resultsJson"] = json
        }));
        Assert.Single(await raceResultRepository.GetForActivityAsync(activity.Id));
    }

    [Fact]
    public async Task Import_WithNonJsonText_RedirectsWithErrorAndStoresNothing()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = "zpbad@example.com", Email = "zpbad@example.com" };
        await userManager.CreateAsync(user, "P@ssw0rd!");

        var (client, formToken) = await LoginAsync("zpbad@example.com");

        var response = await client.PostAsync("/zwiftpower/import", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = formToken,
            ["zwiftRiderId"] = "12345",
            ["resultsJson"] = "<html><body>ZwiftPower profile</body></html>"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/dashboard?zwiftpower=error", response.Headers.Location!.ToString());

        var raceResultRepository = scope.ServiceProvider.GetRequiredService<IRaceResultRepository>();
        Assert.Empty(await raceResultRepository.GetAllForUserAsync(user.Id));
    }
}
