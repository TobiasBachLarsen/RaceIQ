using RaceIQ.Application;

namespace RaceIQ.Web;

public static class ServiceCollectionExtensions
{
    // Register a provider sync service both as its concrete type (so it can be resolved
    // directly) and behind IProviderSyncService (so SyncCoordinator gets it in its set).
    public static IServiceCollection AddProviderSync<TService>(this IServiceCollection services)
        where TService : class, IProviderSyncService
    {
        services.AddScoped<TService>();
        services.AddScoped<IProviderSyncService>(sp => sp.GetRequiredService<TService>());
        return services;
    }
}
