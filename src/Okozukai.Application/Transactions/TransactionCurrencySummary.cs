namespace Okozukai.Application.Transactions;

public sealed record TransactionCurrencySummary(
    string Currency,
    decimal TotalIn,
    decimal TotalOut);
