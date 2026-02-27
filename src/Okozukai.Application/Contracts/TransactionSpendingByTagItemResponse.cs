namespace Okozukai.Application.Contracts;

public sealed record TransactionSpendingByTagItemResponse(
    Guid? TagId,
    string TagName,
    decimal TotalOut);
