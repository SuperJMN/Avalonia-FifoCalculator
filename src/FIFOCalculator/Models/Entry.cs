using System;

namespace FIFOCalculator.Models;

public record Entry(DateTime When, decimal Units, decimal PricePerUnit);