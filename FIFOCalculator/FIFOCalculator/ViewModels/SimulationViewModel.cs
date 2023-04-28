using CSharpFunctionalExtensions;
using ReactiveUI;
using System;
using System.Reactive;
using FIFOCalculator.Models;
using Zafiro.Core.Mixins;
using System.Collections.Generic;
using System.Linq;
using ReactiveUI.Fody.Helpers;

namespace FIFOCalculator.ViewModels;

public class SimulationViewModel : ViewModelBase, ISimulationViewModel
{
    public SimulationViewModel(IEnumerable<Entry> inputs, IEnumerable<Entry> outputs)
    {
        Simulate = ReactiveCommand.Create(() =>
        {
            var calculator = new BalanceCalculator(new Store(Maybe<ILogger>.None), Maybe<ILogger>.None);
            var calculateBalance = calculator.CalculateBalance(inputs.Concat(outputs.Select(entry => entry with { Units = -entry.Units })), From, To);
            return calculateBalance;
        });

        Simulation = Simulate.WhereSuccess();
    }

    [Reactive]
    public DateTimeOffset? From { get; set; }

    [Reactive]
    public DateTimeOffset? To { get; set; }

    public IObservable<decimal> Simulation { get; }

    public ReactiveCommand<Unit, Result<decimal>> Simulate { get; }
}