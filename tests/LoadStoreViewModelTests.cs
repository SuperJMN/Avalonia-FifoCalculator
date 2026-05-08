using System.Reactive.Threading.Tasks;
using CSharpFunctionalExtensions;
using FIFOCalculator.Models;
using FIFOCalculator.Persistence;
using FIFOCalculator.ViewModels;
using Zafiro.UI;

namespace TestProject1;

public sealed class LoadStoreViewModelTests
{
    [Fact]
    public async Task Open_LoadsCatalogFromRepository()
    {
        var catalog = new EntryCatalog(
            [new Entry(new DateTime(2026, 1, 10), 2, 100m)],
            [new Entry(new DateTime(2026, 2, 15), 1, 150m)]);
        var repository = new FakeEntryCatalogRepository { CatalogToLoad = catalog };
        var dataEntry = new DataEntryViewModel(new SilentNotificationService(), repository);

        await dataEntry.LoadStoreViewModel.Open.Execute().ToTask();

        dataEntry.Inputs.ToEntries().Should().BeEquivalentTo(catalog.Inputs);
        dataEntry.Outputs.ToEntries().Should().BeEquivalentTo(catalog.Outputs);
    }

    [Fact]
    public async Task Save_WritesCurrentCatalogToRepository()
    {
        var repository = new FakeEntryCatalogRepository();
        var dataEntry = new DataEntryViewModel(new SilentNotificationService(), repository);
        dataEntry.Inputs.Load([new Entry(new DateTime(2026, 1, 10), 2, 100m)]);
        dataEntry.Outputs.Load([new Entry(new DateTime(2026, 2, 15), 1, 150m)]);

        await dataEntry.LoadStoreViewModel.Save.Execute().ToTask();

        repository.SavedCatalog.Should().BeEquivalentTo(new EntryCatalog(
            [new Entry(new DateTime(2026, 1, 10), 2, 100m)],
            [new Entry(new DateTime(2026, 2, 15), 1, 150m)]));
    }

    private sealed class FakeEntryCatalogRepository : IEntryCatalogRepository
    {
        public EntryCatalog CatalogToLoad { get; init; } = new([], []);
        public EntryCatalog? SavedCatalog { get; private set; }

        public Task<Result<EntryCatalog>> Load() => Task.FromResult(Result.Success(CatalogToLoad));

        public Task<Result> Save(EntryCatalog catalog)
        {
            SavedCatalog = catalog;
            return Task.FromResult(Result.Success());
        }
    }

    private sealed class SilentNotificationService : INotificationService
    {
        public Task Show(string message, Maybe<string> title) => Task.CompletedTask;
    }
}
