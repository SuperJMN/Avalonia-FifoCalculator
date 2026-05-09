using System;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using FIFOCalculator.Models;

namespace FIFOCalculator.Sync;

public sealed class NoOpFifoSyncService : IFifoSyncService
{
    private const string UnsupportedMessage = "Sync is available in the desktop app.";
    private readonly BehaviorSubject<FifoSyncStatus> statusChanged = new(FifoSyncStatus.Unsupported(UnsupportedMessage));

    public FifoSyncStatus Status => statusChanged.Value;

    public IObservable<FifoSyncStatus> StatusChanged => statusChanged;

    public Task<Result> Initialize(CancellationToken cancellationToken = default) => Task.FromResult(Result.Success());

    public Task<Result> CreateIdentity(string password, EntryCatalog localCatalog, CancellationToken cancellationToken = default)
    {
        return Unsupported();
    }

    public Task<Result> Unlock(string password, EntryCatalog localCatalog, CancellationToken cancellationToken = default)
    {
        return Unsupported();
    }

    public Task<Result> ImportIdentity(byte[] identityExport, string password, EntryCatalog localCatalog, CancellationToken cancellationToken = default)
    {
        return Unsupported();
    }

    public Task<Result<byte[]>> ExportIdentity(string password, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Failure<byte[]>(UnsupportedMessage));
    }

    public Task<Result> SyncNow(EntryCatalog localCatalog, CancellationToken cancellationToken = default)
    {
        return Unsupported();
    }

    public Task<Result> SaveLocalChange(EntryCatalog catalog, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    public Task<Result<Maybe<EntryCatalog>>> ResolveConflict(FifoSyncConflictChoice choice, EntryCatalog localCatalog, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success(Maybe<EntryCatalog>.None));
    }

    public Task<Result> Disconnect(CancellationToken cancellationToken = default)
    {
        return Unsupported();
    }

    private static Task<Result> Unsupported()
    {
        return Task.FromResult(Result.Failure(UnsupportedMessage));
    }
}
