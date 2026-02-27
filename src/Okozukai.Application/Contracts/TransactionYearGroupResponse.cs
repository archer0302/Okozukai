namespace Okozukai.Application.Contracts;

public sealed record TransactionYearGroupResponse(
    int Year,
    IReadOnlyCollection<TransactionMonthGroupResponse> Months,
    IReadOnlyCollection<TransactionPeriodRollupResponse> Rollups);
