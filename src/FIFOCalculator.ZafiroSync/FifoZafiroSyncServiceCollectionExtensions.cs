using FIFOCalculator.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Zafiro.UserStorage;

namespace FIFOCalculator.Sync.Zafiro;

public static class FifoZafiroSyncServiceCollectionExtensions
{
    public static IServiceCollection AddFifoZafiroSync(this IServiceCollection services)
    {
        services.AddSingleton<IFifoSyncIdentityProvider, ZafiroSyncFifoSyncIdentityProvider>();
        services.AddSingleton<IFifoRemoteCatalogClientFactory, ZafiroSyncFifoRemoteCatalogClientFactory>();
        services.AddSingleton<IFifoSyncService>(provider => new FifoSyncService(
            provider.GetRequiredService<IEntryCatalogRepository>(),
            provider.GetRequiredService<IUserStorage>(),
            provider.GetRequiredService<IFifoSyncIdentityProvider>(),
            provider.GetRequiredService<IFifoRemoteCatalogClientFactory>(),
            Log.Logger));

        return services;
    }
}
