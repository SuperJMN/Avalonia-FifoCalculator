using CSharpFunctionalExtensions;
using FIFOCalculator.Models;
using FIFOCalculator.Sync;
using Serilog;

namespace TestProject1;

public sealed class FifoSyncStartupTests
{
    [Fact]
    public async Task Start_ShouldReturnBeforeInitializationCompletes()
    {
        var initialization = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        var syncService = new PendingInitializeSyncService(initialization.Task);

        var start = Task.Run(() => FifoSyncStartup.Start(syncService, Log.Logger));

        await start.WaitAsync(TimeSpan.FromSeconds(1));
        await syncService.InitializeStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        initialization.Task.IsCompleted.Should().BeFalse();

        initialization.SetResult(Result.Success());
    }

    private sealed class PendingInitializeSyncService(Task<Result> initialization) : IFifoSyncService
    {
        public TaskCompletionSource InitializeStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public FifoSyncStatus Status => FifoSyncStatus.Unsupported("test");

        public IObservable<FifoSyncStatus> StatusChanged => System.Reactive.Linq.Observable.Return(Status);

        public Task<Result> Initialize(CancellationToken cancellationToken = default)
        {
            InitializeStarted.SetResult();
            return initialization;
        }

        public Task<Result> CreateIdentity(string password, EntryCatalog localCatalog, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Result> Unlock(string password, EntryCatalog localCatalog, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Result> ImportIdentity(byte[] identityExport, string password, EntryCatalog localCatalog, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Result<byte[]>> ExportIdentity(string password, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Result> SyncNow(EntryCatalog localCatalog, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Result> SaveLocalChange(EntryCatalog catalog, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Result<Maybe<EntryCatalog>>> ResolveConflict(FifoSyncConflictChoice choice, EntryCatalog localCatalog, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Result> Disconnect(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
