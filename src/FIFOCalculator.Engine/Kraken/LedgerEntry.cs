namespace FIFOCalculator.Engine.Kraken;

public record LedgerEntry(
    string TxId,
    string RefId,
    DateTimeOffset Time,
    string Type,
    string Subtype,
    string Asset,
    decimal Amount,
    decimal Fee,
    decimal Balance);
