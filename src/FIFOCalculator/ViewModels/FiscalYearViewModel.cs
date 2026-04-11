using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using CSharpFunctionalExtensions;
using FIFOCalculator.Models;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI;
using Zafiro.UI.Shell.Utils;

namespace FIFOCalculator.ViewModels;

[Section(icon: "fa-calendar", sortIndex: 2, FriendlyName = "Fiscal Year")]
public partial class FiscalYearViewModel : ViewModelBase
{
    private readonly ObservableAsPropertyHelper<IReadOnlyList<Entry>> entries;
    private readonly ObservableAsPropertyHelper<IReadOnlyList<int>> availableYears;

    public FiscalYearViewModel(DataEntryViewModel dataEntry, INotificationService notificationService)
    {
        var inputs = dataEntry.Inputs;
        var outputs = dataEntry.Outputs;
        var entriesChanged = Observable.Merge(
                inputs.EntriesCollection.Select(_ => Unit.Default),
                outputs.EntriesCollection.Select(_ => Unit.Default))
            .StartWith(Unit.Default);

        entries = entriesChanged
            .Select(_ => inputs.ToEntries()
                .Concat(outputs.ToEntries().Select(entry => entry with { Units = -entry.Units }))
                .OrderBy(entry => entry.When)
                .ToList())
            .Select(list => (IReadOnlyList<Entry>)list)
            .ToProperty(this, model => model.Entries, new List<Entry>());

        availableYears = entriesChanged
            .Select(_ => (IReadOnlyList<int>)inputs.ToEntries()
                .Concat(outputs.ToEntries())
                .Select(entry => entry.When.Year)
                .Distinct()
                .OrderBy(year => year)
                .ToList())
            .ToProperty(this, model => model.AvailableYears, new List<int>());

        Calculate = ReactiveCommand.Create(() =>
        {
            var calculator = new BalanceCalculator(new Store(Maybe.From(Log.Logger)), Maybe.From(Log.Logger));
            return calculator.CalculateForYear(Entries, SelectedYear!.Value);
        }, this.WhenAnyValue(x => x.SelectedYear).Select(y => y.HasValue));

        Result = Calculate.Successes();
        Calculate.HandleErrorsWith(notificationService);

        this.WhenAnyValue(model => model.SelectedYear)
            .Where(year => year.HasValue)
            .Select(_ => Unit.Default)
            .InvokeCommand(Calculate);
    }

    [Reactive] private int? _selectedYear;

    public IObservable<Balance> Result { get; }

    public ReactiveCommand<Unit, Result<Balance>> Calculate { get; }

    public IReadOnlyList<int> AvailableYears => availableYears.Value;

    private IReadOnlyList<Entry> Entries => entries.Value;
}
