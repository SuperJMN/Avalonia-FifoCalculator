namespace FIFOCalculator.Models;

public record Currency(decimal Amount)
{
    public override string ToString()
    {
        return Amount.ToString("C");
    }
}