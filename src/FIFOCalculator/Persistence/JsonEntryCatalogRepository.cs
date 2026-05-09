using System;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using FIFOCalculator.Models;
using Serilog;
using Zafiro.DivineBytes;
using Zafiro.UserStorage;
using Path = Zafiro.DivineBytes.Path;

namespace FIFOCalculator.Persistence;

public sealed class JsonEntryCatalogRepository : IEntryCatalogRepository
{
    private static readonly Path DatabaseKey = new(["database.json"]);

    private readonly IUserStorage storage;
    private readonly ILogger logger;

    public JsonEntryCatalogRepository(IUserStorage storage, ILogger? logger = null)
    {
        this.storage = storage;
        this.logger = logger ?? Log.Logger;
    }

    public async Task<Result<EntryCatalog>> Load()
    {
        var result = await storage.Load(DatabaseKey);
        if (result.IsFailure)
        {
            logger.Warning("Failed to load FIFO calculator database from storage key {Key}: {Error}", DatabaseKey, result.Error);
            return Result.Failure<EntryCatalog>(result.Error);
        }

        return await result.Value.Match(
            source => Load(source, DatabaseKey.Value),
            () => Task.FromResult(Result.Success(EmptyCatalog())));
    }

    public async Task<Result<EntryCatalog>> Load(INamedByteSource source)
    {
        return await Load(source, source.Name);
    }

    public async Task<Result> Save(EntryCatalog catalog)
    {
        try
        {
            var bytes = EntryCatalogJson.ToBytes(catalog);
            var result = await storage.Save(DatabaseKey, ByteSource.FromBytes(bytes));
            if (result.IsFailure)
            {
                logger.Warning("Failed to save FIFO calculator database to storage key {Key}: {Error}", DatabaseKey, result.Error);
            }

            return result;
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to save FIFO calculator database to storage key {Key}", DatabaseKey);
            return Result.Failure(ex.Message);
        }
    }

    private static EntryCatalog EmptyCatalog() => EntryCatalogJson.EmptyCatalog();

    private async Task<Result<EntryCatalog>> Load(IByteSource source, string sourceName)
    {
        var bytes = await source.ReadAll();
        if (bytes.IsFailure)
        {
            return Result.Failure<EntryCatalog>(bytes.Error);
        }

        var catalog = EntryCatalogJson.FromBytes(bytes.Value);
        if (catalog.IsFailure)
        {
            logger.Warning("Failed to load FIFO calculator database from {Source}: {Error}", sourceName, catalog.Error);
        }

        return catalog;
    }
}
