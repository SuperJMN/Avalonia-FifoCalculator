using System;
using System.Reactive.Linq;
using FIFOCalculator.Models;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;

namespace FIFOCalculator.ViewModels;

public partial class EntryViewModel : ReactiveValidationObject
{
    private readonly ObservableAsPropertyHelper<decimal> total;

    public EntryViewModel(Entry entry)
    {
        When = entry.When;
        Units = entry.Units;
        PricePerUnit = entry.PricePerUnit;

        this.ValidationRule(x => x.When, date => date.HasValue, "Invalid date");
        this.ValidationRule(x => x.Units, s => s > 0, "Invalid number");
        this.ValidationRule(x => x.PricePerUnit, s => s > 0, "Invalid price");

        total = this.WhenAnyValue(x => x.Units, x => x.PricePerUnit, (u, p) => u * p)
            .ToProperty(this, x => x.Total);
    }

    [Reactive] private DateTime? _when;
    [Reactive] private decimal _units;
    [Reactive] private decimal _pricePerUnit;
    public decimal Total => total.Value;

    public Entry ToEntry() => new(When ?? DateTime.MinValue, Units, PricePerUnit);
}