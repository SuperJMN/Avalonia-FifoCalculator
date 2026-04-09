using CSharpFunctionalExtensions.FluentAssertions;
using FIFOCalculator.Engine;
using FluentAssertions.Extensions;

namespace TestProject1;

public class BalanceCalculatorFullTests
{
    [Fact]
    public void Simple_buy_and_sell_at_profit()
    {
        var entries = new[]
        {
            new Entry(1.January(2023), 10, 100),  // buy 10 at 100
            new Entry(1.February(2023), -5, 150),  // sell 5 at 150
        };

        var sut = new BalanceCalculator();
        var result = sut.Calculate(entries);

        result.IsSuccess.Should().BeTrue();
        result.Value.GainOrLoss.Should().Be(250m); // 5*(150-100)
        result.Value.RemainingInventoryValue.Should().Be(500m); // 5*100
    }

    [Fact]
    public void Simple_buy_and_sell_at_loss()
    {
        var entries = new[]
        {
            new Entry(1.January(2023), 10, 100),
            new Entry(1.February(2023), -10, 80),
        };

        var sut = new BalanceCalculator();
        var result = sut.Calculate(entries);

        result.IsSuccess.Should().BeTrue();
        result.Value.GainOrLoss.Should().Be(-200m); // 10*(80-100)
        result.Value.RemainingInventoryValue.Should().Be(0m);
    }

    [Fact]
    public void Entries_are_sorted_by_date()
    {
        // Entries given out of order — should still work correctly
        var entries = new[]
        {
            new Entry(1.March(2023), -1, 200),    // sell (given first but happens last)
            new Entry(1.January(2023), 5, 100),    // buy (happens first)
        };

        var sut = new BalanceCalculator();
        var result = sut.Calculate(entries);

        result.IsSuccess.Should().BeTrue();
        result.Value.GainOrLoss.Should().Be(100m); // 1*(200-100)
        result.Value.RemainingInventoryValue.Should().Be(400m);
    }

    [Fact]
    public void Date_filter_from()
    {
        var entries = new[]
        {
            new Entry(1.January(2023), 10, 50),   // buy — before filter
            new Entry(1.March(2023), -5, 80),      // sell — within filter
        };

        var sut = new BalanceCalculator();
        var result = sut.Calculate(entries, from: 1.February(2023));

        // Only the sell is in range, but the buy before the range is excluded,
        // so the sell should fail because the store is empty
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Date_filter_to()
    {
        var entries = new[]
        {
            new Entry(1.January(2023), 10, 50),    // buy — within filter
            new Entry(1.March(2023), -5, 80),       // sell — after filter
        };

        var sut = new BalanceCalculator();
        var result = sut.Calculate(entries, to: 1.February(2023));

        // Only buy is processed; no sells
        result.IsSuccess.Should().BeTrue();
        result.Value.GainOrLoss.Should().Be(0m);
        result.Value.RemainingInventoryValue.Should().Be(500m);
    }

    [Fact]
    public void Sell_without_prior_buy_fails()
    {
        var entries = new[]
        {
            new Entry(1.January(2023), -5, 100),
        };

        var sut = new BalanceCalculator();
        var result = sut.Calculate(entries);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Sell_more_than_bought_fails()
    {
        var entries = new[]
        {
            new Entry(1.January(2023), 2, 100),
            new Entry(1.February(2023), -5, 150),
        };

        var sut = new BalanceCalculator();
        var result = sut.Calculate(entries);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Empty_entries_returns_zero_balance()
    {
        var sut = new BalanceCalculator();
        var result = sut.Calculate(Enumerable.Empty<Entry>());

        result.IsSuccess.Should().BeTrue();
        result.Value.GainOrLoss.Should().Be(0m);
        result.Value.RemainingInventoryValue.Should().Be(0m);
    }

    [Fact]
    public void Only_buys_returns_zero_gain()
    {
        var entries = new[]
        {
            new Entry(1.January(2023), 5, 100),
            new Entry(1.February(2023), 3, 200),
        };

        var sut = new BalanceCalculator();
        var result = sut.Calculate(entries);

        result.IsSuccess.Should().BeTrue();
        result.Value.GainOrLoss.Should().Be(0m);
        result.Value.RemainingInventoryValue.Should().Be(5 * 100 + 3 * 200);
    }

    [Fact]
    public void Fifo_applied_across_multiple_sells()
    {
        var entries = new[]
        {
            new Entry(1.January(2023), 3, 10),    // buy 3 at 10
            new Entry(1.February(2023), 2, 20),   // buy 2 at 20
            new Entry(1.March(2023), -2, 15),      // sell 2 at 15 → takes from first buy
            new Entry(1.April(2023), -2, 25),      // sell 2 at 25 → takes 1 from first buy + 1 from second
        };

        var sut = new BalanceCalculator();
        var result = sut.Calculate(entries);

        // First sell: 2*(15-10) = 10
        // Second sell: 1*(25-10) + 1*(25-20) = 15 + 5 = 20
        result.IsSuccess.Should().BeTrue();
        result.Value.GainOrLoss.Should().Be(30m);
        result.Value.RemainingInventoryValue.Should().Be(20m); // 1 unit at 20
    }

    [Fact]
    public void Zero_units_entry_is_ignored()
    {
        var entries = new[]
        {
            new Entry(1.January(2023), 5, 100),
            new Entry(1.February(2023), 0, 999),  // zero units — should be ignored
            new Entry(1.March(2023), -5, 120),
        };

        var sut = new BalanceCalculator();
        var result = sut.Calculate(entries);

        result.IsSuccess.Should().BeTrue();
        result.Value.GainOrLoss.Should().Be(100m);
    }

    [Fact]
    public void Real_crypto_data_succeeds()
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
        var result = sut.Calculate(entries);

        result.IsSuccess.Should().BeTrue();
        // All sells happened at a loss (crypto winter 2022), so gain should be negative
        result.Value.GainOrLoss.Should().BeNegative();
    }

    [Fact]
    public void Calculator_is_stateless_and_reusable()
    {
        var sut = new BalanceCalculator();

        var result1 = sut.Calculate(new[]
        {
            new Entry(1.January(2023), 10, 100),
            new Entry(1.February(2023), -10, 120),
        });

        var result2 = sut.Calculate(new[]
        {
            new Entry(1.January(2024), 5, 200),
            new Entry(1.February(2024), -5, 180),
        });

        result1.Value.GainOrLoss.Should().Be(200m);
        result2.Value.GainOrLoss.Should().Be(-100m);
    }
}
