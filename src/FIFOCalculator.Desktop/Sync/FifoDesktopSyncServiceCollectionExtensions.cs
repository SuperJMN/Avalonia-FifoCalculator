using FIFOCalculator.Persistence;
using FIFOCalculator.Sync;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Zafiro.UserStorage;

namespace FIFOCalculator.Desktop.Sync;

public static class FifoDesktopSyncServiceCollectionExtensions
{
    public static IServiceCollection AddFifoDesktopSync(this IServiceCollection services)
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
