using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Zafiro.Sync.Client;

namespace FIFOCalculator.Sync.Zafiro;

public sealed class ZafiroSyncFifoRemoteCatalogClientFactory : IFifoRemoteCatalogClientFactory
{
    public IFifoRemoteCatalogClient Create(IFifoSyncIdentity identity)
    {
        if (identity is not ZafiroSyncFifoSyncIdentity zafiroSyncIdentity)
        {
            throw new ArgumentException("The FIFO sync identity was not created by the Zafiro.Sync provider.", nameof(identity));
        }

        var httpClient = new HttpClient
        {
            BaseAddress = FifoSyncDefaults.ServiceBaseUri,
        };

        var client = new ZafiroSyncClient(
            httpClient,
            new ZafiroSyncClientOptions
            {
                ServiceBaseUri = FifoSyncDefaults.ServiceBaseUri,
                AppId = zafiroSyncIdentity.Identity.AppId,
                DeviceId = zafiroSyncIdentity.Identity.DeviceId,
                AppDataKey = zafiroSyncIdentity.Identity.AppDataKey,
            },
            new AppIdentityTokenProvider(httpClient, FifoSyncDefaults.ServiceBaseUri, zafiroSyncIdentity.Identity),
            new AesGcmFileEncryptor(zafiroSyncIdentity.Identity.AppDataKey));

        return new ZafiroSyncFifoRemoteCatalogClient(client);
    }
}

public sealed class ZafiroSyncFifoRemoteCatalogClient(IZafiroSyncClient client) : IFifoRemoteCatalogClient
{
    public async Task<FifoRemoteCatalogLoadResult> Load(CancellationToken cancellationToken = default)
    {
        var result = await client.LoadAsync(FifoSyncDefaults.LogicalPath, cancellationToken);
        return result switch
        {
            LoadFileResult.Found found => new FifoRemoteCatalogLoadResult.Found(
                new FifoRemoteCatalogFile(found.Revision, null, found.Content)),
            LoadFileResult.NotFound => new FifoRemoteCatalogLoadResult.NotFound(),
            _ => throw new InvalidOperationException("Unknown Zafiro.Sync load result."),
        };
    }

    public async Task<FifoRemoteCatalogSaveResult> Save(byte[] content, long? baseRevision, CancellationToken cancellationToken = default)
    {
        var result = await client.SaveAsync(
            FifoSyncDefaults.LogicalPath,
            content,
            "application/json",
            baseRevision: baseRevision,
            cancellationToken: cancellationToken);

        return result switch
        {
            SaveFileResult.Saved saved => new FifoRemoteCatalogSaveResult.Saved(saved.Revision, saved.Cursor),
            SaveFileResult.Conflict conflict => new FifoRemoteCatalogSaveResult.Conflict(conflict.CurrentRevision, conflict.CurrentCursor),
            _ => throw new InvalidOperationException("Unknown Zafiro.Sync save result."),
        };
    }
}
