using System;
using System.Reactive;
using CSharpFunctionalExtensions;
using ReactiveUI;

namespace FIFOCalculator.ViewModels;

public interface ISimulationViewModel
{
    DateTimeOffset? From { get; set; }
    DateTimeOffset? To { get; set; }
    IObservable<decimal> Simulation { get; }
    ReactiveCommand<Unit, Result<decimal>> Simulate { get; }
}