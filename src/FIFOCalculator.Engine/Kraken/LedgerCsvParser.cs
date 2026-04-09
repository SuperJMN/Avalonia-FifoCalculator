using System.Globalization;

namespace FIFOCalculator.Engine.Kraken;

public static class LedgerCsvParser
{
    private static readonly string[] TradeLikeTypes = ["trade", "spend", "receive"];

    public static IReadOnlyList<LedgerEntry> ParseCsv(TextReader reader)
    {
        var entries = new List<LedgerEntry>();
        var header = reader.ReadLine();
        if (header == null) return entries;

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var entry = ParseLine(line);
            entries.Add(entry);
        }

        return entries;
    }

    /// <summary>
    /// Converts Kraken ledger entries into FIFO entries for a given asset pair.
    /// </summary>
    /// <param name="ledger">Parsed ledger entries from <see cref="ParseCsv"/>.</param>
    /// <param name="targetAsset">The asset to track (e.g., "BTC").</param>
    /// <param name="quoteAsset">The quote/fiat currency (e.g., "EUR").</param>
    /// <param name="depositCostBasisProvider">
    /// Optional callback to provide the cost basis (price per unit in quote currency)
    /// for deposits of the target asset. When provided, deposits are included as buy entries.
    /// When null, deposits are ignored.
    /// </param>
    public static IReadOnlyList<Entry> ToFifoEntries(
        IReadOnlyList<LedgerEntry> ledger,
        string targetAsset,
        string quoteAsset = "EUR",
        Func<LedgerEntry, decimal>? depositCostBasisProvider = null)
    {
        var results = new List<Entry>();

        // Handle trade/spend/receive pairs
        var paired = ledger
            .Where(e => TradeLikeTypes.Contains(e.Type))
            .GroupBy(e => e.RefId)
            .Where(g => g.Count() == 2);

        foreach (var group in paired)
        {
            var legs = group.ToArray();
            var targetLeg = legs.FirstOrDefault(l => NormalizeAsset(l.Asset) == targetAsset);
            var quoteLeg = legs.FirstOrDefault(l => NormalizeAsset(l.Asset) == quoteAsset);

            if (targetLeg == null || quoteLeg == null) continue;

            var units = Math.Abs(targetLeg.Amount);
            if (units == 0) continue;

            var quoteAmount = Math.Abs(quoteLeg.Amount);
            var totalFees = quoteLeg.Fee + targetLeg.Fee * (quoteAmount / units);

            decimal pricePerUnit;
            decimal signedUnits;

            if (targetLeg.Amount > 0)
            {
                pricePerUnit = (quoteAmount + totalFees) / units;
                signedUnits = units;
            }
            else
            {
                pricePerUnit = (quoteAmount - totalFees) / units;
                signedUnits = -units;
            }

            results.Add(new Entry(targetLeg.Time, signedUnits, pricePerUnit));
        }

        // Handle deposits as buys (when a cost basis provider is given)
        if (depositCostBasisProvider != null)
        {
            var deposits = ledger
                .Where(e => e.Type == "deposit" && NormalizeAsset(e.Asset) == targetAsset && e.Amount > 0);

            foreach (var deposit in deposits)
            {
                var costBasis = depositCostBasisProvider(deposit);
                results.Add(new Entry(deposit.Time, deposit.Amount, costBasis));
            }
        }

        return results.OrderBy(e => e.When).ToList();
    }

    private static string NormalizeAsset(string asset) =>
        asset.Replace(".HOLD", "").Replace(".S", "");

    private static LedgerEntry ParseLine(string line)
    {
        var fields = SplitCsvLine(line);

        return new LedgerEntry(
            TxId: Unquote(fields[0]),
            RefId: Unquote(fields[1]),
            Time: DateTimeOffset.Parse(Unquote(fields[2]), CultureInfo.InvariantCulture),
            Type: Unquote(fields[3]),
            Subtype: Unquote(fields[4]),
            Asset: Unquote(fields[7]),
            Amount: decimal.Parse(Unquote(fields[9]), CultureInfo.InvariantCulture),
            Fee: decimal.Parse(Unquote(fields[10]), CultureInfo.InvariantCulture),
            Balance: decimal.Parse(Unquote(fields[11]), CultureInfo.InvariantCulture));
    }

    private static string Unquote(string s) =>
        s.Length >= 2 && s[0] == '"' && s[^1] == '"' ? s[1..^1] : s;

    private static string[] SplitCsvLine(string line)
    {
        var fields = new List<string>();
        var inQuotes = false;
        var start = 0;

        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '"') inQuotes = !inQuotes;
            if (line[i] == ',' && !inQuotes)
            {
                fields.Add(line[start..i]);
                start = i + 1;
            }
        }

        fields.Add(line[start..]);
        return fields.ToArray();
    }
}
