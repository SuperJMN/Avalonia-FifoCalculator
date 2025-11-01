using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using CSharpFunctionalExtensions;
using FIFOCalculator.Models;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI;

namespace FIFOCalculator.ViewModels;

public class SimulationViewModel : ViewModelBase, ISimulationViewModel
{
    private readonly ObservableAsPropertyHelper<IReadOnlyList<Entry>> entries;
    private readonly ObservableAsPropertyHelper<IReadOnlyList<int>> availableYears;

    public SimulationViewModel(EntryEditorViewModel inputs, EntryEditorViewModel outputs, INotificationService notificationService)
    {
        var inputEntries = inputs.EntriesCollection
            .Select(list => list.OrderBy(entry => entry.When).ToList())
            .Publish()
            .RefCount();

        var outputEntries = outputs.EntriesCollection
            .Select(list => list.OrderBy(entry => entry.When).ToList())
            .Publish()
            .RefCount();

        entries = inputEntries
            .CombineLatest(outputEntries, (ins, outs) => ins
                .Concat(outs.Select(entry => entry with { Units = -entry.Units }))
                .OrderBy(entry => entry.When)
                .ToList())
            .Select(list => (IReadOnlyList<Entry>)list)
            .ToProperty(this, model => model.Entries, new List<Entry>());

        availableYears = inputEntries
            .CombineLatest(outputEntries, (ins, outs) => ins.Concat(outs))
            .Select(list => (IReadOnlyList<int>)list
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

    [Reactive] public DateTimeOffset? From { get; set; }

    [Reactive] public DateTimeOffset? To { get; set; }

    [Reactive] public int? SelectedYear { get; set; }

    public IObservable<Balance> Simulation { get; }

    public ReactiveCommand<Unit, Result<Balance>> Simulate { get; }

    public IReadOnlyList<int> AvailableYears => availableYears.Value;

    private IReadOnlyList<Entry> Entries => entries.Value;
}