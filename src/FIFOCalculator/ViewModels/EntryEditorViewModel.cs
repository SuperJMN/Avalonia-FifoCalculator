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
using ReactiveUI.Fody.Helpers;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;

namespace FIFOCalculator.ViewModels;

public class EntryEditorViewModel : ReactiveValidationObject
{
    private readonly ReadOnlyObservableCollection<EntryViewModel> entries;
    private readonly SourceList<EntryViewModel> source;
    private readonly ObservableAsPropertyHelper<decimal?> total;

    public EntryEditorViewModel(string title)
    {
        Title = title;
        source = new SourceList<EntryViewModel>();

        source
            .Connect()
            .Sort(SortExpressionComparer<EntryViewModel>.Ascending(x => x.When))
            .Bind(out entries)
            .Subscribe();

        this.ValidationRule(x => x.DateText, this.WhenAnyValue(model => model.DateText, s => DateTime.TryParse(s, out _)), "Invalid date string");
        this.ValidationRule(x => x.PricePerUnit, s => s is > 0, "Invalid price");
        this.ValidationRule(x => x.Units, s => s is > 0, "Invalid number");

        Add = ReactiveCommand.Create(() =>
        {
            source.Add(FromEntry(new Entry(DateTime.Parse(DateText!), Units!.Value, PricePerUnit!.Value)));
            DateText = "";
            PricePerUnit = null;
            Units = null;
        }, this.IsValid());

        DeleteSelected = ReactiveCommand.Create(() => source.Remove(SelectedEntry!), this.WhenAnyValue(x => x.SelectedEntry).Select(x => x != null));

        total = this.WhenAnyValue(x => x.PricePerUnit, x => x.Units, (a, b) => a * b).ToProperty(this, x => x.Total);
    }

    public ReadOnlyObservableCollection<EntryViewModel> Entries => entries;
    public string Title { get; set; }
    public ReactiveCommandBase<Unit, Unit> Add { get; set; }

    [Reactive] public EntryViewModel? SelectedEntry { get; set; }

    [Reactive] public ReactiveCommand<Unit, bool> DeleteSelected { get; set; }

    [Reactive] public string? DateText { get; set; }

    [Reactive] public decimal? PricePerUnit { get; set; }

    [Reactive] public decimal? Units { get; set; }

    public decimal? Total => total.Value;

    public IEnumerable<Entry> ToEntries()
    {
        return Entries.Select(ToEntry);
    }

    public void Load(IEnumerable<Entry> toLoad)
    {
        source.Edit(action =>
        {
            action.Clear();
            action.Add(toLoad.Select(FromEntry));
        });
    }

    private static Entry ToEntry(EntryViewModel vm)
    {
        return new Entry(vm.When, vm.Units, vm.PricePerUnit);
    }

    private static EntryViewModel FromEntry(Entry entry)
    {
        return new EntryViewModel(entry);
    }
}