namespace Okozukai.Application.Contracts;

public sealed record TransactionMonthGroupResponse(
    int Year,
    int Month,
    IReadOnlyCollection<TransactionResponse> Transactions,
    IReadOnlyCollection<TransactionPeriodRollupResponse> Rollups);
