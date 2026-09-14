using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RaceIQ.Web;
using RaceIQ.Web.Components;
using RaceIQ.Web.Components.Account;
using Anthropic;
using RaceIQ.Infrastructure;
using RaceIQ.Infrastructure.Claude;
using RaceIQ.Infrastructure.Strava;
using RaceIQ.Infrastructure.ZwiftPower;
using RaceIQ.Infrastructure.ZwiftRacing;
using RaceIQ.Application;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityUserAccessor>();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<RaceIQDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.Configure<StravaOAuthOptions>(builder.Configuration.GetSection("Strava"));
builder.Services.AddHttpClient<IStravaOAuthService, StravaOAuthService>();
builder.Services.AddScoped<IConnectedAccountRepository, EfConnectedAccountRepository>();

builder.Services.AddSingleton<StravaRequestThrottle>();
builder.Services.AddHttpClient<IStravaApiClient, StravaApiClient>();
builder.Services.AddScoped<StravaSyncService>();
builder.Services.AddScoped<IProviderSyncService>(sp => sp.GetRequiredService<StravaSyncService>());
builder.Services.AddScoped<IActivityRepository, EfActivityRepository>();

builder.Services.AddHttpClient<IZwiftPowerApiClient, ZwiftPowerApiClient>();
builder.Services.AddScoped<ZwiftPowerSyncService>();
builder.Services.AddScoped<IProviderSyncService>(sp => sp.GetRequiredService<ZwiftPowerSyncService>());
builder.Services.AddScoped<IRaceResultRepository, EfRaceResultRepository>();
builder.Services.AddScoped<IRaceResultMatcher, RaceResultMatcher>();

builder.Services.AddHttpClient<IZwiftRacingApiClient, ZwiftRacingApiClient>();
builder.Services.AddScoped<ZwiftRacingSyncService>();
builder.Services.AddScoped<IProviderSyncService>(sp => sp.GetRequiredService<ZwiftRacingSyncService>());

var anthropicApiKey = builder.Configuration["Anthropic:ApiKey"];
if (string.IsNullOrWhiteSpace(anthropicApiKey))
{
    anthropicApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
}

builder.Services.AddSingleton(new AnthropicClient
{
    ApiKey = anthropicApiKey
});
builder.Services.AddScoped<IClaudeClient, ClaudeClient>();
builder.Services.AddScoped<IActivityAnalysisService, ActivityAnalysisService>();
builder.Services.AddScoped<IAnalysisReportRepository, EfAnalysisReportRepository>();

builder.Services.AddIdentityCore<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = false)
    .AddEntityFrameworkStores<RaceIQDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RaceIQDbContext>();
    db.Database.Migrate();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

app.MapStravaAuthEndpoints();
app.MapZwiftPowerAuthEndpoints();
app.MapZwiftRacingAuthEndpoints();

app.Run();

// Exposes the implicit top-level Program class as public so
// WebApplicationFactory<Program> (RaceIQApiFactory, tests/RaceIQ.IntegrationTests)
// can reference it from the test assembly.
public partial class Program;
