using System;
using System.Collections.Generic;
using System.Reactive;
using CSharpFunctionalExtensions;
using FIFOCalculator.Models;
using ReactiveUI;

namespace FIFOCalculator.ViewModels;

public interface ISimulationViewModel
{
    DateTimeOffset? From { get; set; }
    DateTimeOffset? To { get; set; }
    int? SelectedYear { get; set; }
    IReadOnlyList<int> AvailableYears { get; }
    IObservable<Balance> Simulation { get; }
    ReactiveCommand<Unit, Result<Balance>> Simulate { get; }
}