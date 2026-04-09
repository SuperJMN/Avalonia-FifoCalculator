using CSharpFunctionalExtensions.FluentAssertions;
using FIFOCalculator.Engine;

namespace TestProject1;

public class StoreTests
{
    [Fact]
    public void Empty_queue_has_no_value()
    {
        var sut = new FifoStore();
        sut.InventoryValue.Should().Be(0);
    }

    [Fact]
    public void Simple_sell()
    {
        var sut = new FifoStore();
        sut.Buy(new Order(1, 2));

        var balance = sut.Sell(1, 4);

        balance.Should().SucceedWith(2);
    }

    [Fact]
    public void Simple_sell_fifo()
    {
        var sut = new FifoStore();

        sut.Buy(new Order(1, 2));
        sut.Buy(new Order(1, 3));

        var balance = sut.Sell(1, 4);

        balance.Should().SucceedWith(2);
    }

    [Fact]
    public void Multiple_drop()
    {
        var sut = new FifoStore();

        sut.Buy(new Order(1, 2));
        sut.Buy(new Order(2, 3));
        sut.Buy(new Order(3, 4));

        var balance = sut.Sell(3, 1);

        balance.Should().SucceedWith(-5);
    }

    [Fact]
    public void Sell_on_empty_store_fails()
    {
        var sut = new FifoStore();

        var balance = sut.Sell(1, 4);

        balance.IsFailure.Should().BeTrue();
    }
}