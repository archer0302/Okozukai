namespace Okozukai.Application.Contracts;

public sealed record TransactionSpendingByTagResponse(
    string Currency,
    IReadOnlyCollection<TransactionSpendingByTagItemResponse> Items);
