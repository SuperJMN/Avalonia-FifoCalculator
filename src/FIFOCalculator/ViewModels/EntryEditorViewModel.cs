using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using DynamicData;
using DynamicData.Binding;
using FIFOCalculator.Models;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace FIFOCalculator.ViewModels;

public partial class EntryEditorViewModel : ReactiveObject
{
    private readonly ReadOnlyObservableCollection<EntryViewModel> entries;
    private readonly SourceList<EntryViewModel> source;
    private readonly IObservable<IChangeSet<EntryViewModel>> connected;

    public EntryEditorViewModel(string title)
    {
        Title = title;
        source = new SourceList<EntryViewModel>();

        connected = source
            .Connect()
            .Publish()
            .RefCount();

        connected
            .AutoRefreshOnObservable(vm => vm.WhenAnyValue(x => x.When))
            .Sort(SortExpressionComparer<EntryViewModel>.Ascending(x => x.When ?? DateTime.MinValue))
            .Bind(out entries)
            .Subscribe();

        Add = ReactiveCommand.Create(() =>
        {
            source.Add(FromEntry(new Entry(DateTime.Today, 0, 0)));
        });

        DeleteSelected = ReactiveCommand.Create(() => source.Remove(SelectedEntry!), this.WhenAnyValue(x => x.SelectedEntry).Select(x => x != null));

        EntriesCollection = source.Connect()
            .AutoRefreshOnObservable(vm => vm.WhenAnyValue(x => x.When, x => x.Units, x => x.PricePerUnit))
            .Transform(x => x.ToEntry())
            .ToCollection();
    }

    [Reactive] private EntryViewModel? _selectedEntry;

    public ReadOnlyObservableCollection<EntryViewModel> Entries => entries;
    public string Title { get; set; }
    public ReactiveCommandBase<Unit, Unit> Add { get; set; }
    public ReactiveCommand<Unit, bool> DeleteSelected { get; set; }

    public IObservable<IReadOnlyCollection<Entry>> EntriesCollection { get; }

    public IEnumerable<Entry> ToEntries() => Entries.Select(ToEntry);

    public void Load(IEnumerable<Entry> toLoad)
    {
        source.Edit(action =>
        {
            action.Clear();
            action.Add(toLoad.Select(FromEntry));
        });
    }

    private static Entry ToEntry(EntryViewModel vm) => vm.ToEntry();

    private static EntryViewModel FromEntry(Entry entry) => new(entry);
}