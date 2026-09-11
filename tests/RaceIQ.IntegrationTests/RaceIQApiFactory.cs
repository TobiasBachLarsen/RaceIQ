using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using RaceIQ.Infrastructure;
using RaceIQ.Infrastructure.Strava;
using Xunit;

namespace RaceIQ.IntegrationTests;

public class RaceIQApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public Func<HttpRequestMessage, HttpResponseMessage>? StravaOAuthResponder { get; set; }

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
        });
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaceIQDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
    }

    public new Task DisposeAsync() => Task.CompletedTask;
}
