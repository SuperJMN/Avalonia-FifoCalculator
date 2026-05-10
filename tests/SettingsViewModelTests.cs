using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Text;
using CSharpFunctionalExtensions;
using FIFOCalculator.Models;
using FIFOCalculator.Persistence;
using FIFOCalculator.Sync;
using FIFOCalculator.ViewModels;
using System.Reactive.Subjects;
using Zafiro.Avalonia.Dialogs;
using Zafiro.DivineBytes;
using Zafiro.FileSystem.Mutable;
using Zafiro.UI;

namespace TestProject1;

public sealed class SettingsViewModelTests
{
    [Fact]
    public async Task LoadSavedData_LoadsCatalogFromRepository()
    {
        var catalog = new EntryCatalog(
            [new Entry(new DateTime(2026, 1, 10), 2, 100m)],
            [new Entry(new DateTime(2026, 2, 15), 1, 150m)]);
        var repository = new FakeEntryCatalogRepository { CatalogToLoad = catalog };
        var dataEntry = new DataEntryViewModel();
        var sut = CreateSut(dataEntry, repository);

        await sut.LoadSavedData.Execute().ToTask();

        dataEntry.Inputs.ToEntries().Should().BeEquivalentTo(catalog.Inputs);
        dataEntry.Outputs.ToEntries().Should().BeEquivalentTo(catalog.Outputs);
    }

    [Fact]
    public async Task ClearData_WhenConfirmed_ClearsAndSavesCurrentCatalog()
    {
        var repository = new FakeEntryCatalogRepository();
        var dataEntry = new DataEntryViewModel();
        dataEntry.LoadCatalog(new EntryCatalog(
            [new Entry(new DateTime(2026, 1, 10), 2, 100m)],
            [new Entry(new DateTime(2026, 2, 15), 1, 150m)]));
        var dialog = new FakeDialog();
        var sut = CreateSut(dataEntry, repository, dialog);

        await sut.ClearData.Execute().ToTask();

        dialog.ShownTitles.Should().Contain("Clear data");
        dataEntry.Inputs.ToEntries().Should().BeEmpty();
        dataEntry.Outputs.ToEntries().Should().BeEmpty();
        repository.SavedCatalog.Should().BeEquivalentTo(new EntryCatalog([], []));
    }

    [Fact]
    public async Task ImportDataFile_WhenConfirmed_LoadsSelectedJsonAndSavesIt()
    {
        var imported = new EntryCatalog(
            [new Entry(new DateTime(2026, 1, 10), 2, 100m)],
            [new Entry(new DateTime(2026, 2, 15), 1, 150m)]);
        var repository = new FakeEntryCatalogRepository { CatalogToImport = imported };
        var picker = new FakeFileSystemPicker
        {
            FileToOpen = new Resource("import.json", ByteSource.FromBytes(Encoding.UTF8.GetBytes("{}")))
        };
        var dataEntry = new DataEntryViewModel();
        var dialog = new FakeDialog();
        var sut = CreateSut(dataEntry, repository, dialog, picker);

        await sut.ImportDataFile.Execute().ToTask();

        picker.Filters.Should().ContainSingle(filter => filter.Extensions.Contains("*.json"));
        dialog.ShownTitles.Should().Contain("Import data file");
        dataEntry.Inputs.ToEntries().Should().BeEquivalentTo(imported.Inputs);
        dataEntry.Outputs.ToEntries().Should().BeEquivalentTo(imported.Outputs);
        repository.SavedCatalog.Should().BeEquivalentTo(imported);
    }

    [Fact]
    public async Task DataChanges_AfterInitialLoad_AreSavedAutomatically()
    {
        var repository = new FakeEntryCatalogRepository();
        var dataEntry = new DataEntryViewModel();
        var sut = CreateSut(dataEntry, repository, autosaveInterval: TimeSpan.Zero);

        await sut.LoadSavedData.Execute().ToTask();

        dataEntry.Inputs.Load([new Entry(new DateTime(2026, 1, 10), 2, 100m)]);

        var saved = await repository.NextSave.Task.WaitAsync(TimeSpan.FromSeconds(1));
        saved.Should().BeEquivalentTo(new EntryCatalog(
            [new Entry(new DateTime(2026, 1, 10), 2, 100m)],
            []));
    }

    [Fact]
    public async Task UnlockSync_WhenRemoteCatalogWasDownloaded_ShouldReloadCatalogIntoViewModel()
    {
        var downloaded = new EntryCatalog(
            [new Entry(new DateTime(2026, 5, 9), 2, 100m)],
            []);
        var repository = new FakeEntryCatalogRepository();
        var sync = new FakeFifoSyncService
        {
            OnUnlock = () => repository.CatalogToLoad = downloaded,
        };
        var dataEntry = new DataEntryViewModel();
        var sut = CreateSut(dataEntry, repository, syncService: sync);

        await sut.UnlockSync.Execute().ToTask();

        dataEntry.Inputs.ToEntries().Should().BeEquivalentTo(downloaded.Inputs);
    }

    [Fact]
    public void SyncActions_WhenSyncIsNotReady_ShouldRemainDisabled()
    {
        var sync = new FakeFifoSyncService(
            new FifoSyncStatus(true, false, false, false, false, false, null, "Sync is initializing.", null));
        var sut = CreateSut(new DataEntryViewModel(), new FakeEntryCatalogRepository(), syncService: sync);

        sut.CanCreateSyncIdentity.Should().BeFalse();
        sut.CanImportSyncIdentity.Should().BeFalse();
        sut.CanUnlockSync.Should().BeFalse();
        sut.CanExportSyncIdentity.Should().BeFalse();
        sut.CanSyncNow.Should().BeFalse();
        sut.CanDisconnectSync.Should().BeFalse();
        sut.HasSyncConflict.Should().BeFalse();

        sync.SetStatus(new FifoSyncStatus(true, true, false, false, false, false, null, "Sync is not configured.", null));

        sut.CanCreateSyncIdentity.Should().BeTrue();
        sut.CanImportSyncIdentity.Should().BeTrue();
    }

    private static SettingsViewModel CreateSut(
        DataEntryViewModel dataEntry,
        FakeEntryCatalogRepository repository,
        FakeDialog? dialog = null,
        FakeFileSystemPicker? picker = null,
        IFifoSyncService? syncService = null,
        TimeSpan? autosaveInterval = null)
    {
        return new SettingsViewModel(
            dataEntry,
            repository,
            syncService ?? new FakeFifoSyncService(),
            dialog ?? new FakeDialog(),
            picker ?? new FakeFileSystemPicker(),
            new SilentNotificationService(),
            autosaveInterval);
    }

    private sealed class FakeEntryCatalogRepository : IEntryCatalogRepository
    {
        public EntryCatalog CatalogToLoad { get; set; } = new([], []);
        public EntryCatalog CatalogToImport { get; init; } = new([], []);
        public EntryCatalog? SavedCatalog { get; private set; }
        public TaskCompletionSource<EntryCatalog> NextSave { get; } = new();

        public Task<Result<EntryCatalog>> Load() => Task.FromResult(Result.Success(CatalogToLoad));

        public Task<Result<EntryCatalog>> Load(INamedByteSource source)
        {
            return Task.FromResult(Result.Success(CatalogToImport));
        }

        public Task<Result> Save(EntryCatalog catalog)
        {
            SavedCatalog = catalog;
            NextSave.TrySetResult(catalog);
            return Task.FromResult(Result.Success());
        }
    }

    private sealed class FakeFifoSyncService : IFifoSyncService
    {
        private readonly BehaviorSubject<FifoSyncStatus> status;
        public Action? OnUnlock { get; init; }

        public FakeFifoSyncService()
            : this(new FifoSyncStatus(true, true, false, false, false, false, null, "Sync is not configured.", null))
        {
        }

        public FakeFifoSyncService(FifoSyncStatus initialStatus)
        {
            status = new BehaviorSubject<FifoSyncStatus>(initialStatus);
        }

        public FifoSyncStatus Status => status.Value;

        public IObservable<FifoSyncStatus> StatusChanged => status;

        public void SetStatus(FifoSyncStatus value)
        {
            status.OnNext(value);
        }

        public Task<Result> Initialize(CancellationToken cancellationToken = default) => Task.FromResult(Result.Success());

        public Task<Result> CreateIdentity(string password, EntryCatalog localCatalog, CancellationToken cancellationToken = default) => Task.FromResult(Result.Success());

        public Task<Result> Unlock(string password, EntryCatalog localCatalog, CancellationToken cancellationToken = default)
        {
            OnUnlock?.Invoke();
            return Task.FromResult(Result.Success());
        }

        public Task<Result> ImportIdentity(byte[] identityExport, string password, EntryCatalog localCatalog, CancellationToken cancellationToken = default) => Task.FromResult(Result.Success());

        public Task<Result<byte[]>> ExportIdentity(string password, CancellationToken cancellationToken = default) => Task.FromResult(Result.Success(Array.Empty<byte>()));

        public Task<Result> SyncNow(EntryCatalog localCatalog, CancellationToken cancellationToken = default) => Task.FromResult(Result.Success());

        public Task<Result> SaveLocalChange(EntryCatalog catalog, CancellationToken cancellationToken = default) => Task.FromResult(Result.Success());

        public Task<Result<Maybe<EntryCatalog>>> ResolveConflict(FifoSyncConflictChoice choice, EntryCatalog localCatalog, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result.Success(Maybe<EntryCatalog>.None));
        }

        public Task<Result> Disconnect(CancellationToken cancellationToken = default) => Task.FromResult(Result.Success());
    }

    private sealed class FakeFileSystemPicker : IFileSystemPicker
    {
        public INamedByteSource? FileToOpen { get; init; }
        public List<FileTypeFilter> Filters { get; } = [];

        public Task<Result<IEnumerable<INamedByteSource>>> PickForOpenMultiple(params FileTypeFilter[] filters)
        {
            throw new NotSupportedException();
        }

        public Task<Result<Maybe<INamedByteSource>>> PickForOpen(params FileTypeFilter[] filters)
        {
            Filters.AddRange(filters);
            var file = FileToOpen is null ? Maybe<INamedByteSource>.None : Maybe.From(FileToOpen);
            return Task.FromResult(Result.Success(file));
        }

        public Task<Maybe<IMutableFile>> PickForSave(string desiredName, Maybe<string> defaultExtension, params FileTypeFilter[] filters)
        {
            throw new NotSupportedException();
        }

        public Task<Maybe<IMutableDirectory>> PickFolder()
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeDialog : IDialog
    {
        public List<string> ShownTitles { get; } = [];

        public Task<bool> Show<TViewModel>(
            Maybe<TViewModel> viewModel,
            Maybe<IObservable<string>> title,
            Func<Maybe<TViewModel>, ICloseable, IEnumerable<IOption>> optionsFactory,
            Maybe<object> icon = default,
            DialogTone tone = DialogTone.Neutral,
            DialogSize size = DialogSize.Auto)
        {
            title.Execute(observable => ShownTitles.Add(observable.Wait()));
            viewModel.Execute(model =>
            {
                if (model is PasswordPromptViewModel passwordPrompt)
                {
                    passwordPrompt.Password = "password";
                }
            });

            var closeable = new Closeable();
            var option = optionsFactory(viewModel, closeable).First(x => !x.IsCancel);
            option.Command.Execute(null);

            return Task.FromResult(closeable.WasClosed);
        }
    }

    private sealed class Closeable : ICloseable
    {
        public bool WasClosed { get; private set; }

        public void Close()
        {
            WasClosed = true;
        }

        public void Dismiss()
        {
            WasClosed = false;
        }
    }

    private sealed class SilentNotificationService : INotificationService
    {
        public Task Show(string message, Maybe<string> title) => Task.CompletedTask;
    }
}
