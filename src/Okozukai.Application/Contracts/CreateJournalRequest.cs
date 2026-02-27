namespace Okozukai.Application.Contracts;

public sealed record CreateJournalRequest(
    string Name,
    string PrimaryCurrency);
