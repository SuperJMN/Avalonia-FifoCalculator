using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using FIFOCalculator.Models;
using ReactiveUI;
using Zafiro.Avalonia;
using Zafiro.Avalonia.Interfaces;
using Zafiro.Core.Mixins;
using Zafiro.FileSystem;

namespace FIFOCalculator.ViewModels;

public class MainViewModel : ReactiveObject
{
    public EntryEditorViewModel Inputs { get; set; }
    public EntryEditorViewModel Outputs { get; set; }

    public MainViewModel(IStorage storage, INotificationService notificationService)
    {
        Inputs = new EntryEditorViewModel("Inputs", Enumerable.Empty<Entry>());
        Outputs = new EntryEditorViewModel("Outputs", Enumerable.Empty<Entry>());
        Simulation = new SimulationViewModel(Inputs.Entries.Select(ToEntry), Outputs.Entries.Select(ToEntry));

        Open = ReactiveCommand.CreateFromObservable(() => storage.PickForOpen().SelectMany(m => m.Map(LoadFromFile)));
        Open.Values().WhereSuccess().Do(LoadCatalog).Subscribe();
        Save = ReactiveCommand.CreateFromObservable(() => storage.PickForSave("Accounts", "txt").SelectMany(async m => await m.Map(SaveToFile)));
        
        Open.Values().WhereFailure().Do(notificationService.ShowMessage).Subscribe();
        Save.Values().WhereFailure().Do(notificationService.ShowMessage).Subscribe();

        New = ReactiveCommand.Create(() =>
        {
            Inputs.Load(Enumerable.Empty<Entry>());
            Outputs.Load(Enumerable.Empty<Entry>());
        });
    }

    public ReactiveCommand<Unit, Unit> New { get; set; }

    private void LoadCatalog(EntryCatalog catalog)
    {
        Inputs.Load(catalog.Inputs);
        Outputs.Load(catalog.Outputs);
    }

    private static async Task<Result<EntryCatalog>> LoadFromFile(IStorable storable)
    {
        await using var s = await storable.OpenRead();
        return await EntryStore.Load(s);
    }

    private async Task<Result> SaveToFile(IStorable storable)
    {
        await using var s = await storable.OpenWrite();
        return await EntryStore.Save(s, new EntryCatalog(Inputs.ToEntries().ToList(), Outputs.ToEntries().ToList()));
    }

    public ReactiveCommand<Unit, Maybe<Result>> Save { get; }

    public ReactiveCommand<Unit, Maybe<Result<EntryCatalog>>> Open { get; }

    public SimulationViewModel Simulation { get; }

    private static Entry ToEntry(EntryViewModel x)
    {
        return new Entry(x.When, x.Units, x.PricePerUnit);
    }
}