namespace FIFOCalculator.Models;

public record Order(decimal Units, decimal Price)
{
    public decimal LineItemValue => Units * Price;
}