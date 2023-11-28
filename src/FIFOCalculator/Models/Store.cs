using System.Collections.Generic;
using System.Linq;
using CSharpFunctionalExtensions;
using static System.Decimal;

namespace FIFOCalculator.Models;

public class Store
{
    private readonly Maybe<ILogger> logger;
    private readonly LinkedList<Order> orderList = new();
    public decimal InventoryValue => orderList.Sum(x => x.LineItemValue);
    public decimal Units => orderList.Sum(x => x.Units);

    public Store(Maybe<ILogger> logger)
    {
        this.logger = logger;
    }

    public void Buy(Order order)
    {
        orderList.AddLast(order);
    }

    public Result<decimal> Sell(decimal unitsToTake, decimal price)
    {
        if (orderList.Count == 0)
        {
            return Result.Failure<decimal>("Nothing to shell");
        }

        var balance = new decimal();

        var remainingUnitsToTake = unitsToTake;

        while (remainingUnitsToTake > 0)
        {
            var current = orderList.First();
            orderList.RemoveFirst();
            logger.Execute(x => x.Information(" - Segmento actual: Quedan {Units} unidades a {Price:C}", current.Units, current.Price));
            var unitsTaken = Min(current.Units, remainingUnitsToTake);
            logger.Execute(x => x.Information(" - Hemos podido tomar {Taken} unidades del segmento", unitsTaken));

            var consumed = current with { Units = current.Units - unitsTaken };
            if (consumed.Units != 0)
            {
                orderList.AddFirst(consumed);
            }

            remainingUnitsToTake -= unitsTaken;

            var previousPrice = unitsTaken * current.Price;
            var newPrice = unitsTaken * price;

            var partialBalance = newPrice - previousPrice;

            balance += partialBalance;
            logger.Execute(x =>
            {
                x.Information(" - Precio de compra: {Previous:C}. Precio de venta: {Current:C} ({Proportion:P}). Balance parcial: {Balance:C}", previousPrice, newPrice, newPrice/previousPrice, partialBalance);
            });

            logger.Execute(x =>
            {
                if (remainingUnitsToTake > 0)
                {
                    x.Information(" - Quedan {Remaining} unidades por vender en el siguiente segmento de la cola", remainingUnitsToTake);
                }
            });
        }

        return balance;
    }
}