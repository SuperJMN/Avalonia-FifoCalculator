using System;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Serilog;

namespace FIFOCalculator.Sync;

public static class FifoSyncStartup
{
    public static void Start(IFifoSyncService syncService, ILogger logger)
    {
        _ = Initialize(syncService, logger);
    }

    private static async Task Initialize(IFifoSyncService syncService, ILogger logger)
    {
        try
        {
            var result = await syncService.Initialize();
            if (result.IsFailure)
            {
                logger.Warning("Failed to initialize FIFO sync: {Error}", result.Error);
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to initialize FIFO sync.");
        }
    }
}
