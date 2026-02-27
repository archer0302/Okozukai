namespace Okozukai.Application.Contracts;

public sealed record SpendingByTagMonthlyResponse(
    string Currency,
    IReadOnlyCollection<SpendingByTagMonthResponse> Months);

public sealed record SpendingByTagMonthResponse(
    int Year,
    int Month,
    IReadOnlyCollection<TransactionSpendingByTagItemResponse> Items);
