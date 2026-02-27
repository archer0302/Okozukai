namespace Okozukai.Application.Contracts;

public sealed record TransactionPeriodRollupResponse(
    string Currency,
    decimal Opening,
    decimal TotalIn,
    decimal TotalOut,
    decimal NetChange,
    decimal Closing);
