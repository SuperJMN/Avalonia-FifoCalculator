namespace FIFOCalculator.Engine;

public record Entry(DateTimeOffset When, decimal Units, decimal PricePerUnit);
