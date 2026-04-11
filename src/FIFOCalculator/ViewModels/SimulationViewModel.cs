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

[Section(icon: "fa-chart-line", sortIndex: 1, FriendlyName = "Simulate")]
public partial class SimulationViewModel : ViewModelBase, ISimulationViewModel
{
    private readonly ObservableAsPropertyHelper<IReadOnlyList<Entry>> entries;
    private readonly ObservableAsPropertyHelper<IReadOnlyList<int>> availableYears;

    public SimulationViewModel(DataEntryViewModel dataEntry, INotificationService notificationService)
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

        Simulate = ReactiveCommand.Create(() =>
        {
            var calculator = new BalanceCalculator(new Store(Maybe.From(Log.Logger)), Maybe.From(Log.Logger));
            var calculateBalance = calculator.CalculateBalance(Entries, From, To);
            return calculateBalance;
        });

        Simulation = Simulate.Successes();
        Simulate.HandleErrorsWith(notificationService);

        this.WhenAnyValue(model => model.SelectedYear)
            .Select(year => year.HasValue ? new DateTimeOffset?(new DateTime(year.Value, 1, 1)) : null)
            .BindTo(this, model => model.From);

        this.WhenAnyValue(model => model.SelectedYear)
            .Select(year => year.HasValue ? new DateTimeOffset?(new DateTime(year.Value + 1, 1, 1)) : null)
            .BindTo(this, model => model.To);

        this.WhenAnyValue(model => model.SelectedYear)
            .Where(year => year.HasValue)
            .Select(_ => Unit.Default)
            .InvokeCommand(Simulate);
    }

    [Reactive] private DateTimeOffset? _from;
    [Reactive] private DateTimeOffset? _to;
    [Reactive] private int? _selectedYear;

    public IObservable<Balance> Simulation { get; }

    public ReactiveCommand<Unit, Result<Balance>> Simulate { get; }

    public IReadOnlyList<int> AvailableYears => availableYears.Value;

    private IReadOnlyList<Entry> Entries => entries.Value;
}
