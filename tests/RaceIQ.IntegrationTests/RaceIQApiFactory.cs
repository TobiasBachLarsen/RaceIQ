using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using RaceIQ.Application;
using RaceIQ.Infrastructure;
using RaceIQ.Infrastructure.Strava;
using RaceIQ.Infrastructure.ZwiftPower;
using Xunit;

// Both StravaCallbackTests and SyncAndAnalyzeFlowTests use IClassFixture<RaceIQApiFactory>,
// so xunit creates one RaceIQApiFactory instance per test class. Each instance's
// InitializeAsync drops and recreates the SAME physical "raceiq_test" Postgres database.
// xunit runs different test classes' fixtures concurrently by default, which raced the two
// EnsureDeletedAsync/EnsureCreatedAsync calls against each other and intermittently failed
// with "database raceiq_test does not exist". Disabling collection parallelization serializes
// fixture setup across classes so this doesn't race.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace RaceIQ.IntegrationTests;

public class RaceIQApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public Func<HttpRequestMessage, HttpResponseMessage>? StravaOAuthResponder { get; set; }
    public Func<HttpRequestMessage, HttpResponseMessage>? StravaApiResponder { get; set; }
    public Func<HttpRequestMessage, HttpResponseMessage>? ZwiftPowerApiResponder { get; set; }
    public string ClaudeAnalysisText { get; set; } = "Fake analysis: pacing was even throughout.";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var dbDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<RaceIQDbContext>));
            if (dbDescriptor is not null)
                services.Remove(dbDescriptor);

            services.AddDbContext<RaceIQDbContext>(options =>
                options.UseNpgsql($"Host=localhost;Database=raceiq_test;Username=raceiq;Password=raceiq_dev"));

            services.AddHttpClient<IStravaOAuthService, StravaOAuthService>()
                .ConfigurePrimaryHttpMessageHandler(() =>
                    new FakeHttpMessageHandler(req => StravaOAuthResponder?.Invoke(req)
                        ?? throw new InvalidOperationException("No StravaOAuthResponder configured for this test.")));

            services.AddHttpClient<IStravaApiClient, StravaApiClient>()
                .ConfigurePrimaryHttpMessageHandler(() =>
                    new FakeHttpMessageHandler(req => StravaApiResponder?.Invoke(req)
                        ?? throw new InvalidOperationException("No StravaApiResponder configured for this test.")));

            services.AddHttpClient<IZwiftPowerApiClient, ZwiftPowerApiClient>()
                .ConfigurePrimaryHttpMessageHandler(() =>
                    new FakeHttpMessageHandler(req => ZwiftPowerApiResponder?.Invoke(req)
                        ?? throw new InvalidOperationException("No ZwiftPowerApiResponder configured for this test.")));

            var claudeDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IClaudeClient));
            if (claudeDescriptor is not null)
                services.Remove(claudeDescriptor);
            services.AddScoped<IClaudeClient>(_ => new TestClaudeClient(() => ClaudeAnalysisText));
        });
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaceIQDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
    }

    public new Task DisposeAsync() => Task.CompletedTask;
}

public class TestClaudeClient : IClaudeClient
{
    private readonly Func<string> _responseAccessor;

    public TestClaudeClient(Func<string> responseAccessor)
    {
        _responseAccessor = responseAccessor;
    }

    public Task<string> GenerateAnalysisAsync(string prompt) =>
        Task.FromResult(_responseAccessor());
}
