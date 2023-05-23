using System;
using System.Reactive;
using CSharpFunctionalExtensions;
using FIFOCalculator.Models;
using ReactiveUI;

namespace FIFOCalculator.ViewModels;

public interface ISimulationViewModel
{
    DateTimeOffset? From { get; set; }
    DateTimeOffset? To { get; set; }
    IObservable<Balance> Simulation { get; }
    ReactiveCommand<Unit, Result<Balance>> Simulate { get; }
}