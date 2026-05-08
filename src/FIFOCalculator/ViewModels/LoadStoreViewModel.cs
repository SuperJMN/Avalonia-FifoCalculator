using System.Linq;
using System.Reactive;
using CSharpFunctionalExtensions;
using FIFOCalculator.Models;
using FIFOCalculator.Persistence;
using ReactiveUI;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI;

namespace FIFOCalculator.ViewModels;

public class LoadStoreViewModel : ViewModelBase
{
    private readonly DataEntryViewModel dataEntryViewModel;
    public ReactiveCommand<Unit, Unit> New { get; set; }

    public ReactiveCommand<Unit, Result> Open { get; }

    public ReactiveCommand<Unit, Result> Save { get; }

    public LoadStoreViewModel(DataEntryViewModel dataEntryViewModel, IEntryCatalogRepository repository, INotificationService notificationService)
    {
        this.dataEntryViewModel = dataEntryViewModel;

        Open = ReactiveCommand.CreateFromTask(async () =>
        {
            var result = await repository.Load();
            if (result.IsFailure)
            {
                return Result.Failure(result.Error);
            }

            LoadCatalog(result.Value);
            return Result.Success();
        });
        Open.HandleErrorsWith(notificationService);

        Save = ReactiveCommand.CreateFromTask(async () =>
        {
            return await repository.Save(ToCatalog());
        });
        Save.HandleErrorsWith(notificationService);

        New = ReactiveCommand.Create(() =>
        {
            dataEntryViewModel.Inputs.Load(Enumerable.Empty<Entry>());
            dataEntryViewModel.Outputs.Load(Enumerable.Empty<Entry>());
        });
    }

    private void LoadCatalog(EntryCatalog catalog)
    {
        dataEntryViewModel.Inputs.Load(catalog.Inputs);
        dataEntryViewModel.Outputs.Load(catalog.Outputs);
    }

    private EntryCatalog ToCatalog()
    {
        return new EntryCatalog(dataEntryViewModel.Inputs.ToEntries().ToList(), dataEntryViewModel.Outputs.ToEntries().ToList());
    }
}
