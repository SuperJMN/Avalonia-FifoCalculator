using CSharpFunctionalExtensions.FluentAssertions;
using FIFOCalculator.Engine;
using FluentAssertions.Extensions;

namespace TestProject1;

public class BalanceCalculatorTests
{
    [Fact]
    public void Full_scenario_with_real_data()
    {
        var inputs = new[]
        {
            new Entry(25.March(2022), 0.14495033m, 39668.76m),
            new Entry(25.April(2022), 0.16210374m, 35471.11m),
            new Entry(25.May(2022), 0.20720732m, 27749.98m),
            new Entry(24.June(2022), 0.29020626m, 19813.49m),
            new Entry(22.July(2022), 0.25218201m, 22764.88m),
            new Entry(25.August(2022), 0.26570275m, 21640.72m),
            new Entry(24.September(2022), 0.29456491m, 19520.32m),
            new Entry(25.October(2022), 0.2951428m, 19482.09m),
            new Entry(25.November(2022), 0.36511317m, 15748.54m),
            new Entry(21.December(2022), 0.3644m, 15779.36m),
        };

        var outputs = new[]
        {
            new Entry(25.June(2022), -0.2463889m, 19816.14m),
            new Entry(23.July(2022), -0.216239470m, 23138.11m),
            new Entry(25.August(2022), -0.17348623m, 21800.53m),
            new Entry(24.September(2022), -0.1942020m, 19800.20m),
            new Entry(25.October(2022), -0.2121600m, 20000.00m),
            new Entry(25.November(2022), -0.2585744m, 15882.00m),
            new Entry(22.December(2022), -0.23652997m, 15850.00m),
        };

        var sut = new BalanceCalculator();
        var result = sut.Calculate(inputs.Concat(outputs));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Balance_from_store_operations()
    {
        var store = new FifoStore();
        store.Buy(new Order(2, 50));
        store.Buy(new Order(2, 100));

        var balance = store.Sell(3, 25);
        balance.Should().SucceedWith(-125);
    }
}