using System.Reactive.Threading.Tasks;
using CSharpFunctionalExtensions;
using FIFOCalculator.Models;
using FIFOCalculator.Persistence;
using FIFOCalculator.Sync;
using FIFOCalculator.ViewModels;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Zafiro.Avalonia.Controls.Shell;
using Zafiro.Avalonia.Dialogs;
using Zafiro.DivineBytes;
using Zafiro.FileSystem.Mutable;
using Zafiro.UI;

namespace TestProject1;

public sealed class AppViewModelTests
{
    [Fact]
    public async Task Initialize_ShouldInitializeSyncAndLoadSavedData()
    {
        var catalog = new EntryCatalog(
            [new Entry(new DateTime(2026, 1, 10), 2, 100m)],
            [new Entry(new DateTime(2026, 2, 15), 1, 150m)]);
        var repository = new FakeEntryCatalogRepository { CatalogToLoad = catalog };
        var sync = new FakeFifoSyncService();
        var dataEntry = new DataEntryViewModel();
        var settings = CreateSettings(dataEntry, repository, sync);
        var sut = CreateSut(sync, settings);

        var result = await sut.Initialize.Execute().ToTask();

        result.IsSuccess.Should().BeTrue();
        sync.InitializeCalls.Should().Be(1);
        repository.LoadCalls.Should().Be(1);
        dataEntry.Inputs.ToEntries().Should().BeEquivalentTo(catalog.Inputs);
        dataEntry.Outputs.ToEntries().Should().BeEquivalentTo(catalog.Outputs);
    }

    [Fact]
    public async Task Initialize_ShouldOnlyRunOnce()
    {
        var repository = new FakeEntryCatalogRepository();
        var sync = new FakeFifoSyncService();
        var settings = CreateSettings(new DataEntryViewModel(), repository, sync);
        var sut = CreateSut(sync, settings);

        await sut.Initialize.Execute().ToTask();
        await sut.Initialize.Execute().ToTask();

        sync.InitializeCalls.Should().Be(1);
        repository.LoadCalls.Should().Be(1);
    }

    [Fact]
    public async Task Initialize_WhenSyncFails_ShouldStillLoadSavedData()
    {
        var catalog = new EntryCatalog(
            [new Entry(new DateTime(2026, 3, 5), 4, 50m)],
            []);
        var repository = new FakeEntryCatalogRepository { CatalogToLoad = catalog };
        var sync = new FakeFifoSyncService { InitializeResult = Result.Failure("sync unavailable") };
        var dataEntry = new DataEntryViewModel();
        var settings = CreateSettings(dataEntry, repository, sync);
        var sink = new CapturingSink();
        var sut = CreateSut(sync, settings, new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger());

        var result = await sut.Initialize.Execute().ToTask();

        result.IsSuccess.Should().BeTrue();
        sync.InitializeCalls.Should().Be(1);
        repository.LoadCalls.Should().Be(1);
        dataEntry.Inputs.ToEntries().Should().BeEquivalentTo(catalog.Inputs);
        sink.Events.Should().Contain(log => log.RenderMessage().Contains("sync unavailable"));
    }

    [Fact]
    public async Task Initialize_WhenSyncIsPending_ShouldStillLoadSavedData()
    {
        var initialization = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new FakeEntryCatalogRepository();
        var sync = new FakeFifoSyncService { InitializeTask = initialization.Task };
        var settings = CreateSettings(new DataEntryViewModel(), repository, sync);
        var sut = CreateSut(sync, settings);

        var initialize = sut.Initialize.Execute().ToTask();

        await sync.InitializeStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await repository.LoadStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        initialize.IsCompleted.Should().BeFalse();

        initialization.SetResult(Result.Success());
        var result = await initialize.WaitAsync(TimeSpan.FromSeconds(1));

        result.IsSuccess.Should().BeTrue();
    }

    private static AppViewModel CreateSut(FakeFifoSyncService sync, SettingsViewModel settings, ILogger? logger = null)
    {
        return new AppViewModel(new ShellDesign(), sync, settings, logger ?? new LoggerConfiguration().CreateLogger());
    }

    private static SettingsViewModel CreateSettings(
        DataEntryViewModel dataEntry,
        FakeEntryCatalogRepository repository,
        IFifoSyncService sync)
    {
        return new SettingsViewModel(
            dataEntry,
            repository,
            sync,
            new FakeDialog(),
            new FakeFileSystemPicker(),
            new SilentNotificationService(),
            TimeSpan.Zero);
    }

    private sealed class FakeEntryCatalogRepository : IEntryCatalogRepository
    {
        public EntryCatalog CatalogToLoad { get; init; } = new([], []);
        public int LoadCalls { get; private set; }
        public TaskCompletionSource LoadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<Result<EntryCatalog>> Load()
        {
            LoadCalls++;
            LoadStarted.TrySetResult();
            return Task.FromResult(Result.Success(CatalogToLoad));
        }

        public Task<Result<EntryCatalog>> Load(INamedByteSource source)
        {
            throw new NotSupportedException();
        }

        public Task<Result> Save(EntryCatalog catalog)
        {
            return Task.FromResult(Result.Success());
        }
    }

    private sealed class FakeFifoSyncService : IFifoSyncService
    {
        public Result InitializeResult { get; init; } = Result.Success();
        public Task<Result>? InitializeTask { get; init; }
        public TaskCompletionSource InitializeStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int InitializeCalls { get; private set; }

        public FifoSyncStatus Status { get; } = new(true, true, false, false, false, false, null, "Sync is not configured.", null);

        public IObservable<FifoSyncStatus> StatusChanged => System.Reactive.Linq.Observable.Return(Status);

        public Task<Result> Initialize(CancellationToken cancellationToken = default)
        {
            InitializeCalls++;
            InitializeStarted.TrySetResult();
            return InitializeTask ?? Task.FromResult(InitializeResult);
        }

        public Task<Result> CreateIdentity(string password, EntryCatalog localCatalog, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Result> Unlock(string password, EntryCatalog localCatalog, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Result> ImportIdentity(byte[] identityExport, string password, EntryCatalog localCatalog, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Result<byte[]>> ExportIdentity(string password, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Result> SyncNow(EntryCatalog localCatalog, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Result> SaveLocalChange(EntryCatalog catalog, CancellationToken cancellationToken = default) => Task.FromResult(Result.Success());

        public Task<Result<Maybe<EntryCatalog>>> ResolveConflict(FifoSyncConflictChoice choice, EntryCatalog localCatalog, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Result> Disconnect(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeFileSystemPicker : IFileSystemPicker
    {
        public Task<Result<IEnumerable<INamedByteSource>>> PickForOpenMultiple(params FileTypeFilter[] filters) => throw new NotSupportedException();

        public Task<Result<Maybe<INamedByteSource>>> PickForOpen(params FileTypeFilter[] filters) => throw new NotSupportedException();

        public Task<Maybe<IMutableFile>> PickForSave(string desiredName, Maybe<string> defaultExtension, params FileTypeFilter[] filters) => throw new NotSupportedException();

        public Task<Maybe<IMutableDirectory>> PickFolder() => throw new NotSupportedException();
    }

    private sealed class FakeDialog : IDialog
    {
        public Task<bool> Show<TViewModel>(
            Maybe<TViewModel> viewModel,
            Maybe<IObservable<string>> title,
            Func<Maybe<TViewModel>, ICloseable, IEnumerable<IOption>> optionsFactory,
            Maybe<object> icon = default,
            DialogTone tone = DialogTone.Neutral,
            DialogSize size = DialogSize.Auto)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class SilentNotificationService : INotificationService
    {
        public Task Show(string message, Maybe<string> title) => Task.CompletedTask;
    }

    private sealed class CapturingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent)
        {
            Events.Add(logEvent);
        }
    }
}
