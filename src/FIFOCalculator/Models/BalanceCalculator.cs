using System;
using System.Collections.Generic;
using System.Linq;
using CSharpFunctionalExtensions;

namespace FIFOCalculator.Models;

public class BalanceCalculator
{
    private readonly Store store;
    private readonly Maybe<ILogger> logger;

    public BalanceCalculator(Store store, Maybe<ILogger> logger)
    {
        this.store = store;
        this.logger = logger;
    }

    public Result<Balance> CalculateBalance(IEnumerable<Entry> entries, DateTimeOffset? from = default, DateTimeOffset? to = default)
    {
        var balance = new decimal();

        from = @from ?? DateTimeOffset.MinValue;
        to = @to ?? DateTimeOffset.MaxValue;

        var byDate = entries.OrderBy(x => x.When).Where(x => x.When >= from && x.When < to);

        foreach (var operation in byDate)
        {
            if (operation.Units > 0)
            {
                logger.Execute(x => x.Information("{Date:d}: Compra de {Value:C} ({Units} a {Price:C})", operation.When, operation.Units * operation.PricePerUnit, operation.Units, operation.PricePerUnit));
                store.Buy(new Order(operation.Units, operation.PricePerUnit));
            }
            else
            {
                logger.Execute(x => x.Information("{Date:d}: Venta de {Value:C} ({Units} a {Price:C})", operation.When, -operation.Units * operation.PricePerUnit, -operation.Units, operation.PricePerUnit));
                var result = store.Sell(-operation.Units, operation.PricePerUnit);
                
                if (result.IsFailure)
                {
                    return Result.Failure<Balance>($"Couldn't calculate balance {result.Error}");
                }

                balance += result.Value;
                logger.Execute(x =>
                {
                    if (result.Value > 0)
                    {
                        x.Information("Hemos ganado {Gain:C}", result.Value);
                    }
                    else
                    {
                        x.Information("Hemos perdido {Loss:C}", -result.Value);
                    }
                });
            }
        }

        logger.Execute(x => x.Information("Balance total {Balance:C}", balance));

        return new Balance(balance, store.InventoryValue);
    }
}