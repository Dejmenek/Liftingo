namespace Liftingo.Domain.Account;

public sealed class UserConsent
{
    public const int DocumentVersionMaxLength = 32;

    // For EF Core.
    private UserConsent()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public ConsentType Type { get; private set; }

    public bool Granted { get; private set; }

    public DateTimeOffset RecordedAt { get; private set; }

    public string DocumentVersion { get; private set; } = string.Empty;

    // Set when a takeover of an unverified account replaces the consents.
    public DateTimeOffset? SupersededAt { get; private set; }

    public static UserConsent Record(Guid userId, ConsentType type, bool granted, string documentVersion, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(documentVersion) || documentVersion.Length > DocumentVersionMaxLength)
        {
            throw new AccountDomainException(
                $"Wersja dokumentów musi mieć od 1 do {DocumentVersionMaxLength} znaków.");
        }

        return new UserConsent
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            Granted = granted,
            RecordedAt = now,
            DocumentVersion = documentVersion,
        };
    }

    public void Supersede(DateTimeOffset now)
    {
        if (SupersededAt is not null)
        {
            throw new AccountDomainException("Ta zgoda została już zastąpiona.");
        }

        SupersededAt = now;
    }
}