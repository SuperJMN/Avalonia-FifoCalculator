using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using FIFOCalculator.Models;
using ReactiveUI;
using Zafiro.Avalonia.Interfaces;
using Zafiro.Core.Mixins;
using Zafiro.FileSystem;

namespace FIFOCalculator.ViewModels;

public class LoadStoreViewModel
{
    private readonly DataEntryViewModel dataEntryViewModel;
    public ReactiveCommand<Unit, Unit> New { get; set; }

    public ReactiveCommand<Unit, Maybe<Result>> Save { get; }

    public ReactiveCommand<Unit, Maybe<Result<EntryCatalog>>> Open { get; }

    public LoadStoreViewModel(DataEntryViewModel dataEntryViewModel, IStorage storage, INotificationService notificationService)
    {
        this.dataEntryViewModel = dataEntryViewModel;

        Open = ReactiveCommand.CreateFromObservable(() => storage.PickForOpen().SelectMany(m => m.Map(LoadFromFile)));
        Open.Values().WhereSuccess().Do(LoadCatalog).Subscribe();
        Save = ReactiveCommand.CreateFromObservable(() => storage.PickForSave("Accounts", "txt").SelectMany(async m => await m.Map(SaveToFile)));

        Open.Values().WhereFailure().Do(notificationService.ShowMessage).Subscribe();
        Save.Values().WhereFailure().Do(notificationService.ShowMessage).Subscribe();

        New = ReactiveCommand.Create(() =>
        {
            dataEntryViewModel.Inputs.Load(Enumerable.Empty<Entry>());
            dataEntryViewModel.Outputs.Load(Enumerable.Empty<Entry>());
        });
    }

    private static async Task<Result<EntryCatalog>> LoadFromFile(IStorable storable)
    {
        await using var s = await storable.OpenRead();
        return await EntryStore.Load(s);
    }

    private void LoadCatalog(EntryCatalog catalog)
    {
        dataEntryViewModel.Inputs.Load(catalog.Inputs);
        dataEntryViewModel.Outputs.Load(catalog.Outputs);
    }

    private async Task<Result> SaveToFile(IStorable storable)
    {
        await using var s = await storable.OpenWrite();
        return await EntryStore.Save(s, new EntryCatalog(dataEntryViewModel.Inputs.ToEntries().ToList(), dataEntryViewModel.Outputs.ToEntries().ToList()));
    }

    private void Reset()
    {
        dataEntryViewModel.Inputs.Load(new List<Entry>());
        dataEntryViewModel.Outputs.Load(new List<Entry>());
    }
}

public class DataEntryViewModel
{
    public DataEntryViewModel(INotificationService notificationService, IStorage storage)
    {
        Inputs = new EntryEditorViewModel("Inputs");
        Outputs = new EntryEditorViewModel("Outputs");
        LoadStoreViewModel = new LoadStoreViewModel(this, storage, notificationService);
    }

    public LoadStoreViewModel LoadStoreViewModel { get; set; }

    public EntryEditorViewModel Inputs { get; }
    public EntryEditorViewModel Outputs { get; }
}