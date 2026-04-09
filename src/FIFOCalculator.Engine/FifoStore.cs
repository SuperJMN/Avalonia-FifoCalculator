using CSharpFunctionalExtensions;

namespace FIFOCalculator.Engine;

public class FifoStore
{
    private readonly LinkedList<Order> orders = new();

    public decimal InventoryValue => orders.Sum(x => x.LineItemValue);
    public decimal Units => orders.Sum(x => x.Units);

    public void Buy(Order order)
    {
        orders.AddLast(order);
    }

    public Result<decimal> Sell(decimal unitsToSell, decimal sellPrice)
    {
        if (orders.Count == 0)
        {
            return Result.Failure<decimal>("Nothing to sell: store is empty");
        }

        if (unitsToSell > Units)
        {
            return Result.Failure<decimal>($"Not enough units to sell. Requested: {unitsToSell}, Available: {Units}");
        }

        var balance = 0m;
        var remaining = unitsToSell;

        while (remaining > 0)
        {
            var oldest = orders.First!.Value;
            orders.RemoveFirst();

            var taken = Math.Min(oldest.Units, remaining);
            var leftover = oldest.Units - taken;

            if (leftover > 0)
            {
                orders.AddFirst(oldest with { Units = leftover });
            }

            remaining -= taken;

            var costBasis = taken * oldest.Price;
            var saleProceeds = taken * sellPrice;
            balance += saleProceeds - costBasis;
        }

        return balance;
    }
}
