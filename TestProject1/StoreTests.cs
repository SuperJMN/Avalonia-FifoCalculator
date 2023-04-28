using CSharpFunctionalExtensions;
using FIFOCalculator.Models;
using FluentAssertions;
using CSharpFunctionalExtensions.FluentAssertions;
using Serilog;
using Xunit.Abstractions;

namespace TestProject1;

public class StoreTests
{
    private readonly Serilog.ILogger logger;

    public StoreTests(ITestOutputHelper output)
    {
        logger = Log.Logger = new LoggerConfiguration()
            .WriteTo.TestOutput(output)
            .CreateLogger();
    }

    [Fact]
    public void Empty_queue_has_no_value()
    {
        var sut = new Store(logger.AsMaybe());
        sut.Value.Should().Be(0);
    }

    [Fact]
    public void Simple_sell()
    {
        var sut = new Store(logger.AsMaybe());
        sut.Buy(new StockPrice(1, 2));

        var balance = sut.Sell(1, 4);

        balance.Should().SucceedWith(2);
    }

    [Fact]
    public void Simple_sell_fifo()
    {
        var sut = new Store(logger.AsMaybe());

        sut.Buy(new StockPrice(1, 2));
        sut.Buy(new StockPrice(1, 3));

        var balance = sut.Sell(1, 4);

        balance.Should().SucceedWith(2);
    }

    [Fact]
    public void Multiple_drop()
    {
        var sut = new Store(logger.AsMaybe());

        sut.Buy(new StockPrice(1, 2));
        sut.Buy(new StockPrice(2, 3));
        sut.Buy(new StockPrice(3, 4));

        var balance = sut.Sell(3, 1);

        balance.Should().SucceedWith(-5);
    }

    [Fact]
    public void Empty()
    {
        var sut = new Store(logger.AsMaybe());

        var balance = sut.Sell(1, 4);

        balance.Should().Fail("Esto tendrá que fallar");
    }
}