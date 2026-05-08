using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Text;
using CSharpFunctionalExtensions;
using FIFOCalculator.Models;
using FIFOCalculator.Persistence;
using FIFOCalculator.ViewModels;
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

    private static SettingsViewModel CreateSut(
        DataEntryViewModel dataEntry,
        FakeEntryCatalogRepository repository,
        FakeDialog? dialog = null,
        FakeFileSystemPicker? picker = null,
        TimeSpan? autosaveInterval = null)
    {
        return new SettingsViewModel(
            dataEntry,
            repository,
            dialog ?? new FakeDialog(),
            picker ?? new FakeFileSystemPicker(),
            new SilentNotificationService(),
            autosaveInterval);
    }

    private sealed class FakeEntryCatalogRepository : IEntryCatalogRepository
    {
        public EntryCatalog CatalogToLoad { get; init; } = new([], []);
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
