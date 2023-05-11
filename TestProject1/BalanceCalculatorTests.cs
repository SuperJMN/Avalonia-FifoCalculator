using CSharpFunctionalExtensions;
using CSharpFunctionalExtensions.FluentAssertions;
using FIFOCalculator.Models;
using FluentAssertions.Extensions;
using Serilog;
using Xunit.Abstractions;

namespace TestProject1;

public class BalanceCalculatorTests
{

    private readonly ILogger logger;

    public BalanceCalculatorTests(ITestOutputHelper output)
    {
        logger = new LoggerConfiguration()
            .WriteTo.TestOutput(output)
            .CreateLogger();
    }
    
    [Fact]
    public void Full()
    {
        var inputs = new[]
        {
            new Entry(25.March(2022), (decimal)0.14495033, (decimal)39668.76),
            new Entry(25.April(2022),(decimal) 0.16210374,(decimal) 35471.11),
            new Entry(25.May(2022), (decimal)0.20720732,(decimal) 27749.98),
            new Entry(24.June(2022), (decimal)0.29020626, (decimal)19813.49),
            new Entry(22.July(2022), (decimal)0.25218201, (decimal)22764.88),
            new Entry(25.August(2022), (decimal)0.26570275, (decimal)21640.72),
            new Entry(24.September(2022), (decimal)0.29456491, (decimal)19520.32),
            new Entry(25.October(2022), (decimal)0.2951428,(decimal) 19482.09),
            new Entry(25.November(2022), (decimal)0.36511317, (decimal)15748.54),
            new Entry(21.December(2022), (decimal)0.3644, (decimal)15779.36),
        };

        var outputs = new[]
        {
            new Entry(25.June(2022), (decimal) -0.2463889, (decimal)  19816.14),
            new Entry(23.July(2022),  (decimal)-0.216239470,  (decimal) 23138.11),
            new Entry(25.August(2022),  (decimal)-0.17348623,  (decimal) 21800.53),
            new Entry(24.September(2022), (decimal) -0.1942020, (decimal)  19800.20),
            new Entry(25.October(2022),  (decimal)-0.2121600, (decimal)  20000.00),
            new Entry(25.November(2022), (decimal) -0.2585744, (decimal)  15882.00),
            new Entry(22.December(2022), (decimal) -0.23652997, (decimal)  15850.00),
        };


        var store = new Store(logger.AsMaybe());
        var sut = new BalanceCalculator(store, logger.AsMaybe());
        var enumerable = inputs.Concat(outputs);
        var result = sut.CalculateBalance(enumerable);
        
        result.Should().SucceedWith(new Balance(0, 1));
    }

    [Fact]
    public void Balance()
    {
        var store = new Store(logger.AsMaybe());
        store.Buy(new StockPrice(2, 50));
        store.Buy(new StockPrice(2, 100));

        var balance = store.Sell(3, 25);
        balance.Should().SucceedWith(-125);
    }
}