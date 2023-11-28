using CSharpFunctionalExtensions;
using ReactiveUI;
using System;
using System.Reactive;
using FIFOCalculator.Models;
using System.Collections.Generic;
using System.Linq;
using ReactiveUI.Fody.Helpers;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI;

namespace FIFOCalculator.ViewModels;

public class SimulationViewModel : ViewModelBase, ISimulationViewModel
{
    public SimulationViewModel(Func<IEnumerable<Entry>> inputs, Func<IEnumerable<Entry>> outputs, INotificationService notificationService)
    {
        Simulate = ReactiveCommand.Create(() =>
        {
            var calculator = new BalanceCalculator(new Store(Maybe<ILogger>.None), Maybe<ILogger>.None);
            var calculateBalance = calculator.CalculateBalance(inputs().Concat(outputs().Select(entry => entry with { Units = -entry.Units })), From, To);
            return calculateBalance;
        });

        Simulation = Simulate.Successes();
        Simulate.HandleErrorsWith(notificationService);
    }

    [Reactive]
    public DateTimeOffset? From { get; set; }

    [Reactive]
    public DateTimeOffset? To { get; set; }

    public IObservable<Balance> Simulation { get; }

    public ReactiveCommand<Unit, Result<Balance>> Simulate { get; }
} 