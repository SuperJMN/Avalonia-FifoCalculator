namespace FIFOCalculator.Models;

public record StockPrice(decimal Units, decimal Price)
{
    public decimal Value => Units * Price;
}