namespace Okozukai.Application.Contracts;

public sealed record TransactionSummaryResponse(
    string Currency,
    decimal TotalIn,
    decimal TotalOut,
    decimal Net);
