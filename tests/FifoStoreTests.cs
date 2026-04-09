using CSharpFunctionalExtensions.FluentAssertions;
using FIFOCalculator.Engine;

namespace TestProject1;

public class FifoStoreTests
{
    [Fact]
    public void New_store_has_zero_inventory()
    {
        var sut = new FifoStore();
        sut.InventoryValue.Should().Be(0);
        sut.Units.Should().Be(0);
    }

    [Fact]
    public void Buy_increases_inventory()
    {
        var sut = new FifoStore();
        sut.Buy(new Order(5, 100));

        sut.Units.Should().Be(5);
        sut.InventoryValue.Should().Be(500);
    }

    [Fact]
    public void Multiple_buys_accumulate()
    {
        var sut = new FifoStore();
        sut.Buy(new Order(2, 50));
        sut.Buy(new Order(3, 80));

        sut.Units.Should().Be(5);
        sut.InventoryValue.Should().Be(2 * 50 + 3 * 80);
    }

    [Fact]
    public void Sell_on_empty_store_fails()
    {
        var sut = new FifoStore();
        sut.Sell(1, 10).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Sell_more_than_available_fails()
    {
        var sut = new FifoStore();
        sut.Buy(new Order(2, 50));

        sut.Sell(3, 60).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Sell_more_than_available_does_not_modify_store()
    {
        var sut = new FifoStore();
        sut.Buy(new Order(2, 50));

        sut.Sell(3, 60);

        sut.Units.Should().Be(2);
        sut.InventoryValue.Should().Be(100);
    }

    [Fact]
    public void Simple_sell_at_profit()
    {
        var sut = new FifoStore();
        sut.Buy(new Order(1, 100));

        var result = sut.Sell(1, 150);

        result.Should().SucceedWith(50m);
        sut.Units.Should().Be(0);
    }

    [Fact]
    public void Simple_sell_at_loss()
    {
        var sut = new FifoStore();
        sut.Buy(new Order(1, 100));

        var result = sut.Sell(1, 60);

        result.Should().SucceedWith(-40m);
    }

    [Fact]
    public void Simple_sell_at_same_price()
    {
        var sut = new FifoStore();
        sut.Buy(new Order(1, 100));

        var result = sut.Sell(1, 100);

        result.Should().SucceedWith(0m);
    }

    [Fact]
    public void Fifo_order_is_respected()
    {
        var sut = new FifoStore();
        sut.Buy(new Order(1, 10));  // oldest — should be sold first
        sut.Buy(new Order(1, 50));  // newer — should remain

        var result = sut.Sell(1, 30);

        // Gain = (30 - 10) * 1 = 20 (uses the first/oldest buy)
        result.Should().SucceedWith(20m);
        sut.Units.Should().Be(1);
        sut.InventoryValue.Should().Be(50);
    }

    [Fact]
    public void Sell_spans_multiple_segments()
    {
        var sut = new FifoStore();
        sut.Buy(new Order(1, 20));
        sut.Buy(new Order(2, 30));
        sut.Buy(new Order(3, 40));

        // Sell 4 units at 50:
        //  1 from segment 1 (cost 20): gain = 50-20 = 30
        //  2 from segment 2 (cost 60): gain = 100-60 = 40
        //  1 from segment 3 (cost 40): gain = 50-40 = 10
        //  Total = 80
        var result = sut.Sell(4, 50);

        result.Should().SucceedWith(80m);
        sut.Units.Should().Be(2);
        sut.InventoryValue.Should().Be(80); // 2 units at 40
    }

    [Fact]
    public void Partial_segment_consumption_preserves_remainder()
    {
        var sut = new FifoStore();
        sut.Buy(new Order(10, 5));

        sut.Sell(3, 8); // Take 3 from the 10-unit segment

        sut.Units.Should().Be(7);
        sut.InventoryValue.Should().Be(35); // 7 * 5
    }

    [Fact]
    public void Sequential_sells_exhaust_fifo_correctly()
    {
        var sut = new FifoStore();
        sut.Buy(new Order(2, 10));
        sut.Buy(new Order(2, 20));

        // First sell: takes 1 unit from first segment (cost 10)
        sut.Sell(1, 15).Should().SucceedWith(5m);
        // Second sell: takes 1 remaining from first + 1 from second (cost 10 + 20)
        sut.Sell(2, 25).Should().SucceedWith(25m - 10m + 25m - 20m);
        // Third sell: takes 1 remaining from second (cost 20)
        sut.Sell(1, 30).Should().SucceedWith(10m);

        sut.Units.Should().Be(0);
    }

    [Fact]
    public void Sell_exact_inventory_empties_store()
    {
        var sut = new FifoStore();
        sut.Buy(new Order(5, 10));

        sut.Sell(5, 15);

        sut.Units.Should().Be(0);
        sut.InventoryValue.Should().Be(0);
    }

    [Fact]
    public void Fractional_units()
    {
        var sut = new FifoStore();
        sut.Buy(new Order(0.5m, 1000));

        var result = sut.Sell(0.3m, 1200);

        // Gain = 0.3 * (1200 - 1000) = 60
        result.Should().SucceedWith(60m);
        sut.Units.Should().Be(0.2m);
    }
}
