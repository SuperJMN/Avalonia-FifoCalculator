namespace FIFOCalculator.Engine;

public record Order(decimal Units, decimal Price)
{
    public decimal LineItemValue => Units * Price;
}
