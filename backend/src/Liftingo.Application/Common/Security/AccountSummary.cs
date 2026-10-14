namespace Liftingo.Application.Common.Security;

public sealed record AccountSummary(
    Guid Id,
    string Email,
    bool EmailVerified,
    bool HasPassword,
    IReadOnlyList<string> Providers,
    DateTimeOffset CreatedAt);