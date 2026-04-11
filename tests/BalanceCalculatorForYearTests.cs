using CSharpFunctionalExtensions.FluentAssertions;
using FIFOCalculator.Engine;
using FluentAssertions.Extensions;

namespace TestProject1;

public class BalanceCalculatorForYearTests
{
    [Fact]
    public void Simple_buy_and_sell_same_year()
    {
        var entries = new[]
        {
            new Entry(1.January(2023), 10, 100),
            new Entry(1.June(2023), -5, 150),
        };

        var sut = new BalanceCalculator();
        var result = sut.CalculateForYear(entries, 2023);

        result.IsSuccess.Should().BeTrue();
        result.Value.GainOrLoss.Should().Be(250m);
        result.Value.RemainingInventoryValue.Should().Be(500m);
    }

    [Fact]
    public void Cross_year_inventory_carry_forward()
    {
        var entries = new[]
        {
            new Entry(1.March(2022), 10, 100),   // buy 10 at 100 in 2022
            new Entry(1.February(2023), -5, 150), // sell 5 at 150 in 2023
        };

        var sut = new BalanceCalculator();
        var result = sut.CalculateForYear(entries, 2023);

        result.IsSuccess.Should().BeTrue();
        result.Value.GainOrLoss.Should().Be(250m); // 5*(150-100), cost basis from 2022
        result.Value.RemainingInventoryValue.Should().Be(500m);
    }

    [Fact]
    public void Prior_year_sells_consume_inventory_but_not_counted_in_gain()
    {
        var entries = new[]
        {
            new Entry(1.January(2022), 10, 100),  // buy 10 at 100
            new Entry(1.June(2022), -3, 80),       // sell 3 at 80 in 2022 (loss, not counted)
            new Entry(1.March(2023), -4, 200),     // sell 4 at 200 in 2023
        };

        var sut = new BalanceCalculator();
        var result = sut.CalculateForYear(entries, 2023);

        result.IsSuccess.Should().BeTrue();
        result.Value.GainOrLoss.Should().Be(400m); // 4*(200-100)
        result.Value.RemainingInventoryValue.Should().Be(300m); // 3 remaining at 100
    }

    [Fact]
    public void No_sells_in_target_year_returns_zero_gain()
    {
        var entries = new[]
        {
            new Entry(1.January(2022), 10, 100),
            new Entry(1.June(2022), -5, 150),
        };

        var sut = new BalanceCalculator();
        var result = sut.CalculateForYear(entries, 2023);

        result.IsSuccess.Should().BeTrue();
        result.Value.GainOrLoss.Should().Be(0m);
        result.Value.RemainingInventoryValue.Should().Be(500m);
    }

    [Fact]
    public void Entries_after_target_year_are_excluded()
    {
        var entries = new[]
        {
            new Entry(1.January(2023), 10, 100),
            new Entry(1.June(2023), -5, 150),
            new Entry(1.March(2024), -5, 200), // should be excluded
        };

        var sut = new BalanceCalculator();
        var result = sut.CalculateForYear(entries, 2023);

        result.IsSuccess.Should().BeTrue();
        result.Value.GainOrLoss.Should().Be(250m);
        result.Value.RemainingInventoryValue.Should().Be(500m);
    }

    [Fact]
    public void Empty_entries_returns_zero_balance()
    {
        var sut = new BalanceCalculator();
        var result = sut.CalculateForYear(Enumerable.Empty<Entry>(), 2023);

        result.IsSuccess.Should().BeTrue();
        result.Value.GainOrLoss.Should().Be(0m);
        result.Value.RemainingInventoryValue.Should().Be(0m);
    }

    [Fact]
    public void Sell_more_than_available_fails()
    {
        var entries = new[]
        {
            new Entry(1.January(2023), 2, 100),
            new Entry(1.February(2023), -5, 150),
        };

        var sut = new BalanceCalculator();
        var result = sut.CalculateForYear(entries, 2023);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Fifo_order_across_years()
    {
        var entries = new[]
        {
            new Entry(1.January(2022), 5, 100),  // oldest: price 100
            new Entry(1.June(2022), 5, 200),      // newer: price 200
            new Entry(1.March(2023), -7, 150),    // sell 7: consumes 5@100 + 2@200
        };

        var sut = new BalanceCalculator();
        var result = sut.CalculateForYear(entries, 2023);

        result.IsSuccess.Should().BeTrue();
        // gain = 5*(150-100) + 2*(150-200) = 250 + (-100) = 150
        result.Value.GainOrLoss.Should().Be(150m);
        result.Value.RemainingInventoryValue.Should().Be(600m); // 3 units at 200
    }

    [Fact]
    public void Multiple_years_of_buys_and_sells()
    {
        var entries = new[]
        {
            new Entry(1.January(2021), 10, 50),
            new Entry(1.June(2021), -3, 60),       // 2021 sell
            new Entry(1.January(2022), 5, 80),
            new Entry(1.June(2022), -4, 70),        // 2022 sell
            new Entry(1.March(2023), -2, 100),      // 2023 sell
        };

        var sut = new BalanceCalculator();
        var result = sut.CalculateForYear(entries, 2023);

        result.IsSuccess.Should().BeTrue();
        // After 2021: bought 10@50, sold 3@60 → 7 remaining at 50
        // After 2022: bought 5@80, sold 4@70 → sells consume 4@50 (FIFO) → 3@50 + 5@80 remaining
        // 2023: sell 2@100 → consumes 2@50 (FIFO) → gain = 2*(100-50) = 100
        result.Value.GainOrLoss.Should().Be(100m);
        // Remaining: 1@50 + 5@80 = 50 + 400 = 450
        result.Value.RemainingInventoryValue.Should().Be(450m);
    }

    [Fact]
    public void Real_crypto_data_for_year_2022()
    {
        var entries = new[]
        {
            new Entry(25.March(2022), 0.14495033m, 39668.76m),
            new Entry(25.April(2022), 0.16210374m, 35471.11m),
            new Entry(25.May(2022), 0.20720732m, 27749.98m),
            new Entry(24.June(2022), 0.29020626m, 19813.49m),
            new Entry(25.June(2022), -0.2463889m, 19816.14m),
            new Entry(22.July(2022), 0.25218201m, 22764.88m),
            new Entry(23.July(2022), -0.216239470m, 23138.11m),
            new Entry(25.August(2022), 0.26570275m, 21640.72m),
            new Entry(25.August(2022), -0.17348623m, 21800.53m),
            new Entry(24.September(2022), 0.29456491m, 19520.32m),
            new Entry(24.September(2022), -0.1942020m, 19800.20m),
            new Entry(25.October(2022), 0.2951428m, 19482.09m),
            new Entry(25.October(2022), -0.2121600m, 20000.00m),
            new Entry(25.November(2022), 0.36511317m, 15748.54m),
            new Entry(25.November(2022), -0.2585744m, 15882.00m),
            new Entry(21.December(2022), 0.3644m, 15779.36m),
            new Entry(22.December(2022), -0.23652997m, 15850.00m),
        };

        var sut = new BalanceCalculator();
        var result = sut.CalculateForYear(entries, 2022);

        result.IsSuccess.Should().BeTrue();
        result.Value.GainOrLoss.Should().BeNegative();
    }
}
