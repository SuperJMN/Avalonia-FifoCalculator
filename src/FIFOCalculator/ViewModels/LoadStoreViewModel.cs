using System;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using FIFOCalculator.Models;
using ReactiveUI;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.FileSystem;
using Zafiro.UI;

namespace FIFOCalculator.ViewModels;

public class LoadStoreViewModel : ViewModelBase
{
    private readonly DataEntryViewModel dataEntryViewModel;
    public ReactiveCommand<Unit, Unit> New { get; set; }

    public ReactiveCommand<Unit, Maybe<Result>> Save { get; }

    public ReactiveCommand<Unit, Maybe<Result<EntryCatalog>>> Open { get; }

    public LoadStoreViewModel(DataEntryViewModel dataEntryViewModel, IFilePicker storage, INotificationService notificationService)
    {
        this.dataEntryViewModel = dataEntryViewModel;

        Open = ReactiveCommand.CreateFromObservable(() => storage.PickForOpen().SelectMany(m => m.Map(LoadFromFile)));
        Open.Values().Successes().Do(LoadCatalog).Subscribe();
        Save = ReactiveCommand.CreateFromObservable(() => storage.PickForSave("Accounts", "txt").SelectMany(async m => await m.Map(SaveToFile)));

        Open.Values().HandleErrorsWith(notificationService);
        Save.Values().HandleErrorsWith(notificationService);

        New = ReactiveCommand.Create(() =>
        {
            dataEntryViewModel.Inputs.Load(Enumerable.Empty<Entry>());
            dataEntryViewModel.Outputs.Load(Enumerable.Empty<Entry>());
        });
    }

    private static Task<Result<EntryCatalog>> LoadFromFile(IZafiroFile file)
    {
        return file.GetContents().Bind(EntryStore.Load);
    }

    private void LoadCatalog(EntryCatalog catalog)
    {
        dataEntryViewModel.Inputs.Load(catalog.Inputs);
        dataEntryViewModel.Outputs.Load(catalog.Outputs);
    }

    private Task<Result> SaveToFile(IZafiroFile storable)
    {
        return ToStream()
            .Bind(async stream =>
            {
                using (stream)
                {
                    return await storable.SetContents(stream);
                }
            });
    }

    private async Task<Result<Stream>> ToStream()
    {
        var memoryStream = new MemoryStream();
        var entryCatalog = new EntryCatalog(dataEntryViewModel.Inputs.ToEntries().ToList(), dataEntryViewModel.Outputs.ToEntries().ToList());
        await EntryStore.Save(memoryStream, entryCatalog);
        memoryStream.Position = 0;
        return memoryStream;
    }
}