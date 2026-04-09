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
}
