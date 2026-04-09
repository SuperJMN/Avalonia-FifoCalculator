using FIFOCalculator.Engine;
using FIFOCalculator.Engine.Kraken;

namespace TestProject1;

public class KrakenLedgerParserTests
{
    private const string SampleCsv = """
        "txid","refid","time","type","subtype","aclass","subclass","asset","wallet","amount","fee","balance"
        "TX1","REF1","2023-01-10 10:00:00","trade","tradespot","currency","fiat","EUR","spot / main",-500.0000,1.5000,0.0000
        "TX2","REF1","2023-01-10 10:00:00","trade","tradespot","currency","crypto","BTC","spot / main",0.0250000000,0,0.0250000000
        "TX3","REF2","2023-06-15 14:00:00","trade","tradespot","currency","crypto","BTC","spot / main",-0.0100000000,0,0.0150000000
        "TX4","REF2","2023-06-15 14:00:00","trade","tradespot","currency","fiat","EUR","spot / main",300.0000,0.9000,299.1000
        """;

    [Fact]
    public void ParseCsv_returns_all_entries()
    {
        using var reader = new StringReader(SampleCsv);
        var entries = LedgerCsvParser.ParseCsv(reader);
        entries.Should().HaveCount(4);
    }

    [Fact]
    public void ParseCsv_parses_fields_correctly()
    {
        using var reader = new StringReader(SampleCsv);
        var entries = LedgerCsvParser.ParseCsv(reader);

        var btcBuy = entries.First(e => e.TxId == "TX2");
        btcBuy.RefId.Should().Be("REF1");
        btcBuy.Type.Should().Be("trade");
        btcBuy.Asset.Should().Be("BTC");
        btcBuy.Amount.Should().Be(0.025m);
        btcBuy.Fee.Should().Be(0m);
    }

    [Fact]
    public void ToFifoEntries_buy_includes_fees_in_cost_basis()
    {
        using var reader = new StringReader(SampleCsv);
        var ledger = LedgerCsvParser.ParseCsv(reader);
        var entries = LedgerCsvParser.ToFifoEntries(ledger, "BTC");

        var buy = entries.First(e => e.Units > 0);

        buy.Units.Should().Be(0.025m);
        // Cost basis = (500 + 1.50 fee) / 0.025 = 20,060
        buy.PricePerUnit.Should().Be((500m + 1.5m) / 0.025m);
    }

    [Fact]
    public void ToFifoEntries_sell_subtracts_fees_from_proceeds()
    {
        using var reader = new StringReader(SampleCsv);
        var ledger = LedgerCsvParser.ParseCsv(reader);
        var entries = LedgerCsvParser.ToFifoEntries(ledger, "BTC");

        var sell = entries.First(e => e.Units < 0);

        sell.Units.Should().Be(-0.01m);
        // Net proceeds = (300 - 0.90 fee) / 0.01 = 29,910
        sell.PricePerUnit.Should().Be((300m - 0.9m) / 0.01m);
    }

    [Fact]
    public void ToFifoEntries_returns_sorted_by_date()
    {
        using var reader = new StringReader(SampleCsv);
        var ledger = LedgerCsvParser.ParseCsv(reader);
        var entries = LedgerCsvParser.ToFifoEntries(ledger, "BTC");

        entries.Should().HaveCount(2);
        entries[0].Units.Should().BePositive(); // buy first (Jan)
        entries[1].Units.Should().BeNegative(); // sell second (Jun)
    }

    [Fact]
    public void ToFifoEntries_ignores_unrelated_assets()
    {
        const string csv = """
            "txid","refid","time","type","subtype","aclass","subclass","asset","wallet","amount","fee","balance"
            "TX1","REF1","2023-01-10 10:00:00","trade","tradespot","currency","fiat","EUR","spot / main",-100.0000,0.5000,0.0000
            "TX2","REF1","2023-01-10 10:00:00","trade","tradespot","currency","crypto","ETH","spot / main",0.0500000000,0,0.0500000000
            """;

        using var reader = new StringReader(csv);
        var ledger = LedgerCsvParser.ParseCsv(reader);
        var entries = LedgerCsvParser.ToFifoEntries(ledger, "BTC");

        entries.Should().BeEmpty();
    }

    [Fact]
    public void ToFifoEntries_handles_spend_receive_pairs()
    {
        const string csv = """
            "txid","refid","time","type","subtype","aclass","subclass","asset","wallet","amount","fee","balance"
            "TX1","REF1","2022-03-07 22:02:00","spend","","currency","hold","EUR.HOLD","spot / main",-47.1600,0.7100,0.0000
            "TX2","REF1","2022-03-07 22:02:00","receive","","currency","crypto","BTC","spot / main",0.0013534800,0,0.0013534800
            """;

        using var reader = new StringReader(csv);
        var ledger = LedgerCsvParser.ParseCsv(reader);
        var entries = LedgerCsvParser.ToFifoEntries(ledger, "BTC");

        entries.Should().HaveCount(1);
        var buy = entries[0];
        buy.Units.Should().Be(0.0013534800m);
        // Cost = (47.16 + 0.71) / 0.0013534800
        buy.PricePerUnit.Should().Be((47.16m + 0.71m) / 0.0013534800m);
    }

    [Fact]
    public void ToFifoEntries_normalizes_hold_asset_names()
    {
        const string csv = """
            "txid","refid","time","type","subtype","aclass","subclass","asset","wallet","amount","fee","balance"
            "TX1","REF1","2022-03-07 22:02:00","spend","","currency","hold","EUR.HOLD","spot / main",-50.0000,0.0000,0.0000
            "TX2","REF1","2022-03-07 22:02:00","receive","","currency","crypto","BTC","spot / main",0.0010000000,0,0.0010000000
            """;

        using var reader = new StringReader(csv);
        var ledger = LedgerCsvParser.ParseCsv(reader);
        // EUR.HOLD should match "EUR"
        var entries = LedgerCsvParser.ToFifoEntries(ledger, "BTC", "EUR");

        entries.Should().HaveCount(1);
    }

    [Fact]
    public void ToFifoEntries_ignores_deposits_by_default()
    {
        const string csv = """
            "txid","refid","time","type","subtype","aclass","subclass","asset","wallet","amount","fee","balance"
            "TX1","REF1","2022-03-07 22:01:59","deposit","","currency","hold","EUR.HOLD","spot / main",50.0000,2.1300,47.8700
            "TX2","REF2","2023-05-01 10:00:00","withdrawal","","currency","crypto","BTC","spot / main",-0.5000000000,0.0001,0.0000000000
            "TX3","REF3","2022-03-25 08:47:20","deposit","","currency","crypto","BTC","spot / main",0.1449503300,0,0.1449503300
            """;

        using var reader = new StringReader(csv);
        var ledger = LedgerCsvParser.ParseCsv(reader);
        var entries = LedgerCsvParser.ToFifoEntries(ledger, "BTC");

        entries.Should().BeEmpty();
    }

    [Fact]
    public void ToFifoEntries_includes_deposits_with_cost_basis_provider()
    {
        const string csv = """
            "txid","refid","time","type","subtype","aclass","subclass","asset","wallet","amount","fee","balance"
            "TX1","REF1","2022-03-25 08:47:20","deposit","","currency","crypto","BTC","spot / main",0.1449503300,0,0.1449503300
            """;

        using var reader = new StringReader(csv);
        var ledger = LedgerCsvParser.ParseCsv(reader);
        var entries = LedgerCsvParser.ToFifoEntries(ledger, "BTC",
            depositCostBasisProvider: _ => 39668.76m);

        entries.Should().HaveCount(1);
        var buy = entries[0];
        buy.Units.Should().Be(0.14495033m);
        buy.PricePerUnit.Should().Be(39668.76m);
    }

    [Fact]
    public void Deposits_with_provider_integrate_into_fifo()
    {
        const string csv = """
            "txid","refid","time","type","subtype","aclass","subclass","asset","wallet","amount","fee","balance"
            "TX1","REF1","2023-01-01 10:00:00","deposit","","currency","crypto","BTC","spot / main",1.0000000000,0,1.0000000000
            "TX2","REF2","2023-06-01 10:00:00","trade","tradespot","currency","crypto","BTC","spot / main",-0.5000000000,0,0.5000000000
            "TX3","REF2","2023-06-01 10:00:00","trade","tradespot","currency","fiat","EUR","spot / main",15000.0000,30.0000,14970.0000
            """;

        using var reader = new StringReader(csv);
        var ledger = LedgerCsvParser.ParseCsv(reader);
        var entries = LedgerCsvParser.ToFifoEntries(ledger, "BTC",
            depositCostBasisProvider: _ => 20000m);

        var calc = new BalanceCalculator();
        var result = calc.Calculate(entries);

        result.IsSuccess.Should().BeTrue();
        // Buy 1 BTC at 20000, sell 0.5 at (15000-30)/0.5 = 29940
        // Gain = 0.5 * (29940 - 20000) = 4970
        result.Value.GainOrLoss.Should().Be(0.5m * (29940m - 20000m));
        result.Value.RemainingInventoryValue.Should().Be(0.5m * 20000m);
    }

    [Fact]
    public void Full_pipeline_with_calculator()
    {
        const string csv = """
            "txid","refid","time","type","subtype","aclass","subclass","asset","wallet","amount","fee","balance"
            "TX1","REF1","2023-01-10 10:00:00","trade","tradespot","currency","fiat","EUR","spot / main",-1000.0000,2.0000,0.0000
            "TX2","REF1","2023-01-10 10:00:00","trade","tradespot","currency","crypto","BTC","spot / main",0.0500000000,0,0.0500000000
            "TX3","REF2","2023-06-15 14:00:00","trade","tradespot","currency","crypto","BTC","spot / main",-0.0300000000,0,0.0200000000
            "TX4","REF2","2023-06-15 14:00:00","trade","tradespot","currency","fiat","EUR","spot / main",900.0000,1.5000,898.5000
            """;

        using var reader = new StringReader(csv);
        var ledger = LedgerCsvParser.ParseCsv(reader);
        var entries = LedgerCsvParser.ToFifoEntries(ledger, "BTC");

        var calculator = new BalanceCalculator();
        var result = calculator.Calculate(entries);

        result.IsSuccess.Should().BeTrue();

        // Buy: 0.05 BTC at (1000+2)/0.05 = 20,040 EUR/BTC
        // Sell: 0.03 BTC at (900-1.5)/0.03 = 29,950 EUR/BTC
        // Gain = 0.03 * (29950 - 20040) = 297.30
        var expectedBuyPrice = (1000m + 2m) / 0.05m;
        var expectedSellPrice = (900m - 1.5m) / 0.03m;
        var expectedGain = 0.03m * (expectedSellPrice - expectedBuyPrice);

        result.Value.GainOrLoss.Should().Be(expectedGain);
        // Remaining: 0.02 BTC at 20,040
        result.Value.RemainingInventoryValue.Should().Be(0.02m * expectedBuyPrice);
    }
}
