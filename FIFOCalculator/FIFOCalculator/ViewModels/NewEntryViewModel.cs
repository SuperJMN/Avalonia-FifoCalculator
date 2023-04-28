using System;
using ReactiveUI.Fody.Helpers;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;

namespace FIFOCalculator.ViewModels;

public class NewEntryViewModel : ReactiveValidationObject
{
    public NewEntryViewModel()
    {
        this.ValidationRule(x => x.Units, arg => arg.HasValue && arg.Value > 0, "Units should be greater than 0");
        this.ValidationRule(x => x.PricePerUnit, arg => arg.HasValue && arg.Value > 0, "Price per unit should be greater than 0");
    }

    [Reactive]
    public DateTime? When { get; set; }

    [Reactive]
    public decimal? Units { get; set; }

    [Reactive]
    public decimal? PricePerUnit { get; set; }
}