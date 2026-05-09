using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using FIFOCalculator.Models;
using Path = Zafiro.DivineBytes.Path;

namespace FIFOCalculator.Sync;

public static class FifoSyncDefaults
{
    public const string AppId = "fifo-calculator";
    public const string DisplayName = "FIFO Calculator";
    public const string LogicalPath = "database.json";
    public static readonly Uri ServiceBaseUri = new("https://filesync.superjmn.com");
    public static readonly Path IdentityKey = new(["sync", "identity.json"]);
    public static readonly Path StateKey = new(["sync", "state.json"]);
    public static readonly Path DatabaseKey = new(["database.json"]);
}

public interface IFifoSyncService
{
    FifoSyncStatus Status { get; }
    IObservable<FifoSyncStatus> StatusChanged { get; }

    Task<Result> Initialize(CancellationToken cancellationToken = default);
    Task<Result> CreateIdentity(string password, EntryCatalog localCatalog, CancellationToken cancellationToken = default);
    Task<Result> Unlock(string password, EntryCatalog localCatalog, CancellationToken cancellationToken = default);
    Task<Result> ImportIdentity(byte[] identityExport, string password, EntryCatalog localCatalog, CancellationToken cancellationToken = default);
    Task<Result<byte[]>> ExportIdentity(string password, CancellationToken cancellationToken = default);
    Task<Result> SyncNow(EntryCatalog localCatalog, CancellationToken cancellationToken = default);
    Task<Result> SaveLocalChange(EntryCatalog catalog, CancellationToken cancellationToken = default);
    Task<Result<Maybe<EntryCatalog>>> ResolveConflict(FifoSyncConflictChoice choice, EntryCatalog localCatalog, CancellationToken cancellationToken = default);
    Task<Result> Disconnect(CancellationToken cancellationToken = default);
}

public sealed record FifoSyncStatus(
    bool IsSupported,
    bool HasIdentity,
    bool IsUnlocked,
    bool IsSyncing,
    bool IsDirty,
    FifoSyncConflict? Conflict,
    string Message,
    DateTimeOffset? LastSyncedAt)
{
    public bool HasConflict => Conflict is not null;

    public static FifoSyncStatus Unsupported(string message)
    {
        return new FifoSyncStatus(false, false, false, false, false, null, message, null);
    }
}

public sealed record FifoSyncState
{
    public long? RemoteRevision { get; init; }
    public string? Cursor { get; init; }
    public string? LastSyncedHash { get; init; }
    public bool IsDirty { get; init; }
    public FifoSyncConflict? Conflict { get; init; }
    public DateTimeOffset? LastSyncedAt { get; init; }
}

public sealed record FifoCatalogSnapshot(List<Entry> Inputs, List<Entry> Outputs)
{
    public static FifoCatalogSnapshot FromCatalog(EntryCatalog catalog)
    {
        return new FifoCatalogSnapshot([.. catalog.Inputs], [.. catalog.Outputs]);
    }

    public EntryCatalog ToCatalog()
    {
        return new EntryCatalog([.. Inputs], [.. Outputs]);
    }
}

public sealed record FifoSyncConflict(
    FifoCatalogSnapshot Local,
    FifoCatalogSnapshot Remote,
    long RemoteRevision,
    string? RemoteCursor,
    string LocalHash,
    string RemoteHash,
    string LocalSummary,
    string RemoteSummary);

public enum FifoSyncConflictChoice
{
    UseRemote,
    KeepLocal,
    Cancel
}

public interface IFifoSyncIdentity
{
    string AppId { get; }
    byte[] ProtectedExport { get; }
}

public interface IFifoSyncIdentityProvider
{
    Result<IFifoSyncIdentity> Create(string password);
    Result<IFifoSyncIdentity> Import(string password, byte[] protectedExport);
}

public interface IFifoRemoteCatalogClientFactory
{
    IFifoRemoteCatalogClient Create(IFifoSyncIdentity identity);
}

public interface IFifoRemoteCatalogClient
{
    Task<FifoRemoteCatalogLoadResult> Load(CancellationToken cancellationToken = default);
    Task<FifoRemoteCatalogSaveResult> Save(byte[] content, long? baseRevision, CancellationToken cancellationToken = default);
}

public sealed record FifoRemoteCatalogFile(long Revision, string? Cursor, byte[] Content);

public abstract record FifoRemoteCatalogLoadResult
{
    public sealed record Found(FifoRemoteCatalogFile File) : FifoRemoteCatalogLoadResult;
    public sealed record NotFound : FifoRemoteCatalogLoadResult;
}

public abstract record FifoRemoteCatalogSaveResult
{
    public sealed record Saved(long Revision, string? Cursor) : FifoRemoteCatalogSaveResult;
    public sealed record Conflict(long CurrentRevision, string? CurrentCursor) : FifoRemoteCatalogSaveResult;
}
