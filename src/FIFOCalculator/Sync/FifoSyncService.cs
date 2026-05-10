using System;
using System.Globalization;
using System.Linq;
using System.Reactive.Subjects;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using FIFOCalculator.Models;
using FIFOCalculator.Persistence;
using Serilog;
using Zafiro.DivineBytes;
using Zafiro.UserStorage;

namespace FIFOCalculator.Sync;

public sealed class FifoSyncService : IFifoSyncService
{
    private static readonly JsonSerializerOptions StateJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly IEntryCatalogRepository repository;
    private readonly IUserStorage storage;
    private readonly IFifoSyncIdentityProvider identityProvider;
    private readonly IFifoRemoteCatalogClientFactory remoteCatalogClientFactory;
    private readonly ILogger logger;
    private readonly BehaviorSubject<FifoSyncStatus> statusChanged;
    private readonly SemaphoreSlim initializationGate = new(1, 1);
    private FifoSyncState state = new();
    private IFifoSyncIdentity? identity;
    private IFifoRemoteCatalogClient? remoteCatalogClient;
    private bool hasStoredIdentity;
    private bool initialized;

    public FifoSyncService(
        IEntryCatalogRepository repository,
        IUserStorage storage,
        IFifoSyncIdentityProvider identityProvider,
        IFifoRemoteCatalogClientFactory remoteCatalogClientFactory,
        ILogger? logger = null)
    {
        this.repository = repository;
        this.storage = storage;
        this.identityProvider = identityProvider;
        this.remoteCatalogClientFactory = remoteCatalogClientFactory;
        this.logger = logger ?? Log.Logger;

        Status = new FifoSyncStatus(true, false, false, false, false, null, "Sync is not configured.", null);
        statusChanged = new BehaviorSubject<FifoSyncStatus>(Status);
    }

    public FifoSyncStatus Status { get; private set; }

    public IObservable<FifoSyncStatus> StatusChanged => statusChanged;

    public async Task<Result> Initialize(CancellationToken cancellationToken = default)
    {
        if (initialized)
        {
            return Result.Success();
        }

        await initializationGate.WaitAsync(cancellationToken);
        try
        {
            if (initialized)
            {
                return Result.Success();
            }

            var stateResult = await LoadState(cancellationToken);
            if (stateResult.IsFailure)
            {
                return Result.Failure(stateResult.Error);
            }

            var exists = await storage.Exists(FifoSyncDefaults.IdentityKey, cancellationToken);
            if (exists.IsFailure)
            {
                return Result.Failure(exists.Error);
            }

            state = stateResult.Value;
            hasStoredIdentity = exists.Value;
            initialized = true;
            Publish();
            return Result.Success();
        }
        finally
        {
            initializationGate.Release();
        }
    }

    public async Task<Result> CreateIdentity(string password, EntryCatalog localCatalog, CancellationToken cancellationToken = default)
    {
        return await RunSyncOperation(async () =>
        {
            var created = identityProvider.Create(password);
            if (created.IsFailure)
            {
                return Result.Failure(created.Error);
            }

            var valid = ValidateIdentity(created.Value);
            if (valid.IsFailure)
            {
                return valid;
            }

            var save = await storage.Save(FifoSyncDefaults.IdentityKey, ByteSource.FromBytes(created.Value.ProtectedExport), cancellationToken);
            if (save.IsFailure)
            {
                return save;
            }

            identity = created.Value;
            remoteCatalogClient = remoteCatalogClientFactory.Create(identity);
            hasStoredIdentity = true;
            state = state with { Conflict = null, IsDirty = false };

            return await Synchronize(localCatalog, cancellationToken);
        }, cancellationToken);
    }

    public async Task<Result> Unlock(string password, EntryCatalog localCatalog, CancellationToken cancellationToken = default)
    {
        return await RunSyncOperation(async () =>
        {
            var bytes = await LoadIdentityBytes(cancellationToken);
            if (bytes.IsFailure)
            {
                return Result.Failure(bytes.Error);
            }

            var imported = identityProvider.Import(password, bytes.Value);
            if (imported.IsFailure)
            {
                return Result.Failure(imported.Error);
            }

            var valid = ValidateIdentity(imported.Value);
            if (valid.IsFailure)
            {
                return valid;
            }

            identity = imported.Value;
            remoteCatalogClient = remoteCatalogClientFactory.Create(identity);
            hasStoredIdentity = true;

            return await Synchronize(localCatalog, cancellationToken);
        }, cancellationToken);
    }

    public async Task<Result> ImportIdentity(byte[] identityExport, string password, EntryCatalog localCatalog, CancellationToken cancellationToken = default)
    {
        return await RunSyncOperation(async () =>
        {
            var imported = identityProvider.Import(password, identityExport);
            if (imported.IsFailure)
            {
                return Result.Failure(imported.Error);
            }

            var valid = ValidateIdentity(imported.Value);
            if (valid.IsFailure)
            {
                return valid;
            }

            var save = await storage.Save(FifoSyncDefaults.IdentityKey, ByteSource.FromBytes(imported.Value.ProtectedExport), cancellationToken);
            if (save.IsFailure)
            {
                return save;
            }

            identity = imported.Value;
            remoteCatalogClient = remoteCatalogClientFactory.Create(identity);
            hasStoredIdentity = true;
            state = state with { Conflict = null, IsDirty = false };

            return await Synchronize(localCatalog, cancellationToken);
        }, cancellationToken);
    }

    public async Task<Result<byte[]>> ExportIdentity(string password, CancellationToken cancellationToken = default)
    {
        var initializedResult = await EnsureInitialized(cancellationToken);
        if (initializedResult.IsFailure)
        {
            return Result.Failure<byte[]>(initializedResult.Error);
        }

        var bytes = await LoadIdentityBytes(cancellationToken);
        if (bytes.IsFailure)
        {
            return bytes;
        }

        var imported = identityProvider.Import(password, bytes.Value);
        if (imported.IsFailure)
        {
            return Result.Failure<byte[]>(imported.Error);
        }

        var valid = ValidateIdentity(imported.Value);
        return valid.IsFailure ? Result.Failure<byte[]>(valid.Error) : bytes.Value;
    }

    public async Task<Result> SyncNow(EntryCatalog localCatalog, CancellationToken cancellationToken = default)
    {
        return await RunSyncOperation(() => Synchronize(localCatalog, cancellationToken), cancellationToken);
    }

    public async Task<Result> SaveLocalChange(EntryCatalog catalog, CancellationToken cancellationToken = default)
    {
        var initializedResult = await EnsureInitialized(cancellationToken);
        if (initializedResult.IsFailure)
        {
            return initializedResult;
        }

        if (!hasStoredIdentity)
        {
            return Result.Success();
        }

        if (remoteCatalogClient is null)
        {
            state = state with { IsDirty = true };
            var save = await SaveState(cancellationToken);
            Publish("Sync is locked. Local changes will upload after unlock.");
            return save;
        }

        if (state.Conflict is not null)
        {
            state = state with
            {
                IsDirty = true,
                Conflict = CreateConflict(catalog, state.Conflict.Remote.ToCatalog(), state.Conflict.RemoteRevision, state.Conflict.RemoteCursor),
            };
            var save = await SaveState(cancellationToken);
            Publish("Sync conflict. Choose which database to keep.");
            return save;
        }

        return await UploadLocal(catalog, state.RemoteRevision, "Local changes synced.", treatRemoteFailureAsDirty: true, cancellationToken);
    }

    public async Task<Result<Maybe<EntryCatalog>>> ResolveConflict(FifoSyncConflictChoice choice, EntryCatalog localCatalog, CancellationToken cancellationToken = default)
    {
        var initializedResult = await EnsureInitialized(cancellationToken);
        if (initializedResult.IsFailure)
        {
            return Result.Failure<Maybe<EntryCatalog>>(initializedResult.Error);
        }

        if (state.Conflict is null)
        {
            return Maybe<EntryCatalog>.None;
        }

        if (choice == FifoSyncConflictChoice.Cancel)
        {
            return Maybe<EntryCatalog>.None;
        }

        if (choice == FifoSyncConflictChoice.UseRemote)
        {
            var remoteCatalog = state.Conflict.Remote.ToCatalog();
            var save = await repository.Save(remoteCatalog);
            if (save.IsFailure)
            {
                return Result.Failure<Maybe<EntryCatalog>>(save.Error);
            }

            var remoteBytes = EntryCatalogJson.ToBytes(remoteCatalog);
            state = state with
            {
                RemoteRevision = state.Conflict.RemoteRevision,
                Cursor = state.Conflict.RemoteCursor,
                LastSyncedHash = Hash(remoteBytes),
                IsDirty = false,
                Conflict = null,
                LastSyncedAt = DateTimeOffset.UtcNow,
            };
            var saveState = await SaveState(cancellationToken);
            if (saveState.IsFailure)
            {
                return Result.Failure<Maybe<EntryCatalog>>(saveState.Error);
            }

            Publish("Remote database applied.");
            return Maybe.From(remoteCatalog);
        }

        var upload = await UploadLocal(
            localCatalog,
            state.Conflict.RemoteRevision,
            "Local database kept and synced.",
            treatRemoteFailureAsDirty: true,
            cancellationToken);

        return upload.IsFailure
            ? Result.Failure<Maybe<EntryCatalog>>(upload.Error)
            : Maybe<EntryCatalog>.None;
    }

    public async Task<Result> Disconnect(CancellationToken cancellationToken = default)
    {
        var deleteIdentity = await storage.Delete(FifoSyncDefaults.IdentityKey, cancellationToken);
        if (deleteIdentity.IsFailure)
        {
            return deleteIdentity;
        }

        var deleteState = await storage.Delete(FifoSyncDefaults.StateKey, cancellationToken);
        if (deleteState.IsFailure)
        {
            return deleteState;
        }

        identity = null;
        remoteCatalogClient = null;
        hasStoredIdentity = false;
        state = new FifoSyncState();
        initialized = true;
        Publish("Sync disconnected.");

        return Result.Success();
    }

    private async Task<Result> Synchronize(EntryCatalog localCatalog, CancellationToken cancellationToken)
    {
        var initializedResult = await EnsureInitialized(cancellationToken);
        if (initializedResult.IsFailure)
        {
            return initializedResult;
        }

        if (!hasStoredIdentity)
        {
            Publish("Create or import a sync identity first.");
            return Result.Failure("Create or import a sync identity first.");
        }

        if (remoteCatalogClient is null)
        {
            Publish("Unlock sync to connect this device.");
            return Result.Failure("Unlock sync to connect this device.");
        }

        var localBytes = EntryCatalogJson.ToBytes(localCatalog);
        var localHash = Hash(localBytes);
        FifoRemoteCatalogLoadResult remoteLoad;

        try
        {
            remoteLoad = await remoteCatalogClient.Load(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to load remote FIFO database.");
            state = state with { IsDirty = true };
            await SaveState(cancellationToken);
            Publish("Remote sync failed. Local data is kept.");
            return Result.Failure(ex.Message);
        }

        if (remoteLoad is FifoRemoteCatalogLoadResult.NotFound)
        {
            return await UploadLocal(localCatalog, null, "Local database uploaded.", treatRemoteFailureAsDirty: false, cancellationToken);
        }

        var remoteFile = ((FifoRemoteCatalogLoadResult.Found)remoteLoad).File;
        var remoteCatalog = EntryCatalogJson.FromBytes(remoteFile.Content);
        if (remoteCatalog.IsFailure)
        {
            return Result.Failure(remoteCatalog.Error);
        }

        var remoteHash = Hash(remoteFile.Content);
        if (remoteHash == localHash)
        {
            state = state with
            {
                RemoteRevision = remoteFile.Revision,
                Cursor = remoteFile.Cursor,
                LastSyncedHash = localHash,
                IsDirty = false,
                Conflict = null,
                LastSyncedAt = DateTimeOffset.UtcNow,
            };
            var save = await SaveState(cancellationToken);
            Publish("Database is synced.");
            return save;
        }

        if (IsEmpty(localCatalog) && state.LastSyncedHash is null)
        {
            return await ApplyRemote(remoteFile, remoteCatalog.Value, "Remote database restored.", cancellationToken);
        }

        if (state.LastSyncedHash == localHash)
        {
            return await ApplyRemote(remoteFile, remoteCatalog.Value, "Remote changes downloaded.", cancellationToken);
        }

        if (state.RemoteRevision == remoteFile.Revision)
        {
            return await UploadLocal(localCatalog, remoteFile.Revision, "Local changes synced.", treatRemoteFailureAsDirty: false, cancellationToken);
        }

        return await MarkConflict(localCatalog, remoteCatalog.Value, remoteFile.Revision, remoteFile.Cursor, cancellationToken);
    }

    private async Task<Result> UploadLocal(
        EntryCatalog catalog,
        long? baseRevision,
        string successMessage,
        bool treatRemoteFailureAsDirty,
        CancellationToken cancellationToken)
    {
        if (remoteCatalogClient is null)
        {
            return Result.Failure("Unlock sync to connect this device.");
        }

        var bytes = EntryCatalogJson.ToBytes(catalog);
        FifoRemoteCatalogSaveResult save;

        try
        {
            save = await remoteCatalogClient.Save(bytes, baseRevision, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to save remote FIFO database.");
            state = state with { IsDirty = true };
            var saveDirty = await SaveState(cancellationToken);
            Publish("Remote sync failed. Local data is kept.");
            return treatRemoteFailureAsDirty && saveDirty.IsSuccess ? Result.Success() : Result.Failure(ex.Message);
        }

        if (save is FifoRemoteCatalogSaveResult.Saved saved)
        {
            state = state with
            {
                RemoteRevision = saved.Revision,
                Cursor = saved.Cursor,
                LastSyncedHash = Hash(bytes),
                IsDirty = false,
                Conflict = null,
                LastSyncedAt = DateTimeOffset.UtcNow,
            };
            var saveState = await SaveState(cancellationToken);
            Publish(successMessage);
            return saveState;
        }

        return await RefreshConflict(catalog, cancellationToken);
    }

    private async Task<Result> RefreshConflict(EntryCatalog localCatalog, CancellationToken cancellationToken)
    {
        if (remoteCatalogClient is null)
        {
            return Result.Failure("Unlock sync to connect this device.");
        }

        var remoteLoad = await remoteCatalogClient.Load(cancellationToken);
        if (remoteLoad is FifoRemoteCatalogLoadResult.NotFound)
        {
            return await UploadLocal(localCatalog, null, "Local database uploaded.", treatRemoteFailureAsDirty: true, cancellationToken);
        }

        var remoteFile = ((FifoRemoteCatalogLoadResult.Found)remoteLoad).File;
        var remoteCatalog = EntryCatalogJson.FromBytes(remoteFile.Content);
        return remoteCatalog.IsFailure
            ? Result.Failure(remoteCatalog.Error)
            : await MarkConflict(localCatalog, remoteCatalog.Value, remoteFile.Revision, remoteFile.Cursor, cancellationToken);
    }

    private async Task<Result> ApplyRemote(
        FifoRemoteCatalogFile remoteFile,
        EntryCatalog remoteCatalog,
        string message,
        CancellationToken cancellationToken)
    {
        var saveLocal = await repository.Save(remoteCatalog);
        if (saveLocal.IsFailure)
        {
            return saveLocal;
        }

        state = state with
        {
            RemoteRevision = remoteFile.Revision,
            Cursor = remoteFile.Cursor,
            LastSyncedHash = Hash(remoteFile.Content),
            IsDirty = false,
            Conflict = null,
            LastSyncedAt = DateTimeOffset.UtcNow,
        };
        var saveState = await SaveState(cancellationToken);
        Publish(message);
        return saveState;
    }

    private async Task<Result> MarkConflict(
        EntryCatalog localCatalog,
        EntryCatalog remoteCatalog,
        long remoteRevision,
        string? remoteCursor,
        CancellationToken cancellationToken)
    {
        state = state with
        {
            RemoteRevision = remoteRevision,
            Cursor = remoteCursor,
            IsDirty = true,
            Conflict = CreateConflict(localCatalog, remoteCatalog, remoteRevision, remoteCursor),
        };

        var saveState = await SaveState(cancellationToken);
        Publish("Sync conflict. Choose which database to keep.");
        return saveState;
    }

    private FifoSyncConflict CreateConflict(EntryCatalog localCatalog, EntryCatalog remoteCatalog, long remoteRevision, string? remoteCursor)
    {
        var localBytes = EntryCatalogJson.ToBytes(localCatalog);
        var remoteBytes = EntryCatalogJson.ToBytes(remoteCatalog);

        return new FifoSyncConflict(
            FifoCatalogSnapshot.FromCatalog(localCatalog),
            FifoCatalogSnapshot.FromCatalog(remoteCatalog),
            remoteRevision,
            remoteCursor,
            Hash(localBytes),
            Hash(remoteBytes),
            Summarize(localCatalog),
            Summarize(remoteCatalog));
    }

    private async Task<Result> RunSyncOperation(Func<Task<Result>> operation, CancellationToken cancellationToken)
    {
        var initializedResult = await EnsureInitialized(cancellationToken);
        if (initializedResult.IsFailure)
        {
            return initializedResult;
        }

        Publish(isSyncing: true);
        var result = await operation();
        if (result.IsFailure)
        {
            Publish(result.Error);
        }
        else
        {
            Publish();
        }

        return result;
    }

    private async Task<Result> EnsureInitialized(CancellationToken cancellationToken)
    {
        return initialized ? Result.Success() : await Initialize(cancellationToken);
    }

    private async Task<Result<FifoSyncState>> LoadState(CancellationToken cancellationToken)
    {
        var load = await storage.Load(FifoSyncDefaults.StateKey, cancellationToken);
        if (load.IsFailure)
        {
            return Result.Failure<FifoSyncState>(load.Error);
        }

        if (load.Value.HasNoValue)
        {
            return new FifoSyncState();
        }

        var bytes = await load.Value.Value.ReadAll();
        if (bytes.IsFailure)
        {
            return Result.Failure<FifoSyncState>(bytes.Error);
        }

        try
        {
            return JsonSerializer.Deserialize<FifoSyncState>(bytes.Value, StateJsonOptions) ?? new FifoSyncState();
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to load FIFO sync state.");
            return new FifoSyncState();
        }
    }

    private Task<Result> SaveState(CancellationToken cancellationToken)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(state, StateJsonOptions);
        return storage.Save(FifoSyncDefaults.StateKey, ByteSource.FromBytes(bytes), cancellationToken);
    }

    private async Task<Result<byte[]>> LoadIdentityBytes(CancellationToken cancellationToken)
    {
        var load = await storage.Load(FifoSyncDefaults.IdentityKey, cancellationToken);
        if (load.IsFailure)
        {
            return Result.Failure<byte[]>(load.Error);
        }

        if (load.Value.HasNoValue)
        {
            return Result.Failure<byte[]>("No sync identity is stored.");
        }

        var bytes = await load.Value.Value.ReadAll();
        return bytes.IsFailure ? Result.Failure<byte[]>(bytes.Error) : bytes.Value;
    }

    private static Result ValidateIdentity(IFifoSyncIdentity identity)
    {
        return identity.AppId == FifoSyncDefaults.AppId
            ? Result.Success()
            : Result.Failure($"The identity belongs to '{identity.AppId}', not '{FifoSyncDefaults.AppId}'.");
    }

    private void Publish(string? message = null, bool isSyncing = false)
    {
        Status = new FifoSyncStatus(
            true,
            hasStoredIdentity,
            identity is not null,
            isSyncing,
            state.IsDirty,
            state.Conflict,
            message ?? CreateStatusMessage(),
            state.LastSyncedAt);

        statusChanged.OnNext(Status);
    }

    private string CreateStatusMessage()
    {
        if (!hasStoredIdentity)
        {
            return "Sync is not configured.";
        }

        if (state.Conflict is not null)
        {
            return "Sync conflict. Choose which database to keep.";
        }

        if (identity is null)
        {
            return "Sync identity is stored. Unlock to sync this device.";
        }

        if (state.IsDirty)
        {
            return "Local changes are waiting to sync.";
        }

        return state.LastSyncedAt is null
            ? "Sync is ready."
            : $"Synced {state.LastSyncedAt.Value.LocalDateTime:g}.";
    }

    private static bool IsEmpty(EntryCatalog catalog)
    {
        return catalog.Inputs.Count == 0 && catalog.Outputs.Count == 0;
    }

    private static string Summarize(EntryCatalog catalog)
    {
        var inputUnits = catalog.Inputs.Sum(entry => entry.Units);
        var outputUnits = catalog.Outputs.Sum(entry => entry.Units);

        return string.Format(
            CultureInfo.InvariantCulture,
            "{0} inputs ({1:0.####} units), {2} outputs ({3:0.####} units)",
            catalog.Inputs.Count,
            inputUnits,
            catalog.Outputs.Count,
            outputUnits);
    }

    private static string Hash(ReadOnlySpan<byte> bytes)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(bytes, hash);
        return $"sha256:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }
}
