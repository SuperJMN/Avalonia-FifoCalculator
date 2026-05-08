using System;
using System.Linq;
using System.Reactive.Linq;
using FIFOCalculator.Models;
using Zafiro.UI.Shell.Utils;

namespace FIFOCalculator.ViewModels;

[Section(icon: "fa-table", sortIndex: 0, FriendlyName = "Data Entry")]
public class DataEntryViewModel : ViewModelBase
{
    public DataEntryViewModel()
    {
        Inputs = new EntryEditorViewModel("Inputs");
        Outputs = new EntryEditorViewModel("Outputs");

        CatalogChanges = Inputs.EntriesCollection
            .StartWith(Inputs.ToEntries().ToList())
            .CombineLatest(
                Outputs.EntriesCollection.StartWith(Outputs.ToEntries().ToList()),
                (inputs, outputs) => new EntryCatalog(inputs.ToList(), outputs.ToList()))
            .Publish()
            .RefCount();
    }

    public EntryEditorViewModel Inputs { get; }
    public EntryEditorViewModel Outputs { get; }
    public IObservable<EntryCatalog> CatalogChanges { get; }

    public void LoadCatalog(EntryCatalog catalog)
    {
        Inputs.Load(catalog.Inputs);
        Outputs.Load(catalog.Outputs);
    }

    public EntryCatalog ToCatalog()
    {
        return new EntryCatalog(Inputs.ToEntries().ToList(), Outputs.ToEntries().ToList());
    }
}
