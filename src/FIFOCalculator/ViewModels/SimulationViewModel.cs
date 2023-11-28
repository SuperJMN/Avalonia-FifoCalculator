using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using CSharpFunctionalExtensions;
using FIFOCalculator.Models;
using ReactiveUI;
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
            var calculator = new BalanceCalculator(new Store(Maybe.From(Log.Logger)), Maybe.From(Log.Logger));
            var calculateBalance = calculator.CalculateBalance(inputs().Concat(outputs().Select(entry => entry with { Units = -entry.Units })), From, To);
            return calculateBalance;
        });

        Simulation = Simulate.Successes();
        Simulate.HandleErrorsWith(notificationService);
    }

    [Reactive] public DateTimeOffset? From { get; set; }

    [Reactive] public DateTimeOffset? To { get; set; }

    public IObservable<Balance> Simulation { get; }

    public ReactiveCommand<Unit, Result<Balance>> Simulate { get; }
}