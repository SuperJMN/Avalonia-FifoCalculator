using CSharpFunctionalExtensions;

namespace FIFOCalculator.Engine;

public class BalanceCalculator
{
    public Result<Balance> Calculate(IEnumerable<Entry> entries, DateTimeOffset? from = null, DateTimeOffset? to = null)
    {
        var store = new FifoStore();
        var gainOrLoss = 0m;

        var start = from ?? DateTimeOffset.MinValue;
        var end = to ?? DateTimeOffset.MaxValue;

        var filtered = entries
            .OrderBy(e => e.When)
            .Where(e => e.When >= start && e.When < end);

        foreach (var entry in filtered)
        {
            if (entry.Units > 0)
            {
                store.Buy(new Order(entry.Units, entry.PricePerUnit));
            }
            else if (entry.Units < 0)
            {
                var sellUnits = -entry.Units;
                var result = store.Sell(sellUnits, entry.PricePerUnit);

                if (result.IsFailure)
                {
                    return Result.Failure<Balance>(result.Error);
                }

                gainOrLoss += result.Value;
            }
        }

        return new Balance(gainOrLoss, store.InventoryValue);
    }

    /// <summary>
    /// Calculates the FIFO balance for a specific fiscal year.
    /// Processes all entries from the beginning to build correct inventory state,
    /// but only accumulates gain/loss from sells within the target year.
    /// </summary>
    public Result<Balance> CalculateForYear(IEnumerable<Entry> entries, int year)
    {
        var store = new FifoStore();
        var gainOrLoss = 0m;

        var yearStart = new DateTimeOffset(new DateTime(year, 1, 1), TimeSpan.Zero);
        var yearEnd = new DateTimeOffset(new DateTime(year + 1, 1, 1), TimeSpan.Zero);

        var sorted = entries
            .OrderBy(e => e.When)
            .Where(e => e.When < yearEnd);

        foreach (var entry in sorted)
        {
            if (entry.Units > 0)
            {
                store.Buy(new Order(entry.Units, entry.PricePerUnit));
            }
            else if (entry.Units < 0)
            {
                var sellUnits = -entry.Units;
                var result = store.Sell(sellUnits, entry.PricePerUnit);

                if (result.IsFailure)
                {
                    return Result.Failure<Balance>(result.Error);
                }

                if (entry.When >= yearStart)
                {
                    gainOrLoss += result.Value;
                }
            }
        }

        return new Balance(gainOrLoss, store.InventoryValue);
    }
}
