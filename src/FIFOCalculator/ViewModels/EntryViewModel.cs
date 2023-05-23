using System;
using FIFOCalculator.Models;

namespace FIFOCalculator.ViewModels;

public class EntryViewModel
{
    public Entry Entry { get; }

    public EntryViewModel(Entry entry)
    {
        Entry = entry;
        When = entry.When;
        Units = entry.Units;
        PricePerUnit = entry.PricePerUnit;
    }

    public DateTime When { get; init; }
    public decimal Units { get; init; }
    public decimal PricePerUnit { get; init; }
}